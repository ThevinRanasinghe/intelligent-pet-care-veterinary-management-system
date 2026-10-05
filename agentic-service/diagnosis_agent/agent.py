import logging
import json

from langgraph.graph import StateGraph, END
from langchain_core.messages import HumanMessage, SystemMessage
from pydantic import ValidationError

from shared.backend import BackendApiError
from shared.config import ConfigurationError
from shared.llm import content_to_text, extract_json_object, get_llm, is_non_retryable_error
from shared.sanitize import sanitize_input_data

from .models import DiagnosisAgentState, DiagnosisAssessment, get_safe_fallback
from .tools import fetch_examination_details, fetch_pet_medical_history

logger = logging.getLogger("DiagnosisAgent")


async def retrieve_context_node(state: DiagnosisAgentState) -> DiagnosisAgentState:
    """Gathers required read-only data from the backend."""
    req_id = state["examination_id"]
    logger.info("[%s] Node: retrieve_context", req_id)

    try:
        auth_token = state.get("auth_token")
        examination = await fetch_examination_details(req_id, auth_token)

        pet_id = examination.get("petId")
        history = await fetch_pet_medical_history(pet_id, auth_token) if pet_id else []

        state["raw_input_data"] = {
            "examination": examination,
            "history": history,
        }
    except BackendApiError as e:
        logger.error("[%s] Backend context retrieval failed (%s)", req_id, e.kind)
        state["error"] = f"backend_unavailable:{e.kind}"
    except Exception:
        logger.exception("[%s] Unexpected context retrieval failure", req_id)
        state["error"] = "context_retrieval_failed"

    return state


async def analyze_request_node(state: DiagnosisAgentState) -> DiagnosisAgentState:
    """Prompts the LLM to generate the assessment."""
    req_id = state["examination_id"]
    logger.info("[%s] Node: analyze_request (Retry: %d)", req_id, state["retry_count"])

    sys_prompt = """You are a Diagnosis Analysis Agent for a Veterinary clinic.
    Your ONLY job is to analyze the provided examination symptoms, notes, and medical history.
    Provide a preliminary structured list of likely conditions and recommended next steps.
    DO NOT provide a final diagnosis, and DO NOT write to databases or prescribe medicine.

    IMPORTANT: You must return ONLY a raw JSON object. Do not include markdown code blocks like ```json. Do not include any conversational text before or after the JSON.

    Output EXACTLY this JSON structure:
    {
        "suspectedCondition": "string",
        "recommendedSeverity": "Low" | "Moderate" | "High" | "Critical",
        "rationale": "string",
        "recommendedProcedures": ["string", "string"],
        "suggestedMedicines": [
            {
                "medicineName": "string",
                "suggestedDosage": "string",
                "suggestedDurationDays": 5
            }
        ],
        "precautionaryNotes": ["string"]
    }"""

    if state["error"] and state["retry_count"] > 0:
        sys_prompt += f"\n\nPREVIOUS ERROR (Fix this in your JSON output): {state['error']}"

    human_msg = f"Examination Data to Analyze: {json.dumps(sanitize_input_data(state['raw_input_data']))}"

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


def validate_response_node(state: DiagnosisAgentState) -> DiagnosisAgentState:
    """Validates the LLM JSON output against the Pydantic schema."""
    req_id = state["examination_id"]
    logger.info("[%s] Node: validate_response", req_id)

    if state["error"]:
        return state

    try:
        raw_json = json.loads(state["llm_response"])
        parsed = DiagnosisAssessment(**raw_json)
        state["assessment"] = parsed
        logger.info("[%s] Validation passed", req_id)
    except (json.JSONDecodeError, ValidationError) as e:
        logger.warning("[%s] Validation failed: %s", req_id, type(e).__name__)
        state["error"] = f"validation_error:{type(e).__name__}"
        state["retry_count"] += 1

    return state


def fallback_node(state: DiagnosisAgentState) -> DiagnosisAgentState:
    """Provides a safe fallback if validation repeatedly fails."""
    req_id = state["examination_id"]
    logger.error("[%s] Node: fallback — max retries exceeded", req_id)
    state["assessment"] = get_safe_fallback(req_id)
    return state


def route_after_validation(state: DiagnosisAgentState) -> str:
    if state.get("assessment") is not None:
        return "end"
    if is_non_retryable_error(state.get("error")):
        return "fallback"
    if state["retry_count"] < 2:
        return "retry"
    return "fallback"


def route_after_context(state: DiagnosisAgentState) -> str:
    if state.get("error"):
        return "fallback"
    return "analyze"


graph_builder = StateGraph(DiagnosisAgentState)
graph_builder.add_node("retrieve_context", retrieve_context_node)
graph_builder.add_node("analyze_request", analyze_request_node)
graph_builder.add_node("validate_response", validate_response_node)
graph_builder.add_node("fallback", fallback_node)

graph_builder.set_entry_point("retrieve_context")
graph_builder.add_conditional_edges("retrieve_context", route_after_context, {"analyze": "analyze_request", "fallback": "fallback"})
graph_builder.add_edge("analyze_request", "validate_response")
graph_builder.add_conditional_edges("validate_response", route_after_validation, {
    "end": END,
    "retry": "analyze_request",
    "fallback": "fallback",
})
graph_builder.add_edge("fallback", END)

diagnosis_agent_app = graph_builder.compile()
