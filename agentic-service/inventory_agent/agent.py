import logging
import json
from datetime import date, datetime

from langgraph.graph import StateGraph, END
from langchain_core.messages import HumanMessage, SystemMessage
from pydantic import ValidationError

from shared.backend import BackendApiError
from shared.config import ConfigurationError
from shared.llm import content_to_text, extract_json_object, get_llm

from .models import InventoryAgentState, InventoryAssessment, get_safe_fallback
from .tools import (
    fetch_all_medicines,
    fetch_low_stock_medicines,
    fetch_medicine_batches,
    fetch_prescription_items,
    fetch_treatment_record,
    get_available_quantity,
)

logger = logging.getLogger("InventoryAgent")

_NEAR_EXPIRY_DAYS = 30


async def retrieve_context_node(state: InventoryAgentState) -> InventoryAgentState:
    """Gathers required read-only data from the backend.

    The input is a TreatmentRecordId — in the PetCare workflow a
    "medicine request" is the batch of Prescription rows created under a
    treatment record, so the requested items come from
    GET /prescriptions/treatment/{id}.
    """
    req_id = state["treatment_record_id"]
    logger.info("[%s] Node: retrieve_context", req_id)

    try:
        auth_token = state.get("auth_token")

        treatment_record = await fetch_treatment_record(req_id, auth_token)
        prescription_items = await fetch_prescription_items(req_id, auth_token)
        medicines = await fetch_all_medicines(auth_token)

        # Real batch data for only the medicines actually requested
        # (medicine requests are capped at 10 items, so this is bounded).
        requested_ids = {
            item.get("medicineId") for item in prescription_items if item.get("medicineId")
        }
        batches = {}
        for medicine_id in requested_ids:
            batches[medicine_id] = await fetch_medicine_batches(medicine_id, auth_token)

        low_stock = await fetch_low_stock_medicines(auth_token)

        state["raw_input_data"] = {
            "treatment_record": treatment_record,
            "requested_items": prescription_items,
            "medicines": medicines,
            "batches": batches,
            "low_stock_medicines": low_stock,
        }
    except BackendApiError as e:
        logger.error("[%s] Backend context retrieval failed (%s)", req_id, e.kind)
        state["error"] = f"backend_unavailable:{e.kind}"
    except Exception:
        logger.exception("[%s] Unexpected context retrieval failure", req_id)
        state["error"] = "context_retrieval_failed"

    return state


async def plan_inventory_node(state: InventoryAgentState) -> InventoryAgentState:
    """Prompts the LLM to generate the inventory proposal."""
    req_id = state["treatment_record_id"]
    logger.info("[%s] Node: plan_inventory (Retry: %d)", req_id, state["retry_count"])

    sys_prompt = """You are an Inventory & Medicine Management Agent for a Veterinary clinic.
    Your ONLY job is to analyze the treatment record, the requested medicine items, the
    medicine catalog, and the real stock/batch data provided, then produce a structured
    recommendation for the Inventory Officer.
    DO NOT independently modify medicine or inventory records. This is an advisory layer.

    CRITICAL RULES:
    - Only recommend a medicineId that exists in the provided "medicines" catalog.
    - Only recommend a batchId that exists in the provided "batches" data for that medicine.
    - NEVER invent a batch number, expiry date, or stock quantity. If no suitable batch
      exists in the provided data, set "recommendedBatch" to null.
    - Prefer batches that are not expired and expire furthest in the future.

    IMPORTANT: You must return ONLY a raw JSON object. Do not include markdown code blocks like ```json. Do not include any conversational text before or after the JSON.

    Output EXACTLY this JSON structure:
    {
        "requestId": "string",
        "medicineRecommendation": {
            "medicineId": "string",
            "medicineName": "string",
            "requiredQuantity": 10,
            "availableQuantity": 25,
            "sufficientStock": true,
            "reason": "string"
        } | null,
        "recommendedBatch": {
            "batchId": "string",
            "batchNumber": "string",
            "quantityAvailable": 25,
            "expiryDate": "YYYY-MM-DD",
            "expiryStatus": "Valid" | "Near Expiry" | "Expired"
        } | null,
        "alternativeMedicines": [],
        "inventorySummary": {
            "medicineFound": true,
            "stockAvailable": true,
            "sufficientQuantity": true,
            "batchAvailable": true,
            "notExpired": true,
            "lowStock": false
        },
        "confidence": "Low" | "Moderate" | "High",
        "planningNotes": "string",
        "disclaimer": "AI-generated medicine and inventory recommendation - deterministic backend validation and authorized staff review are required before any inventory action."
    }"""

    if state["error"] and state["retry_count"] > 0:
        sys_prompt += f"\n\nPREVIOUS ERROR (Fix this in your JSON output): {state['error']}"

    human_msg = f"Data to Analyze: {json.dumps(state['raw_input_data'], default=str)}"

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


def _expiry_status(batch: dict) -> str:
    """Derives expiry status from the real batch record — never from the LLM."""
    if batch.get("isExpired"):
        return "Expired"
    expiry = batch.get("expiryDate")
    try:
        expiry_date = datetime.strptime(str(expiry)[:10], "%Y-%m-%d").date()
    except (ValueError, TypeError):
        return "Valid"
    return "Near Expiry" if (expiry_date - date.today()).days <= _NEAR_EXPIRY_DAYS else "Valid"


async def validate_response_node(state: InventoryAgentState) -> InventoryAgentState:
    """Validates the LLM output and deterministically overwrites stock/batch facts."""
    req_id = state["treatment_record_id"]
    logger.info("[%s] Node: validate_response", req_id)

    if state["error"]:
        return state

    try:
        raw_json = json.loads(state["llm_response"])
        raw_json["requestId"] = req_id

        parsed = InventoryAssessment(**raw_json)

        medicines = state["raw_input_data"].get("medicines", [])
        batches_by_medicine = state["raw_input_data"].get("batches", {})

        if parsed.medicineRecommendation:
            med_id = parsed.medicineRecommendation.medicineId
            med = next((m for m in medicines if m.get("id") == med_id), None)
            if med is None:
                parsed.inventorySummary.medicineFound = False
                raise ValueError(f"Medicine ID {med_id} not found in catalog.")

            req_qty = parsed.medicineRecommendation.requiredQuantity
            # availableQuantity (total - reserved) is the real stock figure;
            # overwrite whatever the LLM produced.
            available = get_available_quantity(medicines, med_id)
            parsed.medicineRecommendation.availableQuantity = available
            parsed.medicineRecommendation.sufficientStock = available >= req_qty
            parsed.medicineRecommendation.medicineName = med.get("name") or parsed.medicineRecommendation.medicineName
            parsed.inventorySummary.sufficientQuantity = available >= req_qty
            parsed.inventorySummary.stockAvailable = available > 0
            parsed.inventorySummary.lowStock = bool(med.get("isLowStock"))

            real_batches = batches_by_medicine.get(med_id, [])
            parsed.inventorySummary.batchAvailable = any(
                not b.get("isExpired") and int(b.get("quantity") or 0) > 0 for b in real_batches
            )

            if parsed.recommendedBatch:
                batch = next(
                    (b for b in real_batches if b.get("id") == parsed.recommendedBatch.batchId),
                    None,
                )
                if batch is None:
                    parsed.inventorySummary.batchAvailable = any(
                        not b.get("isExpired") for b in real_batches
                    )
                    raise ValueError("Recommended batch does not exist in real batch data.")
                # Overwrite all batch facts from the real record.
                parsed.recommendedBatch.batchNumber = batch.get("batchNumber", "")
                parsed.recommendedBatch.quantityAvailable = int(batch.get("quantity") or 0)
                parsed.recommendedBatch.expiryDate = str(batch.get("expiryDate", ""))[:10]
                parsed.recommendedBatch.expiryStatus = _expiry_status(batch)
                parsed.inventorySummary.notExpired = not batch.get("isExpired", False)
                if batch.get("isExpired"):
                    raise ValueError("Recommended batch is expired.")
            else:
                parsed.inventorySummary.notExpired = parsed.inventorySummary.batchAvailable

        state["assessment"] = parsed
        logger.info("[%s] Validation passed", req_id)
    except (json.JSONDecodeError, ValidationError, ValueError) as e:
        logger.warning("[%s] Validation failed: %s", req_id, type(e).__name__)
        state["error"] = f"validation_error:{type(e).__name__}"
        state["retry_count"] += 1
    except BackendApiError as e:
        logger.warning("[%s] Backend validation call failed (%s)", req_id, e.kind)
        state["error"] = f"backend_unavailable:{e.kind}"
        state["retry_count"] += 1

    return state


def fallback_node(state: InventoryAgentState) -> InventoryAgentState:
    req_id = state["treatment_record_id"]
    logger.error("[%s] Node: fallback — max retries exceeded", req_id)
    state["assessment"] = get_safe_fallback(req_id)
    return state


def route_after_validation(state: InventoryAgentState) -> str:
    if state.get("assessment") is not None:
        return "end"
    if state["retry_count"] < 2:
        return "retry"
    return "fallback"


def route_after_context(state: InventoryAgentState) -> str:
    if state.get("error"):
        return "fallback"
    return "analyze"


graph_builder = StateGraph(InventoryAgentState)
graph_builder.add_node("retrieve_context", retrieve_context_node)
graph_builder.add_node("plan_inventory", plan_inventory_node)
graph_builder.add_node("validate_response", validate_response_node)
graph_builder.add_node("fallback", fallback_node)

graph_builder.set_entry_point("retrieve_context")
graph_builder.add_conditional_edges("retrieve_context", route_after_context, {"analyze": "plan_inventory", "fallback": "fallback"})
graph_builder.add_edge("plan_inventory", "validate_response")
graph_builder.add_conditional_edges("validate_response", route_after_validation, {
    "end": END,
    "retry": "plan_inventory",
    "fallback": "fallback",
})
graph_builder.add_edge("fallback", END)

inventory_agent_app = graph_builder.compile()
