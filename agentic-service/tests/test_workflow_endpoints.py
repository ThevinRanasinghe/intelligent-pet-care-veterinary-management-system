"""Workflow endpoints: auth, run/resume/advance/state, rehydration."""
import itertools
import json
from types import SimpleNamespace

import pytest
from fastapi.testclient import TestClient

import main
import supervisor.planner as planner
import supervisor.validation as validation
from supervisor.planner import DEFAULT_PLAN
from supervisor.specialists import SPECIALIST_APPS

KEY = {"X-Internal-Key": "test-internal-key"}
_ids = itertools.count(100)


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
    def __init__(self, result):
        self.result = result

    async def ainvoke(self, state):
        out = dict(state)
        out.update(self.result)
        return out


SCHED_ASSESSMENT = {
    "recommendedAppointment": {
        "veterinarianId": "vet-1",
        "date": "2026-02-01",
        "startTime": "09:00",
        "endTime": "10:00",
        "appointmentSlotId": "slot-1",
    },
    "disclaimer": "Advisory only; manager approval required.",
}


@pytest.fixture
def client(monkeypatch):
    monkeypatch.setattr(
        planner, "get_llm",
        lambda: _FakeLLM([json.dumps(DEFAULT_PLAN.model_dump())]),
    )
    original = {k: dict(v) for k, v in SPECIALIST_APPS.items()}
    SPECIALIST_APPS.update({
        "consultation_agent": {
            "app": _FakeApp({"assessment": _FakeAssessment({"ok": 1})}),
            "id_key": "consultation_request_id",
        },
        "scheduling_agent": {
            "app": _FakeApp({"assessment": _FakeAssessment(SCHED_ASSESSMENT)}),
            "id_key": "request_id",
        },
        "diagnosis_agent": {
            "app": _FakeApp({"assessment": _FakeAssessment({"ok": 1})}),
            "id_key": "examination_id",
        },
        "inventory_agent": {
            "app": _FakeApp({"assessment": _FakeAssessment({"ok": 1})}),
            "id_key": "treatment_record_id",
        },
    })

    async def _no_conflict(*args, **kwargs):
        return False

    async def _slots(*args, **kwargs):
        # Post-approval revalidation — every proposed slot still Available.
        return [{"id": "slot-1"}]

    monkeypatch.setattr(validation, "check_conflict", _no_conflict)
    monkeypatch.setattr(validation, "fetch_available_slots", _slots)
    yield TestClient(main.app)
    SPECIALIST_APPS.clear()
    SPECIALIST_APPS.update(original)


def _wf_id():
    return f"wf-endpoint-{next(_ids)}"


def _run(client, workflow_id):
    return client.post(
        f"/api/workflows/{workflow_id}/run",
        headers=KEY,
        json={"consultationRequestId": "c1", "objective": "flow"},
    )


def test_workflow_endpoints_require_internal_key(client):
    wid = _wf_id()
    assert client.post(f"/api/workflows/{wid}/run", json={}).status_code == 401
    assert client.post(f"/api/workflows/{wid}/resume", json={}).status_code == 401
    assert client.post(f"/api/workflows/{wid}/advance", json={}).status_code == 401
    assert client.get(f"/api/workflows/{wid}/state").status_code == 401


def test_run_returns_interrupted_at_approval(client):
    wid = _wf_id()
    resp = _run(client, wid)
    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert body["status"] == "PendingManagerApproval"
    assert body["interrupted"] is True
    assert body["proposal"]["appointment"]["veterinarianId"] == "vet-1"
    assert body["approvedAction"] is None
    assert body["delegationCount"] == 2
    assert len(body["trajectory"]) > 0
    assert body["trajectoryTotal"] == len(body["trajectory"])  # fresh run


def test_resume_approved_emits_approved_action(client):
    wid = _wf_id()
    run_body = _run(client, wid).json()
    resp = client.post(
        f"/api/workflows/{wid}/resume",
        headers=KEY,
        json={"decision": {"decision": "Approved"}},
    )
    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert body["approvedAction"]["action"] == "book_appointment"
    assert body["status"] == "AwaitingExamination"
    # trajectory returned is ONLY this invocation's events
    run_events = {e["event"] for e in run_body["trajectory"]}
    resume_events = {e["event"] for e in body["trajectory"]}
    assert "decision_approved" in resume_events
    assert not (run_events & resume_events) or "plan_created" not in resume_events
    assert body["trajectoryTotal"] == len(run_body["trajectory"]) + len(
        body["trajectory"]
    )


def test_resume_rejected_has_no_action(client):
    wid = _wf_id()
    _run(client, wid)
    resp = client.post(
        f"/api/workflows/{wid}/resume",
        headers=KEY,
        json={"decision": {"decision": "Rejected"}},
    )
    body = resp.json()
    assert body["status"] == "Rejected"
    assert body["approvedAction"] is None


def test_resume_invalid_decision_rejected_400(client):
    wid = _wf_id()
    _run(client, wid)
    resp = client.post(
        f"/api/workflows/{wid}/resume",
        headers=KEY,
        json={"decision": {"decision": "Banana"}},
    )
    assert resp.status_code == 400


def test_resume_unknown_workflow_404(client):
    resp = client.post(
        f"/api/workflows/{_wf_id()}/resume",
        headers=KEY,
        json={"decision": {"decision": "Approved"}},
    )
    assert resp.status_code == 404


def test_advance_to_diagnosis_step(client):
    wid = _wf_id()
    _run(client, wid)
    client.post(
        f"/api/workflows/{wid}/resume",
        headers=KEY,
        json={"decision": {"decision": "Approved"}},
    )
    resp = client.post(
        f"/api/workflows/{wid}/advance",
        headers=KEY,
        json={"event": {"type": "examination_recorded", "referenceId": "ex1"}},
    )
    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert body["status"] == "AwaitingPrescription"
    assert any(
        s.get("agentName") == "diagnosis_agent" for s in body["steps"]
    )


def test_advance_requires_event_fields(client):
    wid = _wf_id()
    _run(client, wid)
    resp = client.post(
        f"/api/workflows/{wid}/advance",
        headers=KEY,
        json={"event": {"type": "examination_recorded"}},
    )
    assert resp.status_code == 400


def test_state_inspection_returns_checkpoint(client):
    wid = _wf_id()
    _run(client, wid)
    resp = client.get(f"/api/workflows/{wid}/state", headers=KEY)
    assert resp.status_code == 200
    body = resp.json()
    assert body["workflowId"] == wid
    assert body["interrupted"] is True
    assert body["proposal"] is not None
    # state inspection returns the FULL trajectory
    assert len(body["trajectory"]) == body["trajectoryTotal"]
    assert any(e["event"] == "plan_created" for e in body["trajectory"])


def test_state_unknown_workflow_404(client):
    assert client.get(
        f"/api/workflows/{_wf_id()}/state", headers=KEY
    ).status_code == 404


def test_run_rehydrates_from_snapshot_when_no_checkpoint(client):
    """A backend-persisted snapshot can stand in for a missing checkpoint."""
    wid = _wf_id()
    snapshot = {
        "plan": DEFAULT_PLAN.model_dump(),
        "steps": [
            {"stepNumber": 1, "agentName": "consultation_agent",
             "task": "t", "status": "Completed",
             "output": {"ok": 1}},
        ],
        "completedSteps": [1],
        "delegationCount": 1,
        "revisionCount": 0,
        "status": "Running",
        # The consultation step completed in the snapshot — its analysis
        # event gates the scheduling step.
        "availableEvents": {"consultation_analyzed": {"ok": 1}},
    }
    resp = client.post(
        f"/api/workflows/{wid}/run",
        headers=KEY,
        json={
            "consultationRequestId": "c1",
            "objective": "resumed externally",
            "snapshot": snapshot,
        },
    )
    assert resp.status_code == 200, resp.text
    body = resp.json()
    # scheduling was re-run from the snapshot; only one NEW step recorded
    scheduling_steps = [
        s for s in body["steps"] if s.get("agentName") == "scheduling_agent"
    ]
    assert len(scheduling_steps) == 1
    assert body["interrupted"] is True
    assert body["delegationCount"] == 2  # 1 persisted + 1 new
