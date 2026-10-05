"""Allow-list registry: validation, isolation, and tracing."""
import pytest

from shared.tool_registry import (
    TOOL_ALLOW_LIST,
    ToolCallRecord,
    ToolNotAllowedError,
    ToolValidationError,
    get_tool_trace,
    registered_tool,
    workflow_tool_context,
)
from pydantic import BaseModel, Field

from consultation_agent.tools import (
    fetch_consultation_details,
    fetch_previous_history,
)
from diagnosis_agent.tools import fetch_examination_details
from scheduling_agent.tools import check_conflict


class _DummyInput(BaseModel):
    item_id: str = Field(min_length=1, max_length=64)


@registered_tool("consultation_agent", _DummyInput)
async def _dummy_fetch(item_id: str, auth_token=None):
    return {"id": item_id}


def test_registered_tools_populate_allow_list():
    assert "fetch_consultation_details" in TOOL_ALLOW_LIST["consultation_agent"]
    assert "fetch_previous_history" in TOOL_ALLOW_LIST["consultation_agent"]
    assert "fetch_examination_details" in TOOL_ALLOW_LIST["diagnosis_agent"]
    assert "check_conflict" in TOOL_ALLOW_LIST["scheduling_agent"]
    assert "fetch_all_medicines" in TOOL_ALLOW_LIST["inventory_agent"]


@pytest.mark.asyncio
async def test_tool_runs_without_context_no_agent_restriction(monkeypatch):
    calls = []

    async def fake_get(path, auth_token=None, params=None):
        calls.append(path)
        return {"ok": True}

    import consultation_agent.tools as tools

    monkeypatch.setattr(tools, "backend_get", fake_get)
    result = await fetch_consultation_details("c-1", "Bearer x")
    assert result == {"ok": True}
    assert calls == ["/consultations/c-1"]


@pytest.mark.asyncio
async def test_tool_in_matching_context_traced(monkeypatch):
    async def fake_get(path, auth_token=None, params=None):
        return {"ok": True}

    import consultation_agent.tools as tools

    monkeypatch.setattr(tools, "backend_get", fake_get)

    with workflow_tool_context("consultation_agent") as trace:
        await fetch_consultation_details("c-2")
        await fetch_previous_history("PET-1")

    assert [t.tool for t in trace] == [
        "fetch_consultation_details",
        "fetch_previous_history",
    ]
    assert all(t.ok and t.agent == "consultation_agent" for t in trace)


@pytest.mark.asyncio
async def test_tool_from_other_agent_blocked_in_context(monkeypatch):
    with workflow_tool_context("diagnosis_agent"):
        with pytest.raises(ToolNotAllowedError):
            await fetch_consultation_details("c-3")

    trace = get_tool_trace()
    assert trace == []  # context exited


@pytest.mark.asyncio
async def test_invalid_input_rejected_before_call():
    with pytest.raises(ToolValidationError):
        await _dummy_fetch("")  # empty id fails IdStr-ish model

    class _IdInput(BaseModel):
        item_id: str = Field(min_length=1, max_length=64)

    @registered_tool("consultation_agent", _IdInput)
    async def _needs_id(item_id: str, auth_token=None):
        return {"id": item_id}

    with pytest.raises(ToolValidationError):
        await _needs_id("x" * 65)


@pytest.mark.asyncio
async def test_date_time_input_validation(monkeypatch):
    async def fake_post(path, body, auth_token=None):
        return True

    import scheduling_agent.tools as tools

    monkeypatch.setattr(tools, "backend_post", fake_post)

    with pytest.raises(ToolValidationError):
        await check_conflict("v1", "2026-13-40", "09:00", "10:00")
    with pytest.raises(ToolValidationError):
        await check_conflict("v1", "2026-01-01", "9:60", "10:00")
