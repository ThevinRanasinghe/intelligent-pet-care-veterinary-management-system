"""Read-only backend data access for the Diagnosis Analysis Agent."""
import logging
from typing import Optional

from pydantic import BaseModel

from shared.backend import BackendApiError, backend_get
from shared.tool_registry import IdStr, registered_tool

logger = logging.getLogger(__name__)

_AGENT = "diagnosis_agent"


class _ExaminationIdInput(BaseModel):
    examination_id: IdStr


class _PetIdInput(BaseModel):
    pet_id: IdStr


@registered_tool(_AGENT, _ExaminationIdInput)
async def fetch_examination_details(examination_id: str, auth_token: Optional[str] = None) -> dict:
    """Reads the examination from the ASP.NET Core backend.

    Raises BackendApiError on any failure (401/403/404/5xx/timeout).
    """
    return await backend_get(f"/examinations/{examination_id}", auth_token)


@registered_tool(_AGENT, _PetIdInput)
async def fetch_pet_medical_history(pet_id: str, auth_token: Optional[str] = None) -> list:
    """Reads the pet's prior examinations.

    A 404 means no history — a legitimate empty result. Other failures
    raise so the agent falls back rather than analysing partial context.
    """
    try:
        return await backend_get(f"/examinations/pet/{pet_id}", auth_token)
    except BackendApiError as e:
        if e.kind == "not_found":
            return []
        raise
