import logging
import json

from langgraph.graph import StateGraph, END
from langchain_core.messages import HumanMessage, SystemMessage
from pydantic import ValidationError

from shared.backend import BackendApiError
from shared.config import ConfigurationError
from shared.llm import content_to_text, extract_json_object, get_llm

from .models import SchedulingAgentState, SchedulingAssessment, get_safe_fallback
from .tools import fetch_consultation_request, fetch_available_slots, check_conflict

logger = logging.getLogger("SchedulingAgent")


async def retrieve_context_node(state: SchedulingAgentState) -> SchedulingAgentState:
    """Gathers required read-only data from the backend."""
    req_id = state["request_id"]
    logger.info("[%s] Node: retrieve_context", req_id)

    try:
        auth_token = state.get("auth_token")

        consultation = await fetch_consultation_request(req_id, auth_token)

        # Narrow the slot search where the request itself provides hints:
        # follow-up requests carry requestedByVeterinarianId, and owners can
        # express a preferredDate. Both map to the backend's query params.
        preferred_date = consultation.get("preferredDate")
        slots = await fetch_available_slots(
            auth_token,
            veterinarian_id=consultation.get("requestedByVeterinarianId"),
            date=str(preferred_date)[:10] if preferred_date else None,
        )

        state["raw_input_data"] = {
            "consultation": consultation,
            "available_slots": slots,
        }
    except BackendApiError as e:
        logger.error("[%s] Backend context retrieval failed (%s)", req_id, e.kind)
        state["error"] = f"backend_unavailable:{e.kind}"
    except Exception:
        logger.exception("[%s] Unexpected context retrieval failure", req_id)
        state["error"] = "context_retrieval_failed"

    return state


async def plan_scheduling_node(state: SchedulingAgentState) -> SchedulingAgentState:
    """Prompts the LLM to generate the scheduling and quotation proposal."""
    req_id = state["request_id"]
    logger.info("[%s] Node: plan_scheduling (Retry: %d)", req_id, state["retry_count"])

    sys_prompt = """You are a Scheduling & Quotation Planning Agent for a Veterinary clinic.
    Your ONLY job is to analyze the provided consultation request, budget, and available appointment slots.
    Provide a structured proposed appointment and quotation plan.
    DO NOT make approval decisions or directly execute appointment or quotation actions.

    IMPORTANT: You must return ONLY a raw JSON object. Do not include markdown code blocks like ```json. Do not include any conversational text before or after the JSON.

    Output EXACTLY this JSON structure:
    {
        "requestId": "string",
        "recommendedAppointment": {
            "appointmentSlotId": "string",
            "veterinarianId": "string",
            "date": "YYYY-MM-DD",
            "startTime": "HH:MM",
            "endTime": "HH:MM",
            "branch": "string",
            "reason": "string"
        } | null,
        "alternativeSlots": [
            {
                "appointmentSlotId": "string",
                "veterinarianId": "string",
                "date": "YYYY-MM-DD",
                "startTime": "HH:MM",
                "endTime": "HH:MM",
                "branch": "string",
                "reason": "string"
            }
        ],
        "quotationProposal": {
            "budget": 0,
            "items": [
                {
                    "category": "Consultation" | "Examination" | "Treatment" | "Medicine" | "Other",
                    "description": "string",
                    "quantity": 1,
                    "unitPrice": 0,
                    "reason": "string"
                }
            ],
            "estimatedSubtotal": 0,
            "estimatedTotal": 0,
            "withinBudget": true
        } | null,
        "validationSummary": {
            "slotFound": true,
            "veterinarianAvailable": true,
            "noKnownConflict": true,
            "withinRequestedTime": true,
            "withinBudget": true
        },
        "confidence": "Low" | "Moderate" | "High",
        "planningNotes": "string",
        "disclaimer": "AI-generated scheduling and quotation proposal - requires deterministic backend validation and Clinic Manager approval before execution."
    }"""

    if state["error"] and state["retry_count"] > 0:
        sys_prompt += f"\n\nPREVIOUS ERROR (Fix this in your JSON output): {state['error']}"

    human_msg = f"Data to Analyze: {json.dumps(state['raw_input_data'])}"

    try:
        response = await get_llm().ainvoke([
            SystemMessage(content=sys_prompt),
            HumanMessage(content=human_msg),
        ])

        content = extract_json_object(content_to_text(response.content))
        if content is None:
            raise ValueError("LLM output did not contain a JSON object")

        logger.info("[%s] LLM response received (%d chars)", req_id, len(content))
        state["llm_response"] = content
        state["error"] = None
    except ConfigurationError:
        logger.error("[%s] LLM is not configured (missing API key)", req_id)
        state["error"] = "llm_not_configured"
        state["retry_count"] += 1
    except Exception as e:
        logger.error("[%s] LLM call failed: %s", req_id, type(e).__name__)
        state["error"] = f"llm_error:{type(e).__name__}"
        state["retry_count"] += 1

    return state


async def validate_response_node(state: SchedulingAgentState) -> SchedulingAgentState:
    """Validates the LLM JSON output against rules and Pydantic schema."""
    req_id = state["request_id"]
    logger.info("[%s] Node: validate_response", req_id)

    if state["error"]:
        return state

    try:
        raw_json = json.loads(state["llm_response"])
        raw_json["requestId"] = req_id  # Enforce ID correctness

        parsed = SchedulingAssessment(**raw_json)

        # Deterministic backend validations
        auth_token = state.get("auth_token")
        slots = state["raw_input_data"].get("available_slots", [])

        if parsed.recommendedAppointment:
            rec = parsed.recommendedAppointment
            # 1. Validate slot exists
            slot = next((s for s in slots if s.get("id") == rec.appointmentSlotId), None)
            if not slot:
                raise ValueError(f"Recommended slot {rec.appointmentSlotId} does not exist in available slots.")

            # 2. Validate relationships
            if slot.get("veterinarianId") != rec.veterinarianId:
                raise ValueError("Veterinarian ID mismatch.")
            # Note: The JSON date may be in 'YYYY-MM-DD' while C# API may return full ISO string.
            if not slot.get("date", "").startswith(rec.date):
                raise ValueError("Date mismatch.")
            if slot.get("startTime") != rec.startTime + ":00" and slot.get("startTime") != rec.startTime:
                raise ValueError("Start time mismatch.")

            # 3. Check conflict via backend tool — a failed check raises
            # BackendApiError and is treated as an unsafe result, never as
            # "no conflict".
            conflict = await check_conflict(rec.veterinarianId, rec.date, rec.startTime, rec.endTime, auth_token)
            if conflict:
                parsed.validationSummary.noKnownConflict = False
                raise ValueError("Backend detected a scheduling conflict for the recommended appointment.")
            parsed.validationSummary.noKnownConflict = True

        # Quotation math deterministic overwrite (Do not trust LLM math)
        if parsed.quotationProposal:
            qp = parsed.quotationProposal
            actual_subtotal = sum(item.quantity * item.unitPrice for item in qp.items)
            qp.estimatedSubtotal = actual_subtotal
            qp.estimatedTotal = actual_subtotal  # Simplified logic; could include taxes if applicable
            qp.withinBudget = qp.estimatedTotal <= qp.budget if qp.budget > 0 else True
            parsed.validationSummary.withinBudget = qp.withinBudget

        state["assessment"] = parsed
        logger.info("[%s] Validation passed", req_id)
    except (json.JSONDecodeError, ValidationError, ValueError) as e:
        logger.warning("[%s] Validation failed: %s", req_id, type(e).__name__)
        state["error"] = f"validation_error:{type(e).__name__}"
        state["retry_count"] += 1
    except BackendApiError as e:
        # Conflict check (or another backend call) failed mid-validation.
        # The result is unsafe — retry, then fall back.
        logger.warning("[%s] Backend validation call failed (%s)", req_id, e.kind)
        state["error"] = f"backend_unavailable:{e.kind}"
        state["retry_count"] += 1

    return state


def fallback_node(state: SchedulingAgentState) -> SchedulingAgentState:
    """Provides a safe fallback if validation repeatedly fails."""
    req_id = state["request_id"]
    logger.error("[%s] Node: fallback — max retries exceeded", req_id)
    state["assessment"] = get_safe_fallback(req_id)
    return state


def route_after_validation(state: SchedulingAgentState) -> str:
    if state.get("assessment") is not None:
        return "end"
    if state["retry_count"] < 2:
        return "retry"
    return "fallback"


def route_after_context(state: SchedulingAgentState) -> str:
    if state.get("error"):
        return "fallback"
    return "analyze"


graph_builder = StateGraph(SchedulingAgentState)
graph_builder.add_node("retrieve_context", retrieve_context_node)
graph_builder.add_node("plan_scheduling", plan_scheduling_node)
graph_builder.add_node("validate_response", validate_response_node)
graph_builder.add_node("fallback", fallback_node)

graph_builder.set_entry_point("retrieve_context")
graph_builder.add_conditional_edges("retrieve_context", route_after_context, {"analyze": "plan_scheduling", "fallback": "fallback"})
graph_builder.add_edge("plan_scheduling", "validate_response")
graph_builder.add_conditional_edges("validate_response", route_after_validation, {
    "end": END,
    "retry": "plan_scheduling",
    "fallback": "fallback",
})
graph_builder.add_edge("fallback", END)

scheduling_agent_app = graph_builder.compile()
