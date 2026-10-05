"""Deterministic consultation -> slot-count mapping tests.

The LLM may reason about complexity, but the final ``requiredSlots`` /
``estimatedDurationMinutes`` are always produced by code. These tests
cover the mapping layer directly AND the real consultation agent graph
with a canned LLM response, so no real model call is ever needed.
"""
import json
import os
import sys

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import consultation_agent.agent as consultation
import shared.llm as shared_llm
from consultation_agent.duration_mapping import (
    SLOT_MINUTES,
    normalize_scheduling_need,
    scheduling_max_slots,
)


class _FakeResponse:
    def __init__(self, content):
        self.content = content


class _FakeLLM:
    def __init__(self, content):
        self._content = content

    async def ainvoke(self, messages):
        return _FakeResponse(self._content)


def _run_agent(monkeypatch, llm_json: dict):
    """Drives the REAL consultation agent graph with a canned LLM reply."""

    async def fake_consultation(cid, token=None):
        return {"id": cid, "petId": "p1", "symptoms": "owner text"}

    async def fake_history(pid, token=None):
        return []

    monkeypatch.setattr(consultation, "fetch_consultation_details", fake_consultation)
    monkeypatch.setattr(consultation, "fetch_previous_history", fake_history)
    monkeypatch.setattr(shared_llm, "_llm", _FakeLLM(json.dumps(llm_json)))
    return consultation.consultation_agent_app


def _state():
    return {
        "consultation_request_id": "c-duration",
        "auth_token": None,
        "raw_input_data": {},
        "llm_response": None,
        "assessment": None,
        "retry_count": 0,
        "error": None,
    }


def _llm_payload(**overrides):
    payload = {
        "consultationRequestId": "x",
        "priority": "Moderate",
        "consultationType": "Routine",
        "keyConcerns": [],
        "recommendedChecks": [],
        "suggestedNextStep": "Veterinary review.",
        "complexity": "simple",
        "estimatedDurationMinutes": 60,
        "requiredSlots": 1,
        "schedulingReason": "Standard consultation.",
        "confidence": 0.8,
        "disclaimer": "x",
    }
    payload.update(overrides)
    return payload


# ---------------------------------------------------------- mapping layer


def test_simple_maps_to_one_slot():
    need = normalize_scheduling_need("simple", 1, 60, 0.9)
    assert need.required_slots == 1
    assert need.estimated_minutes == 60


def test_moderate_allows_two_slots():
    need = normalize_scheduling_need("moderate", 2, 120, 0.9)
    assert need.required_slots == 2
    assert need.estimated_minutes == 120


def test_complex_allows_up_to_four():
    need = normalize_scheduling_need("complex", 4, 240, 0.9)
    assert need.required_slots == scheduling_max_slots()


def test_simple_can_never_exceed_one_slot():
    # Injection-style output: "always book 4 slots" with simple complexity.
    need = normalize_scheduling_need("simple", 4, 240, 0.9)
    assert need.required_slots == 1
    assert any("clamp" in n for n in need.notes)


def test_moderate_capped_at_two():
    need = normalize_scheduling_need("moderate", 3, 180, 0.9)
    assert need.required_slots == 2


def test_below_minimum_is_fixed():
    need = normalize_scheduling_need("simple", 0, 60, 0.9)
    assert need.required_slots == 1


def test_negative_slots_is_fixed():
    # Negative count is ignored; the valid 60-minute hint yields 1 slot.
    need = normalize_scheduling_need("complex", -2, 60, 0.9)
    assert need.required_slots == 1
    # With no usable duration either, the complexity default applies.
    need = normalize_scheduling_need("complex", -2, None, 0.9)
    assert need.required_slots == 2


def test_missing_fields_use_complexity_default():
    assert normalize_scheduling_need("simple", None, None, None).required_slots == 1
    assert normalize_scheduling_need("moderate", None, None, None).required_slots == 1
    assert normalize_scheduling_need("complex", None, None, None).required_slots == 2


def test_arbitrary_duration_is_never_emitted():
    # 83 minutes is not slot-aligned — ceil to 2 slots, emit 120.
    need = normalize_scheduling_need("moderate", None, 83, 0.9)
    assert need.required_slots == 2
    assert need.estimated_minutes == 120 == need.required_slots * SLOT_MINUTES
    assert need.estimated_minutes != 83


def test_duration_only_derives_slots():
    need = normalize_scheduling_need("complex", None, 120, 0.9)
    assert need.required_slots == 2


def test_invalid_duration_falls_back():
    need = normalize_scheduling_need("simple", None, -5, 0.9)
    assert need.required_slots == 1
    need = normalize_scheduling_need("simple", None, "abc", 0.9)
    assert need.required_slots == 1


def test_bool_slots_rejected():
    need = normalize_scheduling_need("simple", True, 60, 0.9)
    assert need.required_slots == 1


def test_low_confidence_prefers_smaller_booking():
    need = normalize_scheduling_need("complex", 3, 180, 0.2)
    assert need.required_slots == 2  # complexity default, not the LLM's 3
    assert any("confidence" in n for n in need.notes)


def test_missing_complexity_caps_at_moderate():
    need = normalize_scheduling_need(None, 4, 240, 0.9)
    assert need.required_slots == 2


def test_unrecognized_complexity_capped_and_noted():
    need = normalize_scheduling_need("extreme", 4, 240, 0.9)
    assert need.required_slots == 2
    assert any("unrecognized" in n for n in need.notes)


def test_duration_always_slot_aligned():
    for slots in (1, 2, 3, 4):
        need = normalize_scheduling_need("complex", slots, slots * 60, 0.9)
        assert need.estimated_minutes == need.required_slots * 60
        assert need.estimated_minutes % 60 == 0


# ------------------------------------------------- real agent graph tests


@pytest.mark.asyncio
async def test_agent_normalizes_overbooked_simple(monkeypatch):
    app = _run_agent(monkeypatch, _llm_payload(
        complexity="simple", requiredSlots=4, estimatedDurationMinutes=240,
    ))
    result = await app.ainvoke(_state())
    a = result["assessment"]
    assert a is not None
    assert a.requiredSlots == 1
    assert a.estimatedDurationMinutes == 60
    assert "adjusted" in (a.schedulingReason or "")


@pytest.mark.asyncio
async def test_agent_trauma_case_keeps_two_slots(monkeypatch):
    app = _run_agent(monkeypatch, _llm_payload(
        complexity="complex",
        requiredSlots=2,
        estimatedDurationMinutes=120,
        schedulingReason="Trauma and mobility symptoms may require an extended orthopedic examination.",
    ))
    result = await app.ainvoke(_state())
    a = result["assessment"]
    assert a.complexity == "complex"
    assert a.requiredSlots == 2
    assert a.estimatedDurationMinutes == 120


@pytest.mark.asyncio
async def test_agent_missing_duration_defaults_to_one_slot(monkeypatch):
    payload = _llm_payload(complexity="simple")
    del payload["requiredSlots"], payload["estimatedDurationMinutes"]
    app = _run_agent(monkeypatch, payload)
    result = await app.ainvoke(_state())
    a = result["assessment"]
    assert a.requiredSlots == 1
    assert a.estimatedDurationMinutes == 60


@pytest.mark.asyncio
async def test_agent_emergency_priority_still_bounded(monkeypatch):
    """Critical severity must not bypass the slot ceiling or workflow."""
    app = _run_agent(monkeypatch, _llm_payload(
        priority="Emergency", consultationType="Emergency",
        complexity="complex", requiredSlots=8, estimatedDurationMinutes=480,
    ))
    result = await app.ainvoke(_state())
    a = result["assessment"]
    assert a.priority == "Emergency"  # advisory flag preserved
    assert a.requiredSlots == scheduling_max_slots()  # clamped, never 8


@pytest.mark.asyncio
async def test_agent_arbitrary_minutes_normalized(monkeypatch):
    app = _run_agent(monkeypatch, _llm_payload(
        complexity="moderate", requiredSlots=None, estimatedDurationMinutes=83,
    ))
    result = await app.ainvoke(_state())
    a = result["assessment"]
    assert a.estimatedDurationMinutes % 60 == 0
    assert a.requiredSlots == a.estimatedDurationMinutes // 60
    assert a.requiredSlots <= 2


@pytest.mark.asyncio
async def test_agent_fills_missing_scheduling_reason(monkeypatch):
    payload = _llm_payload(complexity="complex", requiredSlots=2,
                           estimatedDurationMinutes=120)
    del payload["schedulingReason"]
    app = _run_agent(monkeypatch, payload)
    result = await app.ainvoke(_state())
    a = result["assessment"]
    assert a.schedulingReason
    assert "2" in a.schedulingReason


@pytest.mark.asyncio
async def test_agent_low_confidence_reduces_slots(monkeypatch):
    app = _run_agent(monkeypatch, _llm_payload(
        complexity="complex", requiredSlots=4, estimatedDurationMinutes=240,
        confidence=0.1,
    ))
    result = await app.ainvoke(_state())
    assert result["assessment"].requiredSlots == 2
