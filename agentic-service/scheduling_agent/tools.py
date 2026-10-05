"""Read-only backend data access for the Scheduling & Quotation Agent."""
import logging
from typing import Optional

from pydantic import BaseModel

from shared.backend import backend_get, backend_post
from shared.tool_registry import DateStr, IdStr, TimeStr, registered_tool

logger = logging.getLogger(__name__)

_AGENT = "scheduling_agent"


class _RequestIdInput(BaseModel):
    request_id: IdStr


class _AvailableSlotsInput(BaseModel):
    veterinarian_id: Optional[IdStr] = None
    date: Optional[DateStr] = None


class _CheckConflictInput(BaseModel):
    veterinarian_id: IdStr
    date: DateStr
    start_time: TimeStr
    end_time: TimeStr


@registered_tool(_AGENT, _RequestIdInput)
async def fetch_consultation_request(request_id: str, auth_token: Optional[str] = None) -> dict:
    """Reads the consultation request. Raises BackendApiError on failure."""
    return await backend_get(f"/consultations/{request_id}", auth_token)


@registered_tool(_AGENT, _AvailableSlotsInput)
async def fetch_available_slots(
    auth_token: Optional[str] = None,
    veterinarian_id: Optional[str] = None,
    date: Optional[str] = None,
) -> list:
    """Reads available appointment slots, filtered when possible.

    veterinarian_id / date map to the backend's optional query parameters
    (GET /appointments/available-slots?veterinarianId=&date=). Filtering
    keeps the candidate set small instead of pulling every slot in the
    organisation. Raises BackendApiError on failure — the agent must not
    plan against a silently-empty slot list.
    """
    params = {}
    if veterinarian_id:
        params["veterinarianId"] = veterinarian_id
    if date:
        params["date"] = date
    return await backend_get("/appointments/available-slots", auth_token, params=params or None)


@registered_tool(_AGENT, _CheckConflictInput)
async def check_conflict(
    veterinarian_id: str,
    date: str,
    start_time: str,
    end_time: str,
    auth_token: Optional[str] = None,
) -> bool:
    """Deterministic overlap check via the backend. Returns True = conflict.

    Raises BackendApiError on failure: a failed conflict check must never
    be treated as "no conflict".
    """
    payload = {
        "veterinarianId": veterinarian_id,
        "scheduledStart": f"{date}T{start_time}:00",
        "scheduledEnd": f"{date}T{end_time}:00",
    }
    result = await backend_post("/appointments/check-conflict", payload, auth_token)
    return bool(result)
