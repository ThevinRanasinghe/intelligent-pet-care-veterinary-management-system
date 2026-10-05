"""Backend HTTP client: timeouts and failure-status mapping.

Every backend failure must surface as BackendApiError with a stable kind —
never as empty/fabricated data.
"""
import httpx
import pytest
import respx

from shared.backend import BackendApiError, backend_get, backend_post

BASE = "http://petcare.test/api"


@pytest.mark.asyncio
async def test_401_maps_to_unauthorized():
    with respx.mock(base_url=BASE) as mock:
        mock.get("/consultations/abc").respond(401)
        with pytest.raises(BackendApiError) as exc:
            await backend_get("/consultations/abc", "Bearer token")
        assert exc.value.kind == "unauthorized"


@pytest.mark.asyncio
async def test_403_maps_to_forbidden():
    with respx.mock(base_url=BASE) as mock:
        mock.get("/medicines").respond(403)
        with pytest.raises(BackendApiError) as exc:
            await backend_get("/medicines")
        assert exc.value.kind == "forbidden"


@pytest.mark.asyncio
async def test_404_maps_to_not_found():
    with respx.mock(base_url=BASE) as mock:
        mock.get("/examinations/nope").respond(404)
        with pytest.raises(BackendApiError) as exc:
            await backend_get("/examinations/nope")
        assert exc.value.kind == "not_found"


@pytest.mark.asyncio
async def test_409_maps_to_conflict():
    with respx.mock(base_url=BASE) as mock:
        mock.post("/appointments/check-conflict").respond(409)
        with pytest.raises(BackendApiError) as exc:
            await backend_post("/appointments/check-conflict", {})
        assert exc.value.kind == "conflict"


@pytest.mark.asyncio
async def test_500_maps_to_unavailable():
    with respx.mock(base_url=BASE) as mock:
        mock.get("/medicines").respond(500)
        with pytest.raises(BackendApiError) as exc:
            await backend_get("/medicines")
        assert exc.value.kind == "unavailable"


@pytest.mark.asyncio
async def test_timeout_maps_to_timeout():
    with respx.mock(base_url=BASE) as mock:
        mock.get("/medicines").mock(side_effect=httpx.TimeoutException("slow"))
        with pytest.raises(BackendApiError) as exc:
            await backend_get("/medicines")
        assert exc.value.kind == "timeout"


@pytest.mark.asyncio
async def test_connection_failure_maps_to_unavailable():
    with respx.mock(base_url=BASE) as mock:
        mock.get("/medicines").mock(side_effect=httpx.ConnectError("refused"))
        with pytest.raises(BackendApiError) as exc:
            await backend_get("/medicines")
        assert exc.value.kind == "unavailable"


@pytest.mark.asyncio
async def test_bearer_token_forwarded():
    with respx.mock(base_url=BASE) as mock:
        route = mock.get("/consultations/abc").respond(200, json={"id": "abc"})
        await backend_get("/consultations/abc", "Bearer real-token")
        assert route.calls[0].request.headers["Authorization"] == "Bearer real-token"


@pytest.mark.asyncio
async def test_success_returns_json():
    with respx.mock(base_url=BASE) as mock:
        mock.get("/examinations/e1").respond(200, json={"id": "e1", "symptoms": "x"})
        assert (await backend_get("/examinations/e1"))["id"] == "e1"
