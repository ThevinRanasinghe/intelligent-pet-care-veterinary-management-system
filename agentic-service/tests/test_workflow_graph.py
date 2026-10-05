"""Supervisor LangGraph: routing, interrupt, caps, and failure paths."""
import json
from types import SimpleNamespace

import pytest
from langgraph.types import Command

import supervisor.planner as planner
import supervisor.validation as validation
from supervisor.graph import build_graph
from supervisor.planner import DEFAULT_PLAN
from supervisor.specialists import SPECIALIST_APPS


class _FakeLLM:
    def __init__(self, responses):
        self.responses = list(responses)

    async def ainvoke(self, messages):
        item = self.responses.pop(0) if self.responses else ""
        if isinstance(item, Exception):
            raise item
        return SimpleNamespace(content=item)


class _FakeAssessment:
    def __init__(self, data):
        self._data = data

    def model_dump(self):
        return self._data


class _FakeApp:
    """Stand-in compiled graph: returns canned assessment/error."""

    def __init__(self, result):
        self.result = result
        self.seen_states = []

    async def ainvoke(self, state):
        self.seen_states.append(dict(state))
        out = dict(state)
        out.update(self.result)
        return out


SCHED_ASSESSMENT = {
    "requestId": "c1",
    "recommendedAppointment": {
        "veterinarianId": "vet-1",
        "date": "2026-02-01",
        "startTime": "09:00",
        "endTime": "10:00",
        "appointmentSlotId": "slot-1",
    },
    "quotationProposal": {"total": 4500},
    "validationSummary": {"slotAvailable": True},
    "confidence": "High",
    "planningNotes": "ok",
    "disclaimer": "Advisory only; a Clinic Manager must approve.",
}

CONSULT_ASSESSMENT = {"requestId": "c1", "priority": "Normal",
                      "disclaimer": "Advisory only."}
DIAG_ASSESSMENT = {"examinationId": "ex1", "disclaimer": "Advisory only."}
INV_ASSESSMENT = {"treatmentRecordId": "t1", "disclaimer": "Advisory only."}


def _specialist_apps(**overrides):
    apps = {
        "consultation_agent": {
            "app": _FakeApp({"assessment": _FakeAssessment(CONSULT_ASSESSMENT)}),
            "id_key": "consultation_request_id",
        },
        "scheduling_agent": {
            "app": _FakeApp({"assessment": _FakeAssessment(SCHED_ASSESSMENT)}),
            "id_key": "request_id",
        },
        "diagnosis_agent": {
            "app": _FakeApp({"assessment": _FakeAssessment(DIAG_ASSESSMENT)}),
            "id_key": "examination_id",
        },
        "inventory_agent": {
            "app": _FakeApp({"assessment": _FakeAssessment(INV_ASSESSMENT)}),
            "id_key": "treatment_record_id",
        },
    }
    for name, spec in overrides.items():
        apps[name] = spec
    return apps


@pytest.fixture
def patched(monkeypatch):
    """Patch planner LLM, all specialist graphs, and the conflict check."""
    plan_json = json.dumps(DEFAULT_PLAN.model_dump())
    monkeypatch.setattr(planner, "get_llm", lambda: _FakeLLM([plan_json]))
    original = {k: dict(v) for k, v in SPECIALIST_APPS.items()}
    SPECIALIST_APPS.update(_specialist_apps())
    async def _no_conflict(*args, **kwargs):
        return False

    async def _slots(*args, **kwargs):
        # Post-approval revalidation — proposed slot still Available.
        return [{"id": "slot-1"}]

    monkeypatch.setattr(validation, "check_conflict", _no_conflict)
    monkeypatch.setattr(validation, "fetch_available_slots", _slots)
    yield
    SPECIALIST_APPS.clear()
    SPECIALIST_APPS.update(original)


def _initial(workflow_id="wf-1", **overrides):
    state = {
        "workflowId": workflow_id,
        "consultationRequestId": "c1",
        "objective": "triage to billing",
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


def _cfg(tid):
    return {"configurable": {"thread_id": tid}}


@pytest.mark.asyncio
async def test_run_reaches_approval_gate_and_interrupts(patched):
    graph = build_graph()
    result = await graph.ainvoke(_initial("wf-1"), config=_cfg("wf-1"))
    snap = await graph.aget_state(_cfg("wf-1"))

    assert snap.next  # paused at the approval gate
    assert snap.values["status"] == "PendingManagerApproval"
    assert snap.values["proposal"]["appointment"]["veterinarianId"] == "vet-1"
    assert not snap.values.get("approvedAction")
    agents = [s["agentName"] for s in snap.values["steps"]]
    assert agents == ["consultation_agent", "scheduling_agent"]
    events = [e["event"] for e in snap.values["trajectory"]]
    assert "plan_created" in events
    assert events.count("delegated") == 2


@pytest.mark.asyncio
async def test_approved_resume_emits_action_then_awaits_event(patched):
    graph = build_graph()
    await graph.ainvoke(_initial("wf-2"), config=_cfg("wf-2"))
    await graph.ainvoke(
        Command(resume={"decision": "Approved"}), config=_cfg("wf-2")
    )
    snap = await graph.aget_state(_cfg("wf-2"))

    action = snap.values.get("approvedAction")
    assert action and action["action"] == "book_appointment"
    assert action["veterinarianId"] == "vet-1"
    # diagnosis requires examination_recorded — not asserted yet
    assert snap.values["status"] == "AwaitingExamination"
    assert not any(
        s.get("agentName") == "diagnosis_agent" for s in snap.values["steps"]
    )


@pytest.mark.asyncio
async def test_rejected_resume_produces_no_action(patched):
    graph = build_graph()
    await graph.ainvoke(_initial("wf-3"), config=_cfg("wf-3"))
    await graph.ainvoke(
        Command(resume={"decision": "Rejected", "comments": "no"}),
        config=_cfg("wf-3"),
    )
    snap = await graph.aget_state(_cfg("wf-3"))

    assert snap.values["status"] == "Rejected"
    assert not snap.values.get("approvedAction")


@pytest.mark.asyncio
async def test_invalid_decision_fails_safely(patched):
    graph = build_graph()
    await graph.ainvoke(_initial("wf-4"), config=_cfg("wf-4"))
    await graph.ainvoke(
        Command(resume={"decision": "ApproveItNow"}), config=_cfg("wf-4")
    )
    snap = await graph.aget_state(_cfg("wf-4"))

    assert snap.values["status"] == "Failed"
    assert snap.values["failureReason"] == "invalid_decision"
    assert not snap.values.get("approvedAction")


@pytest.mark.asyncio
async def test_delegation_cap_stops_workflow(patched):
    graph = build_graph()
    state = _initial("wf-5", delegationCount=6)  # at cap already
    await graph.ainvoke(state, config=_cfg("wf-5"))
    snap = await graph.aget_state(_cfg("wf-5"))

    assert snap.values["status"] == "Failed"
    assert snap.values["failureReason"] == "delegation_cap"


@pytest.mark.asyncio
async def test_specialist_failure_fails_workflow_safely(patched, monkeypatch):
    SPECIALIST_APPS["scheduling_agent"] = {
        "app": _FakeApp({"assessment": None, "error": "backend_unavailable"}),
        "id_key": "request_id",
    }
    graph = build_graph()
    await graph.ainvoke(_initial("wf-6"), config=_cfg("wf-6"))
    snap = await graph.aget_state(_cfg("wf-6"))

    assert snap.values["status"] == "Failed"
    assert snap.values["failureReason"].startswith("step_failed")


@pytest.mark.asyncio
async def test_planner_fallback_event_recorded(patched, monkeypatch):
    monkeypatch.setattr(
        planner, "get_llm", lambda: _FakeLLM([RuntimeError("x"), "no json"])
    )
    graph = build_graph()
    await graph.ainvoke(_initial("wf-7"), config=_cfg("wf-7"))
    snap = await graph.aget_state(_cfg("wf-7"))

    events = [e["event"] for e in snap.values["trajectory"]]
    assert "plan_fallback" in events
    assert snap.values["status"] == "PendingManagerApproval"


@pytest.mark.asyncio
async def test_diagnosis_runs_only_after_examination_event(patched):
    graph = build_graph()
    await graph.ainvoke(_initial("wf-8"), config=_cfg("wf-8"))
    await graph.ainvoke(
        Command(resume={"decision": "Approved"}), config=_cfg("wf-8")
    )
    snap = await graph.aget_state(_cfg("wf-8"))
    assert snap.values["status"] == "AwaitingExamination"

    # Assert examination_recorded — diagnosis can now run.
    resumed = dict(snap.values)
    resumed["availableEvents"] = {"examination_recorded": {"id": "ex1"}}
    resumed["eventRefs"] = {"examination_recorded": "ex1"}
    resumed["iterations"] = 0
    await graph.ainvoke(resumed, config=_cfg("wf-8"))

    snap = await graph.aget_state(_cfg("wf-8"))
    agents = [s.get("agentName") for s in snap.values["steps"]]
    assert "diagnosis_agent" in agents
    assert snap.values["status"] == "AwaitingPrescription"
    diag = SPECIALIST_APPS["diagnosis_agent"]["app"]
    assert diag.seen_states[-1]["examination_id"] == "ex1"


@pytest.mark.asyncio
async def test_final_validation_blocks_when_slot_taken(patched, monkeypatch):
    calls = {"n": 0}

    async def _conflict(*args, **kwargs):
        calls["n"] += 1
        return calls["n"] > 1  # free at proposal time, taken at approval

    monkeypatch.setattr(validation, "check_conflict", _conflict)
    graph = build_graph()
    await graph.ainvoke(_initial("wf-9"), config=_cfg("wf-9"))
    await graph.ainvoke(
        Command(resume={"decision": "Approved"}), config=_cfg("wf-9")
    )
    snap = await graph.aget_state(_cfg("wf-9"))

    assert snap.values["status"] == "Failed"
    assert snap.values["failureReason"] == "slot_no_longer_available"
    assert not snap.values.get("approvedAction")
