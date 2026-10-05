"""Read-only backend data access for the Inventory & Medicine Agent.

All reads go through the existing PetCare API contracts:

- GET /treatmentrecords/{id}            — the treatment context
- GET /prescriptions/treatment/{id}     — the requested medicine items
- GET /medicines?page=&pageSize=        — paged catalog (looped to completion)
- GET /medicines/{id}/batches           — real batch/expiry data per medicine
- GET /medicines/low-stock              — medicines at/below reorder level

Nothing here writes to inventory. availableQuantity is the authoritative
stock figure (totalQuantity - reservedQuantity); the non-existent
"currentStock" field is never used.
"""
import logging
from typing import Any, Dict, List, Optional

from pydantic import BaseModel, Field

from shared.backend import BackendApiError, backend_get
from shared.tool_registry import IdStr, registered_tool

logger = logging.getLogger("InventoryAgentTools")

_AGENT = "inventory_agent"


class _TreatmentRecordIdInput(BaseModel):
    treatment_record_id: IdStr


class _MedicineIdInput(BaseModel):
    medicine_id: IdStr


class _AvailableQuantityInput(BaseModel):
    medicines: List[Dict[str, Any]]
    medicine_id: IdStr


class _StockCheckInput(BaseModel):
    medicines: List[Dict[str, Any]]
    medicine_id: IdStr
    required_quantity: int = Field(ge=0)

_MEDICINE_PAGE_SIZE = 200
_MAX_PAGES = 20  # hard cap: 4000 medicines — far beyond any realistic catalog


@registered_tool(_AGENT, _TreatmentRecordIdInput)
async def fetch_treatment_record(treatment_record_id: str, auth_token: Optional[str] = None) -> Dict[str, Any]:
    """Fetches the treatment record. Raises BackendApiError on failure."""
    return await backend_get(f"/treatmentrecords/{treatment_record_id}", auth_token)


@registered_tool(_AGENT, _TreatmentRecordIdInput)
async def fetch_prescription_items(treatment_record_id: str, auth_token: Optional[str] = None) -> List[Dict[str, Any]]:
    """Fetches the medicine-request items (prescriptions) for a treatment record.

    In the PetCare workflow a "medicine request" is the batch of
    Prescription rows sharing a TreatmentRecordId. A 404/empty result is
    legitimate (no items requested yet); other failures raise.
    """
    try:
        result = await backend_get(f"/prescriptions/treatment/{treatment_record_id}", auth_token)
    except BackendApiError as e:
        if e.kind == "not_found":
            return []
        raise
    return result if isinstance(result, list) else []


@registered_tool(_AGENT)
async def fetch_all_medicines(auth_token: Optional[str] = None) -> List[Dict[str, Any]]:
    """Fetches the full medicine catalog, following backend pagination.

    GET /medicines returns PagedResult { items, total, page, pageSize } —
    the default pageSize is 20, so we request a large page and keep going
    until every item is collected. Raises BackendApiError on failure.
    """
    items: List[Dict[str, Any]] = []
    page = 1
    while page <= _MAX_PAGES:
        data = await backend_get(
            "/medicines",
            auth_token,
            params={"page": page, "pageSize": _MEDICINE_PAGE_SIZE},
        )
        if not isinstance(data, dict):
            # Unpaged fallback: a bare list response.
            return data if isinstance(data, list) else items

        batch = data.get("items", [])
        items.extend(batch)
        total = data.get("total", len(items))
        if len(items) >= total or not batch:
            break
        page += 1
    return items


@registered_tool(_AGENT, _MedicineIdInput)
async def fetch_medicine_batches(medicine_id: str, auth_token: Optional[str] = None) -> List[Dict[str, Any]]:
    """Fetches real stock batches (batch number, quantity, expiry) for a medicine."""
    result = await backend_get(f"/medicines/{medicine_id}/batches", auth_token)
    return result if isinstance(result, list) else []


@registered_tool(_AGENT)
async def fetch_low_stock_medicines(auth_token: Optional[str] = None) -> List[Dict[str, Any]]:
    """Fetches medicines at/below reorder level. Advisory context only —
    a failure here degrades to empty context rather than failing the run,
    because the authoritative stock check uses the catalog anyway."""
    try:
        result = await backend_get("/medicines/low-stock", auth_token)
    except BackendApiError:
        logger.warning("low-stock endpoint unavailable; continuing without it")
        return []
    return result if isinstance(result, list) else []


@registered_tool(_AGENT, _AvailableQuantityInput)
def get_available_quantity(medicines: List[Dict[str, Any]], medicine_id: str) -> int:
    """Reads the real availableQuantity for a medicine id, 0 when unknown."""
    for med in medicines:
        if med.get("id") == medicine_id:
            return int(med.get("availableQuantity") or 0)
    return 0


@registered_tool(_AGENT, _StockCheckInput)
def check_stock_availability(
    medicines: List[Dict[str, Any]],
    medicine_id: str,
    required_quantity: int,
) -> bool:
    """Deterministic stock check against the fetched catalog."""
    return get_available_quantity(medicines, medicine_id) >= required_quantity
