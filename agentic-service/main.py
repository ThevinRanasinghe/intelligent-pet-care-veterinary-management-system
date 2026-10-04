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

import uvicorn
from fastapi import Depends, FastAPI, Header, HTTPException, Request

from shared.config import get_internal_api_key, get_service_port

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
    """Unauthenticated liveness probe — reports configuration readiness only."""
    return {
        "status": "ok",
        "internalKeyConfigured": get_internal_api_key() is not None,
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


if __name__ == "__main__":
    if get_internal_api_key() is None:
        logger.warning(
            "AGENTIC_INTERNAL_KEY is not set — all agent endpoints will return 503 "
            "until it is configured."
        )
    uvicorn.run(app, host="0.0.0.0", port=get_service_port())
