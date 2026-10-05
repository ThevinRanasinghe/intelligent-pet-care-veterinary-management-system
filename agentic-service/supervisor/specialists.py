"""Adapters invoking the four existing specialist graphs.

Each call builds an ISOLATED input state — only the id key plus
``auth_token`` — so the supervisor's history/trajectory never leaks into
a specialist's context. Tools run inside ``workflow_tool_context`` so
the call trace and per-agent allow-list isolation are enforced.
"""
import logging
from datetime import datetime, timezone
from typing import Any, Dict, Optional

from consultation_agent.agent import consultation_agent_app
from diagnosis_agent.agent import diagnosis_agent_app
from inventory_agent.agent import inventory_agent_app
from scheduling_agent.agent import scheduling_agent_app
from shared.tool_registry import workflow_tool_context

logger = logging.getLogger("SupervisorSpecialists")

# agent name -> (compiled graph, state id-key)
SPECIALIST_APPS: Dict[str, Dict[str, Any]] = {
    "consultation_agent": {
        "app": consultation_agent_app,
        "id_key": "consultation_request_id",
    },
    "diagnosis_agent": {"app": diagnosis_agent_app, "id_key": "examination_id"},
    "scheduling_agent": {"app": scheduling_agent_app, "id_key": "request_id"},
    "inventory_agent": {"app": inventory_agent_app, "id_key": "treatment_record_id"},
}


def _utcnow() -> str:
    return datetime.now(timezone.utc).isoformat()


async def run_specialist(
    agent_name: str,
    input_id: str,
    auth_token: Optional[str],
    step_number: int,
    task: str = "",
    context: Optional[Dict[str, Any]] = None,
) -> Dict[str, Any]:
    """Invokes one specialist graph and maps the result to a StepRecord dict.

    ``context`` carries selected supervisor facts (e.g. the consultation
    assessment for the scheduling agent) into the specialist's isolated
    input state without leaking the full workflow history.
    """
    spec = SPECIALIST_APPS.get(agent_name)
    if spec is None:
        return {
            "stepNumber": step_number,
            "agentName": agent_name,
            "task": task,
            "status": "Failed",
            "error": f"unsupported_agent:{agent_name}",
            "toolCalls": [],
            "inputSummary": {},
            "retryCount": 0,
            "startedAt": _utcnow(),
            "completedAt": _utcnow(),
        }

    initial_state = {
        spec["id_key"]: input_id,
        "auth_token": auth_token,
        "raw_input_data": {},
        "llm_response": None,
        "assessment": None,
        "retry_count": 0,
        "error": None,
    }
    if context:
        initial_state.update(context)

    started_at = _utcnow()
    record: Dict[str, Any] = {
        "stepNumber": step_number,
        "agentName": agent_name,
        "task": task or f"Run {agent_name}",
        "inputSummary": {spec["id_key"]: input_id},
        "toolCalls": [],
        "startedAt": started_at,
    }

    try:
        with workflow_tool_context(agent_name) as trace:
            result = await spec["app"].ainvoke(initial_state)
        record["toolCalls"] = [t.to_dict() for t in trace]
    except Exception as exc:  # noqa: BLE001 — specialist crash is a failed step
        logger.exception("specialist %s crashed", agent_name)
        record.update(
            status="Failed",
            error=f"specialist_error:{type(exc).__name__}",
            completedAt=_utcnow(),
        )
        return record

    assessment = result.get("assessment")
    record["retryCount"] = int(result.get("retry_count") or 0)
    # Specialists may surface structured input facts for audit (e.g. the
    # scheduling agent's preferred date/time and required slot count).
    if result.get("input_summary"):
        record["inputSummary"].update(result["input_summary"])
    if assessment is not None:
        record["output"] = (
            assessment.model_dump()
            if hasattr(assessment, "model_dump")
            else dict(assessment)
        )
        record["status"] = "Completed"
    else:
        record["status"] = "Failed"
        record["error"] = result.get("error") or "no_assessment"
    record["completedAt"] = _utcnow()
    return record
