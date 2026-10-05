"""PetCare Agentic Service — internal, advisory AI agents.

This service is NOT a public browser API. The intended call chain is:

    React / Flutter -> ASP.NET Core API -> Agentic Service -> PetCare API

Requests must carry the shared service credential in the X-Internal-Key
header (compared with the AGENTIC_INTERNAL_KEY environment variable).
The caller's bearer token is forwarded to the PetCare backend for all
data reads, so role and organisation scoping stay enforced by the API.

All agents are read-only and advisory: they analyse data and return
recommendations; humans and the existing business workflows remain
authoritative for every decision.
"""
import hmac
import logging
import time
from typing import Optional

import httpx
import uvicorn
from fastapi import Depends, FastAPI, Header, HTTPException, Request

from langgraph.types import Command
from pydantic import BaseModel

from shared.config import get_api_base_url, get_internal_api_key, get_service_port
from supervisor.graph import workflow_graph
from supervisor.models import WorkflowRunResponse

from consultation_agent.agent import consultation_agent_app
from diagnosis_agent.agent import diagnosis_agent_app
from inventory_agent.agent import inventory_agent_app
from scheduling_agent.agent import scheduling_agent_app

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("AgenticService")

app = FastAPI(
    title="PetCare Agentic Service",
    description="Internal advisory AI agents for the PetCare system",
    version="1.0.0",
)

# NOTE: no CORSMiddleware is registered on purpose. This service is
# internal-only (ASP.NET -> Agentic Service); browser CORS exposure is
# neither needed nor wanted.


async def require_internal_key(x_internal_key: str = Header(default=None)) -> None:
    """Service-to-service authentication via a shared secret."""
    expected = get_internal_api_key()
    if expected is None:
        raise HTTPException(
            status_code=503,
            detail="Agentic service is not configured: AGENTIC_INTERNAL_KEY is unset.",
        )
    if not x_internal_key or not hmac.compare_digest(x_internal_key, expected):
        raise HTTPException(status_code=401, detail="Invalid or missing internal service key.")


@app.get("/health")
async def health():
    """Unauthenticated liveness probe.

    Reports configuration readiness AND reachability of the configured
    PetCare backend (/health on the API root). A pointing-at-the-wrong-
    environment mistake (e.g. agents reading a disposable DB) surfaces
    here instead of as misleading not_found tool failures mid-workflow.
    """
    api_base = get_api_base_url()
    backend_health = api_base[: -len("/api")] + "/health" if api_base.endswith("/api") else f"{api_base}/health"
    reachable = False
    try:
        async with httpx.AsyncClient(timeout=3.0) as client:
            reachable = (await client.get(backend_health)).status_code == 200
    except httpx.HTTPError:
        reachable = False
    return {
        "status": "ok",
        "internalKeyConfigured": get_internal_api_key() is not None,
        "backendBaseUrl": api_base,
        "backendReachable": reachable,
    }


async def _run_agent(agent_app, agent_name: str, input_id: str, initial_state: dict, request: Request):
    """Shared runner: initialises state, invokes the graph, logs safe metadata."""
    initial_state["auth_token"] = request.headers.get("Authorization")
    started = time.perf_counter()
    logger.info("agent=%s input=%s started", agent_name, input_id)

    try:
        result_state = await agent_app.ainvoke(initial_state)
    except Exception:
        logger.exception("agent=%s input=%s graph_error", agent_name, input_id)
        raise HTTPException(status_code=500, detail="Agent execution failed.")

    duration_ms = int((time.perf_counter() - started) * 1000)
    assessment = result_state.get("assessment")
    outcome = "assessment" if assessment else "none"
    logger.info(
        "agent=%s input=%s finished outcome=%s duration_ms=%d",
        agent_name, input_id, outcome, duration_ms,
    )

    if not assessment:
        raise HTTPException(status_code=500, detail="Agent failed to produce an assessment.")
    return assessment.model_dump()


@app.post("/api/agents/consultation-analysis/{consultation_id}", dependencies=[Depends(require_internal_key)])
async def analyze_consultation(consultation_id: str, request: Request):
    """Advisory triage assessment for a consultation request."""
    return await _run_agent(
        consultation_agent_app,
        "consultation",
        consultation_id,
        {
            "consultation_request_id": consultation_id,
            "raw_input_data": {},
            "llm_response": None,
            "assessment": None,
            "retry_count": 0,
            "error": None,
        },
        request,
    )


@app.post("/api/agents/diagnosis-analysis/{examination_id}", dependencies=[Depends(require_internal_key)])
async def analyze_diagnosis(examination_id: str, request: Request):
    """Advisory diagnosis assistance for an examination."""
    return await _run_agent(
        diagnosis_agent_app,
        "diagnosis",
        examination_id,
        {
            "examination_id": examination_id,
            "raw_input_data": {},
            "llm_response": None,
            "assessment": None,
            "retry_count": 0,
            "error": None,
        },
        request,
    )


@app.post("/api/agents/scheduling-planning/{request_id}", dependencies=[Depends(require_internal_key)])
async def plan_scheduling(request_id: str, request: Request):
    """Advisory appointment + quotation proposal for a consultation request."""
    return await _run_agent(
        scheduling_agent_app,
        "scheduling",
        request_id,
        {
            "request_id": request_id,
            "raw_input_data": {},
            "llm_response": None,
            "assessment": None,
            "retry_count": 0,
            "error": None,
        },
        request,
    )


@app.post("/api/agents/inventory-planning/{treatment_record_id}", dependencies=[Depends(require_internal_key)])
async def plan_inventory(treatment_record_id: str, request: Request):
    """Advisory inventory/medicine recommendation for a treatment record.

    The path parameter is a TreatmentRecordId — the medicine request for a
    treatment is the batch of Prescription rows that share that id.
    """
    return await _run_agent(
        inventory_agent_app,
        "inventory",
        treatment_record_id,
        {
            "treatment_record_id": treatment_record_id,
            "raw_input_data": {},
            "llm_response": None,
            "assessment": None,
            "retry_count": 0,
            "error": None,
        },
        request,
    )


# ---------------------------------------------------------------------------
# Supervisor workflow endpoints (spec §3)
#
# The ASP.NET backend is the system of record: it persists each response's
# snapshot ({plan, steps, delegationCount, revisionCount, approval}) and sends
# it back so the graph can rehydrate when no in-memory checkpoint exists.
# All endpoints require X-Internal-Key; the caller's Authorization header is
# forwarded to specialists as auth_token.
# ---------------------------------------------------------------------------


def _thread_config(workflow_id: str) -> dict:
    return {"configurable": {"thread_id": workflow_id}}


async def _checkpoint_values(workflow_id: str):
    snapshot = await workflow_graph.aget_state(_thread_config(workflow_id))
    return snapshot


def _derive_completed_steps(snapshot: dict) -> list:
    completed = {
        s.get("stepNumber")
        for s in (snapshot.get("steps") or [])
        if s.get("status") in ("Completed", "NoProposal", "Skipped")
    }
    return sorted(n for n in completed if n is not None)


def _rehydrate_input(snapshot: dict) -> dict:
    """Rebuilds graph state from the backend-persisted snapshot."""
    state = dict(snapshot or {})
    state.setdefault("steps", [])
    state["completedSteps"] = snapshot.get("completedSteps") or _derive_completed_steps(snapshot)
    state.setdefault("delegationCount", 0)
    state.setdefault("revisionCount", 0)
    # consultation_analyzed is a supervisor-internal gating event, not a
    # persisted backend marker — re-derive it so a rehydrated run does
    # not wait forever for an event that already happened.
    if any(
        s.get("agentName") == "consultation_agent"
        and s.get("status") in ("Completed", "NoProposal")
        for s in (snapshot.get("steps") or [])
    ):
        events = dict(state.get("availableEvents") or {})
        events.setdefault("consultation_analyzed", {"rehydrated": True})
        state["availableEvents"] = events
    return state


def _trajectory_len(values: dict) -> int:
    return len(values.get("trajectory") or [])


async def _respond(
    workflow_id: str, trajectory_before: Optional[int] = None
) -> WorkflowRunResponse:
    """Reads the post-invocation checkpoint and shapes the response.

    When ``trajectory_before`` is given, ``trajectory`` contains ONLY the
    events appended during this invocation; ``trajectoryTotal`` always
    reports the full persisted length. ``GET /state`` passes None so the
    whole trajectory is returned.
    """
    snapshot = await workflow_graph.aget_state(_thread_config(workflow_id))
    values = dict(snapshot.values) if snapshot and snapshot.values else {}
    interrupted = bool(snapshot.next) if snapshot else False
    full_trajectory = list(values.get("trajectory") or [])
    if trajectory_before is None:
        trajectory_before = 0
    return WorkflowRunResponse(
        workflowId=workflow_id,
        status=values.get("status", "Unknown"),
        plan=values.get("plan"),
        steps=list(values.get("steps") or []),
        trajectory=full_trajectory[trajectory_before:],
        trajectoryTotal=len(full_trajectory),
        proposal=values.get("proposal"),
        approvedAction=values.get("approvedAction"),
        delegationCount=int(values.get("delegationCount") or 0),
        revisionCount=int(values.get("revisionCount") or 0),
        failureReason=values.get("failureReason"),
        interrupted=interrupted,
    )


class WorkflowRunRequest(BaseModel):
    consultationRequestId: str
    objective: str = ""
    availableEvents: dict = {}
    snapshot: dict | None = None
    eventRefs: dict | None = None


class WorkflowResumeRequest(BaseModel):
    decision: dict
    snapshot: dict | None = None


class WorkflowAdvanceRequest(BaseModel):
    event: dict
    availableEvents: dict = {}
    snapshot: dict | None = None


@app.post("/api/workflows/{workflow_id}/run", dependencies=[Depends(require_internal_key)])
async def workflow_run(workflow_id: str, body: WorkflowRunRequest, request: Request):
    """Starts (or restarts) a workflow run for a consultation."""
    checkpoint = await _checkpoint_values(workflow_id)
    if checkpoint and checkpoint.values:
        # Re-entrant: merge the caller's fields over the persisted state.
        initial = dict(checkpoint.values)
        before = _trajectory_len(initial)
        # trajectory is an operator.add reducer — passing the stored copy
        # back in would duplicate it. The checkpoint already holds it.
        initial["trajectory"] = []
        initial["availableEvents"] = body.availableEvents or initial.get("availableEvents") or {}
        initial["objective"] = body.objective or initial.get("objective") or ""
        initial["auth_token"] = request.headers.get("Authorization")
    else:
        initial = _rehydrate_input(body.snapshot or {})
        # Merge (not replace): rehydration derives internal gating events
        # like consultation_analyzed from the persisted steps.
        merged_events = dict(initial.get("availableEvents") or {})
        merged_events.update(body.availableEvents or {})
        initial.update(
            workflowId=workflow_id,
            consultationRequestId=body.consultationRequestId,
            objective=body.objective,
            availableEvents=merged_events,
            eventRefs=body.eventRefs or {},
            auth_token=request.headers.get("Authorization"),
            status="Created",
        )
        before = _trajectory_len(initial)
    initial["iterations"] = 0

    try:
        await workflow_graph.ainvoke(initial, config=_thread_config(workflow_id))
    except Exception:
        logger.exception("workflow=%s run failed", workflow_id)
        raise HTTPException(status_code=500, detail="Workflow execution failed.")

    response = await _respond(workflow_id, trajectory_before=before)
    return response.model_dump()


@app.post("/api/workflows/{workflow_id}/resume", dependencies=[Depends(require_internal_key)])
async def workflow_resume(workflow_id: str, body: WorkflowResumeRequest, request: Request):
    """Applies the ClinicManager's approval decision to a paused workflow."""
    verdict = (body.decision or {}).get("decision")
    if verdict not in ("Approved", "Rejected", "RevisionRequested"):
        raise HTTPException(status_code=400, detail="Invalid approval decision.")

    checkpoint = await _checkpoint_values(workflow_id)
    before = _trajectory_len(checkpoint.values) if checkpoint and checkpoint.values else 0
    if checkpoint and checkpoint.next:
        # Paused at the approval gate — resume the interrupt.
        try:
            await workflow_graph.ainvoke(
                Command(resume=body.decision), config=_thread_config(workflow_id)
            )
        except Exception:
            logger.exception("workflow=%s resume failed", workflow_id)
            raise HTTPException(status_code=500, detail="Workflow resume failed.")
    else:
        if checkpoint and checkpoint.values:
            existing = dict(checkpoint.values)
            if existing.get("approval") or existing.get("status") in ("Completed", "Rejected", "Failed"):
                raise HTTPException(status_code=409, detail="Workflow already decided.")
            before = _trajectory_len(existing)
            initial = existing
            # operator.add reducer — the checkpoint already holds these
            # events; re-submitting them would duplicate the trajectory.
            initial["trajectory"] = []
            initial["decision"] = body.decision
            initial["auth_token"] = request.headers.get("Authorization")
        elif body.snapshot:
            initial = _rehydrate_input(body.snapshot)
            initial.update(
                workflowId=workflow_id,
                decision=body.decision,
                auth_token=request.headers.get("Authorization"),
            )
            before = 0
        else:
            raise HTTPException(status_code=404, detail="No workflow state for this id.")
        initial["iterations"] = 0
        try:
            await workflow_graph.ainvoke(initial, config=_thread_config(workflow_id))
        except Exception:
            logger.exception("workflow=%s resume (rehydrated) failed", workflow_id)
            raise HTTPException(status_code=500, detail="Workflow resume failed.")

    response = await _respond(workflow_id, trajectory_before=before)
    return response.model_dump()


@app.post("/api/workflows/{workflow_id}/advance", dependencies=[Depends(require_internal_key)])
async def workflow_advance(workflow_id: str, body: WorkflowAdvanceRequest, request: Request):
    """Asserts a business event (examination_recorded / prescription_created)."""
    event_type = (body.event or {}).get("type")
    reference_id = (body.event or {}).get("referenceId")
    if not event_type or not reference_id:
        raise HTTPException(status_code=400, detail="event.type and event.referenceId are required.")

    checkpoint = await _checkpoint_values(workflow_id)
    if checkpoint and checkpoint.values:
        initial = dict(checkpoint.values)
        before = _trajectory_len(initial)
        # operator.add reducer — strip before re-invoking or the stored
        # trajectory is duplicated (see run/resume for the same pattern).
        initial["trajectory"] = []
    elif body.snapshot:
        initial = _rehydrate_input(body.snapshot)
        initial["workflowId"] = workflow_id
        before = 0
    else:
        raise HTTPException(status_code=404, detail="No workflow state for this id.")

    available = dict(initial.get("availableEvents") or {})
    available.update(body.availableEvents or {})
    available[event_type] = reference_id
    initial["availableEvents"] = available
    refs = dict(initial.get("eventRefs") or {})
    refs[event_type] = str(reference_id)
    initial["eventRefs"] = refs
    initial["auth_token"] = request.headers.get("Authorization")
    initial["iterations"] = 0

    try:
        await workflow_graph.ainvoke(initial, config=_thread_config(workflow_id))
    except Exception:
        logger.exception("workflow=%s advance failed", workflow_id)
        raise HTTPException(status_code=500, detail="Workflow advance failed.")

    response = await _respond(workflow_id, trajectory_before=before)
    return response.model_dump()


@app.get("/api/workflows/{workflow_id}/state", dependencies=[Depends(require_internal_key)])
async def workflow_state(workflow_id: str):
    """Checkpoint inspection — current values + pending nodes."""
    checkpoint = await _checkpoint_values(workflow_id)
    if not checkpoint or not checkpoint.values:
        raise HTTPException(status_code=404, detail="No workflow state for this id.")
    response = await _respond(workflow_id)
    return response.model_dump()


if __name__ == "__main__":
    if get_internal_api_key() is None:
        logger.warning(
            "AGENTIC_INTERNAL_KEY is not set — all agent endpoints will return 503 "
            "until it is configured."
        )
    uvicorn.run(app, host="0.0.0.0", port=get_service_port())
