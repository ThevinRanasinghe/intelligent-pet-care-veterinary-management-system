"""HTTP client for Agentic Service -> PetCare ASP.NET Core API calls.

Every call has an explicit timeout and maps backend failures to typed
errors. Callers must never treat a failed call as a successful read —
a 401/403/404/409/5xx/timeout raises BackendApiError instead of
returning empty or fabricated data.
"""
import logging
from typing import Any, Optional

import httpx

from .config import get_api_base_url, get_backend_timeout_seconds

logger = logging.getLogger("BackendClient")


class BackendApiError(Exception):
    """A call to the PetCare backend failed.

    kind is a stable machine-readable category:
    unauthorized | forbidden | not_found | conflict | unavailable | timeout
    """

    def __init__(self, message: str, status_code: Optional[int] = None, kind: str = "unavailable"):
        super().__init__(message)
        self.status_code = status_code
        self.kind = kind


def _headers(auth_token: Optional[str]) -> dict:
    return {"Authorization": auth_token} if auth_token else {}


def _raise_for_status(response: httpx.Response, context: str) -> None:
    code = response.status_code
    if code < 400:
        return
    if code == 401:
        raise BackendApiError(f"{context}: backend rejected the caller token (401)", code, "unauthorized")
    if code == 403:
        raise BackendApiError(f"{context}: caller lacks permission (403)", code, "forbidden")
    if code == 404:
        raise BackendApiError(f"{context}: resource not found (404)", code, "not_found")
    if code == 409:
        raise BackendApiError(f"{context}: backend reported a conflict (409)", code, "conflict")
    raise BackendApiError(f"{context}: backend error ({code})", code, "unavailable")


async def backend_get(path: str, auth_token: Optional[str] = None, params: Optional[dict] = None) -> Any:
    """GET {API_BASE_URL}{path}. Raises BackendApiError on any failure."""
    base = get_api_base_url()
    try:
        async with httpx.AsyncClient(base_url=base, timeout=get_backend_timeout_seconds()) as client:
            response = await client.get(path, params=params, headers=_headers(auth_token))
    except httpx.TimeoutException:
        logger.warning("backend GET %s timed out", path)
        raise BackendApiError(f"GET {path}: backend request timed out", kind="timeout")
    except httpx.HTTPError:
        logger.warning("backend GET %s connection failed", path)
        raise BackendApiError(f"GET {path}: backend unreachable", kind="unavailable")

    _raise_for_status(response, f"GET {path}")
    return response.json()


async def backend_post(path: str, body: dict, auth_token: Optional[str] = None) -> Any:
    """POST {API_BASE_URL}{path}. Raises BackendApiError on any failure."""
    try:
        async with httpx.AsyncClient(base_url=get_api_base_url(), timeout=get_backend_timeout_seconds()) as client:
            response = await client.post(path, json=body, headers=_headers(auth_token))
    except httpx.TimeoutException:
        logger.warning("backend POST %s timed out", path)
        raise BackendApiError(f"POST {path}: backend request timed out", kind="timeout")
    except httpx.HTTPError:
        logger.warning("backend POST %s connection failed", path)
        raise BackendApiError(f"POST {path}: backend unreachable", kind="unavailable")

    _raise_for_status(response, f"POST {path}")
    return response.json()
