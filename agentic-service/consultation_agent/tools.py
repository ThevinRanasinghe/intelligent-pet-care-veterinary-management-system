"""Read-only backend data access for the Consultation Analysis Agent."""
import logging
from typing import Optional

from pydantic import BaseModel

from shared.backend import BackendApiError, backend_get
from shared.tool_registry import IdStr, registered_tool

logger = logging.getLogger(__name__)

_AGENT = "consultation_agent"


class _ConsultationIdInput(BaseModel):
    consultation_id: IdStr


class _PetIdInput(BaseModel):
    pet_id: IdStr


@registered_tool(_AGENT, _ConsultationIdInput)
async def fetch_consultation_details(consultation_id: str, auth_token: Optional[str] = None) -> dict:
    """Reads the consultation request from the ASP.NET Core backend.

    Raises BackendApiError on any failure (401/403/404/5xx/timeout).
    """
    return await backend_get(f"/consultations/{consultation_id}", auth_token)


@registered_tool(_AGENT, _PetIdInput)
async def fetch_previous_history(pet_id: str, auth_token: Optional[str] = None) -> list:
    """Reads the pet's previous examination history.

    A 404 means there is no history — that is a legitimate empty result.
    Auth/backend failures raise BackendApiError so the caller falls back
    instead of analysing against silently-missing history.
    """
    try:
        return await backend_get(f"/examinations/pet/{pet_id}", auth_token)
    except BackendApiError as e:
        if e.kind == "not_found":
            return []
        raise
