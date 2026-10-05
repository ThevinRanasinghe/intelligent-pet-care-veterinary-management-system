"""Shared fakes + runner utilities for evaluation cases.

Every case runs the REAL supervisor graph (build_graph()) with only the
LLM, the four specialist apps, and the backend conflict check mocked.
"""
import json
import os
from types import SimpleNamespace

from langgraph.types import Command

import supervisor.planner as planner
import supervisor.validation as validation
from supervisor.graph import build_graph
from supervisor.specialists import SPECIALIST_APPS

GOLDEN_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "golden")


def load_golden(name):
    with open(os.path.join(GOLDEN_DIR, name), encoding="utf-8") as fh:
        return json.load(fh)


class FakeLLM:
    def __init__(self, responses):
        self.responses = list(responses)
        self.calls = 0

    async def ainvoke(self, messages):
        self.calls += 1
        item = self.responses.pop(0) if self.responses else ""
        if isinstance(item, Exception):
            raise item
        return SimpleNamespace(content=item)


class FakeAssessment:
    def __init__(self, data):
        self._data = data

    def model_dump(self):
        return self._data


class FakeApp:
    """Stand-in compiled specialist graph returning canned output."""

    def __init__(self, result=None, exc=None):
        self.result = result or {}
        self.exc = exc
        self.seen_states = []

    async def ainvoke(self, state):
        self.seen_states.append(dict(state))
        if self.exc:
            raise self.exc
        out = dict(state)
        out.update(self.result)
        return out


def patch_all(monkeypatch, *, llm=None, apps=None, conflict=False):
    """Mock the three external surfaces. `apps` maps agent name -> FakeApp."""
    if llm is not None:
        monkeypatch.setattr(planner, "get_llm", lambda: llm)
    original = {k: dict(v) for k, v in SPECIALIST_APPS.items()}
    if apps:
        SPECIALIST_APPS.update(apps)

    async def _conflict(*args, **kwargs):
        return conflict

    # Post-approval revalidation re-fetches availability — the fake says
    # every proposed slot is still Available unless overridden.
    async def _slots(*args, **kwargs):
        return [{"id": sid} for sid in ("slot-1", "slot-2", "slot-3")]

    monkeypatch.setattr(validation, "check_conflict", _conflict)
    monkeypatch.setattr(validation, "fetch_available_slots", _slots)
    return original


def restore_apps(original):
    SPECIALIST_APPS.clear()
    SPECIALIST_APPS.update(original)


def good_appointment(overrides=None):
    appt = {
        "veterinarianId": "vet-1",
        "date": "2026-02-01",
        "startTime": "09:00",
        "endTime": "10:00",
        "appointmentSlotId": "slot-1",
    }
    appt.update(overrides or {})
    return appt


def scheduling_result(appointment=None):
    assessment = {"recommendedAppointment": appointment} if appointment else {}
    assessment.update({"confidence": "High",
                       "disclaimer": "Advisory only; approval required."})
    return {"assessment": FakeAssessment(assessment)}


def default_apps(appointment=None, overrides=None):
    apps = {
        "consultation_agent": {
            "app": FakeApp({"assessment": FakeAssessment({"ok": 1})}),
            "id_key": "consultation_request_id",
        },
        "scheduling_agent": {
            "app": FakeApp(scheduling_result(
                good_appointment() if appointment is None else appointment)),
            "id_key": "request_id",
        },
        "diagnosis_agent": {
            "app": FakeApp({"assessment": FakeAssessment({"ok": 1})}),
            "id_key": "examination_id",
        },
        "inventory_agent": {
            "app": FakeApp({"assessment": FakeAssessment({"ok": 1})}),
            "id_key": "treatment_record_id",
        },
    }
    apps.update(overrides or {})
    return apps


def initial_state(workflow_id="eval-wf", **overrides):
    state = {
        "workflowId": workflow_id,
        "consultationRequestId": "c1",
        "objective": "evaluation",
        "availableEvents": {},
        "eventRefs": {},
        "auth_token": "token",
        "steps": [],
        "trajectory": [],
        "completedSteps": [],
        "delegationCount": 0,
        "revisionCount": 0,
        "iterations": 0,
    }
    state.update(overrides)
    return state


def cfg(tid):
    return {"configurable": {"thread_id": tid}}


async def run_to_gate(graph, tid, **state_overrides):
    """Runs the workflow until the approval interrupt; returns state values."""
    await graph.ainvoke(initial_state(tid, **state_overrides), config=cfg(tid))
    snap = await graph.aget_state(cfg(tid))
    return snap.values


async def approve(graph, tid, decision="Approved"):
    await graph.ainvoke(Command(resume={"decision": decision}), config=cfg(tid))
    snap = await graph.aget_state(cfg(tid))
    return snap.values


def trajectory_events(values):
    return [e["event"] for e in (values.get("trajectory") or [])]


def delegated_agents(values):
    return [
        e["agent"] for e in (values.get("trajectory") or [])
        if e["event"] == "delegated"
    ]
