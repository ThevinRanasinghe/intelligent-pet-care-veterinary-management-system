import logging
import json

from langgraph.graph import StateGraph, END
from langchain_core.messages import HumanMessage, SystemMessage
from pydantic import ValidationError

from shared.backend import BackendApiError
from shared.config import ConfigurationError
from shared.llm import content_to_text, extract_json_object, get_llm, is_non_retryable_error
from shared.sanitize import sanitize_input_data

from .models import (
    AppointmentRecommendation,
    SchedulingAgentState,
    SchedulingAssessment,
    ValidationSummary,
    get_safe_fallback,
)
from .slot_search import find_appointment_window, parse_preferred, resolve_required_slots
from .tools import fetch_consultation_request, fetch_available_slots, check_conflict

logger = logging.getLogger("SchedulingAgent")


async def retrieve_context_node(state: SchedulingAgentState) -> SchedulingAgentState:
    """Gathers required read-only data from the backend."""
    req_id = state["request_id"]
    logger.info("[%s] Node: retrieve_context", req_id)

    try:
        auth_token = state.get("auth_token")

        consultation = await fetch_consultation_request(req_id, auth_token)

        # The full availability inventory is fetched once — the
        # deterministic selector then applies the preferred-date-first
        # search order and the future-date range itself.
        slots = await fetch_available_slots(
            auth_token,
            veterinarian_id=consultation.get("requestedByVeterinarianId"),
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


async def select_window_node(state: SchedulingAgentState) -> SchedulingAgentState:
    """Deterministic slot search — code, not the LLM, picks the window.

    Search order: exact preferred time → nearest window on the preferred
    date → nearest future date (bounded). Only real Available
    AppointmentSlot rows are eligible; nothing is invented.
    """
    req_id = state["request_id"]
    logger.info("[%s] Node: select_window", req_id)

    if state.get("error"):
        return state

    consultation = state["raw_input_data"].get("consultation") or {}
    slots = state["raw_input_data"].get("available_slots") or []

    preferred_date, preferred_time = parse_preferred(consultation.get("preferredDate"))

    required, minutes, source = resolve_required_slots(
        state.get("consultation_assessment")
    )

    state["input_summary"] = {
        "requestId": req_id,
        "preferredDate": preferred_date,
        "preferredTime": preferred_time,
        "requiredSlots": required,
        "estimatedDurationMinutes": minutes,
        "requiredSlotsSource": source,
        "preferredBranch": consultation.get("preferredBranch"),
        "requestedVeterinarianId": consultation.get("requestedByVeterinarianId"),
        "slotsConsidered": len(slots),
    }

    if source == "invalid" or source == "exceeds_max":
        state["slot_selection"] = {
            "found": False,
            "reasonCode": "INVALID_DURATION",
            "reason": (
                "Consultation assessment produced an unsupported slot/"
                "duration requirement; rejected deterministically."
            ),
            "requiredSlots": required,
            "requiredSlotsSource": source,
            "requestedDate": preferred_date,
            "requestedTime": preferred_time,
        }
        return state

    selection = find_appointment_window(
        slots=slots,
        preferred_date=preferred_date,
        preferred_time=preferred_time,
        required_slots=required,
        veterinarian_id=consultation.get("requestedByVeterinarianId"),
        branch=consultation.get("preferredBranch"),
    )
    state["slot_selection"] = selection.to_dict()
    return state


async def plan_scheduling_node(state: SchedulingAgentState) -> SchedulingAgentState:
    """Prompts the LLM for the quotation and planning rationale.

    The appointment window itself is already fixed by
    ``select_window_node`` — the LLM reasons about pricing and notes but
    its slot choices are overwritten by the deterministic selection.
    """
    req_id = state["request_id"]
    logger.info("[%s] Node: plan_scheduling (Retry: %d)", req_id, state["retry_count"])

    sys_prompt = """You are a Scheduling & Quotation Planning Agent for a Veterinary clinic.
    The deterministic scheduler has ALREADY selected the appointment window
    (see "slotSelection" in the data) — do NOT choose or invent slots.
    Your job: produce a quotation for the visit and a short planning note.

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
    }

    When slotSelection.found is false, set recommendedAppointment to null —
    the workflow will fail safely. When it is true, copy the slotSelection
    fields into recommendedAppointment verbatim."""

    if state["error"] and state["retry_count"] > 0:
        sys_prompt += f"\n\nPREVIOUS ERROR (Fix this in your JSON output): {state['error']}"

    human_msg = f"Data to Analyze: {json.dumps(sanitize_input_data(state['raw_input_data']))}\n\nDeterministic slot selection: {json.dumps(state.get('slot_selection'))}"

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
    """Validates the LLM JSON and enforces the deterministic selection."""
    req_id = state["request_id"]
    logger.info("[%s] Node: validate_response", req_id)

    if state["error"]:
        return state

    try:
        raw_json = json.loads(state["llm_response"])
        raw_json["requestId"] = req_id  # Enforce ID correctness

        parsed = SchedulingAssessment(**raw_json)
        selection = state.get("slot_selection") or {}
        parsed.searchDiagnostics = selection
        parsed.reasonCode = selection.get("reasonCode", "OK")

        # The appointment window is authoritative code output — the LLM's
        # copy is discarded and rebuilt so a hallucinated slot can never
        # survive validation.
        if selection.get("found"):
            reason = (
                (parsed.recommendedAppointment.reason if parsed.recommendedAppointment else None)
                or selection.get("reason")
                or "Deterministic scheduler selected this window."
            )
            parsed.recommendedAppointment = AppointmentRecommendation(
                appointmentSlotId=selection["slotIds"][0],
                veterinarianId=selection["veterinarianId"],
                date=selection["date"],
                startTime=selection["startTime"],
                endTime=selection["endTime"],
                branch=selection.get("branch") or "",
                reason=reason,
                slotIds=list(selection["slotIds"]),
                slotCount=int(selection["slotCount"]),
                requiredSlots=int(selection["requiredSlots"]),
                estimatedDurationMinutes=int(selection["estimatedDurationMinutes"]),
                usedPreferredTime=bool(selection.get("usedPreferredTime")),
                usedPreferredDate=bool(selection.get("usedPreferredDate")),
                fallbackUsed=bool(selection.get("fallbackUsed")),
                fallbackType=selection.get("fallbackType", "none"),
            )
            parsed.validationSummary.slotFound = True
            parsed.validationSummary.veterinarianAvailable = True
            parsed.validationSummary.withinRequestedTime = bool(
                selection.get("usedPreferredTime")
            )

            # Conflict check over the WHOLE proposed window — a failed
            # check raises BackendApiError and is treated as unsafe,
            # never as "no conflict".
            auth_token = state.get("auth_token")
            conflict = await check_conflict(
                parsed.recommendedAppointment.veterinarianId,
                parsed.recommendedAppointment.date,
                parsed.recommendedAppointment.startTime,
                parsed.recommendedAppointment.endTime,
                auth_token,
            )
            if conflict:
                parsed.validationSummary.noKnownConflict = False
                raise ValueError("Backend detected a scheduling conflict for the recommended appointment.")
            parsed.validationSummary.noKnownConflict = True
        else:
            # Deterministic search found nothing / invalid duration —
            # no proposal is the correct, safe outcome.
            parsed.recommendedAppointment = None
            parsed.alternativeSlots = []
            parsed.validationSummary.slotFound = False
            parsed.validationSummary.veterinarianAvailable = False
            parsed.validationSummary.noKnownConflict = False
            parsed.validationSummary.withinRequestedTime = False
            if selection.get("reason"):
                parsed.planningNotes = (
                    f"{parsed.planningNotes} | Slot search: {selection['reason']}"
                ).strip(" |")

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


def no_proposal_node(state: SchedulingAgentState) -> SchedulingAgentState:
    """Deterministic no-proposal path — no LLM call is needed (or safe)
    when the slot search already proved nothing is bookable."""
    req_id = state["request_id"]
    selection = state.get("slot_selection") or {}
    logger.info("[%s] Node: no_proposal (%s)", req_id, selection.get("reasonCode"))
    state["assessment"] = SchedulingAssessment(
        requestId=req_id,
        recommendedAppointment=None,
        alternativeSlots=[],
        quotationProposal=None,
        validationSummary=ValidationSummary(
            slotFound=False,
            veterinarianAvailable=False,
            noKnownConflict=False,
            withinRequestedTime=False,
            withinBudget=False,
        ),
        confidence="Low",
        planningNotes=selection.get("reason") or "No valid appointment window was found.",
        searchDiagnostics=selection,
        reasonCode=selection.get("reasonCode", "NO_VALID_SLOT"),
    )
    return state


def fallback_node(state: SchedulingAgentState) -> SchedulingAgentState:
    """Provides a safe fallback if validation repeatedly fails."""
    req_id = state["request_id"]
    logger.error("[%s] Node: fallback — max retries exceeded", req_id)
    reason_code = (state.get("slot_selection") or {}).get("reasonCode", "NO_VALID_SLOT")
    state["assessment"] = get_safe_fallback(req_id, reason_code)
    return state


def route_after_validation(state: SchedulingAgentState) -> str:
    if state.get("assessment") is not None:
        return "end"
    if is_non_retryable_error(state.get("error")):
        return "fallback"
    if state["retry_count"] < 2:
        return "retry"
    return "fallback"


def route_after_context(state: SchedulingAgentState) -> str:
    if state.get("error"):
        return "fallback"
    return "select_window"


def route_after_select_window(state: SchedulingAgentState) -> str:
    if state.get("error"):
        return "fallback"
    if not (state.get("slot_selection") or {}).get("found"):
        return "no_proposal"
    return "analyze"


graph_builder = StateGraph(SchedulingAgentState)
graph_builder.add_node("retrieve_context", retrieve_context_node)
graph_builder.add_node("select_window", select_window_node)
graph_builder.add_node("plan_scheduling", plan_scheduling_node)
graph_builder.add_node("validate_response", validate_response_node)
graph_builder.add_node("no_proposal", no_proposal_node)
graph_builder.add_node("fallback", fallback_node)

graph_builder.set_entry_point("retrieve_context")
graph_builder.add_conditional_edges("retrieve_context", route_after_context, {"select_window": "select_window", "fallback": "fallback"})
graph_builder.add_conditional_edges("select_window", route_after_select_window, {
    "analyze": "plan_scheduling",
    "no_proposal": "no_proposal",
    "fallback": "fallback",
})
graph_builder.add_edge("no_proposal", END)
graph_builder.add_edge("plan_scheduling", "validate_response")
graph_builder.add_conditional_edges("validate_response", route_after_validation, {
    "end": END,
    "retry": "plan_scheduling",
    "fallback": "fallback",
})
graph_builder.add_edge("fallback", END)

scheduling_agent_app = graph_builder.compile()
