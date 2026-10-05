"""Planner + model contract tests (no graph run needed)."""
import json
from types import SimpleNamespace

import pytest

from supervisor import planner
from supervisor.models import (
    MAX_PLAN_STEPS,
    PlanStep,
    WorkflowPlan,
)
from supervisor.planner import (
    DEFAULT_PLAN,
    PlanValidationError,
    plan_workflow,
    validate_plan,
)


def _plan(steps):
    return WorkflowPlan(objective="test", steps=steps)


def _valid_steps():
    return [s.model_dump() for s in DEFAULT_PLAN.steps]


def test_default_plan_is_valid():
    validate_plan(DEFAULT_PLAN)  # must not raise


def test_valid_routing_plan_accepted():
    plan = _plan([
        PlanStep(step=1, type="agent", agent="consultation_agent"),
        PlanStep(step=2, type="agent", agent="scheduling_agent", requires=["consultation_analyzed"]),
        PlanStep(step=3, type="human_approval"),
        PlanStep(step=4, type="backend_action", action="book_appointment"),
        PlanStep(step=5, type="agent", agent="diagnosis_agent",
                 requires=["examination_recorded"]),
        PlanStep(step=6, type="agent", agent="inventory_agent",
                 requires=["prescription_created"]),
    ])
    validate_plan(plan)


def test_invalid_agent_rejected():
    plan = _plan([
        PlanStep(step=1, type="agent", agent="approve_request"),
        PlanStep(step=2, type="agent", agent="scheduling_agent", requires=["consultation_analyzed"]),
        PlanStep(step=3, type="human_approval"),
        PlanStep(step=4, type="backend_action", action="book_appointment"),
    ])
    with pytest.raises(PlanValidationError, match="unsupported agent"):
        validate_plan(plan)


def test_missing_consultation_step_rejected():
    plan = _plan([
        PlanStep(step=1, type="agent", agent="scheduling_agent", requires=["consultation_analyzed"]),
        PlanStep(step=2, type="human_approval"),
        PlanStep(step=3, type="backend_action", action="book_appointment"),
    ])
    with pytest.raises(PlanValidationError, match="consultation_agent"):
        validate_plan(plan)


def test_diagnosis_without_examination_require_rejected():
    plan = _plan([
        PlanStep(step=1, type="agent", agent="consultation_agent"),
        PlanStep(step=2, type="agent", agent="scheduling_agent", requires=["consultation_analyzed"]),
        PlanStep(step=3, type="human_approval"),
        PlanStep(step=4, type="backend_action", action="book_appointment"),
        PlanStep(step=5, type="agent", agent="diagnosis_agent"),
    ])
    with pytest.raises(PlanValidationError, match="examination_recorded"):
        validate_plan(plan)


def test_unsatisfiable_requires_event_rejected():
    # "appointment_proposed" is not a real event — a plan requiring it would
    # park forever at AwaitingEvent. The whitelist check must reject it.
    plan = _plan([
        PlanStep(step=1, type="agent", agent="consultation_agent"),
        PlanStep(step=2, type="agent", agent="scheduling_agent", requires=["consultation_analyzed"]),
        PlanStep(step=3, type="human_approval", requires=["appointment_proposed"]),
        PlanStep(step=4, type="backend_action", action="book_appointment"),
    ])
    with pytest.raises(PlanValidationError, match="unsatisfiable"):
        validate_plan(plan)


def test_approval_must_immediately_follow_scheduling():
    plan = _plan([
        PlanStep(step=1, type="agent", agent="consultation_agent"),
        PlanStep(step=2, type="agent", agent="scheduling_agent", requires=["consultation_analyzed"]),
        PlanStep(step=3, type="backend_action", action="book_appointment"),
        PlanStep(step=4, type="human_approval"),
    ])
    with pytest.raises(PlanValidationError, match="immediately follow"):
        validate_plan(plan)


def test_excessive_plan_steps_rejected():
    steps = [
        PlanStep(step=1, type="agent", agent="consultation_agent"),
        PlanStep(step=2, type="agent", agent="scheduling_agent", requires=["consultation_analyzed"]),
        PlanStep(step=3, type="human_approval"),
        PlanStep(step=4, type="backend_action", action="book_appointment"),
    ]
    for i in range(5, MAX_PLAN_STEPS + 3):
        steps.append(PlanStep(step=i, type="agent", agent="diagnosis_agent",
                              requires=["examination_recorded"]))
    with pytest.raises(PlanValidationError, match="max"):
        validate_plan(_plan(steps))


class _FakeLLM:
    def __init__(self, responses):
        self.responses = list(responses)
        self.calls = 0

    async def ainvoke(self, messages):
        self.calls += 1
        item = self.responses.pop(0) if self.responses else "ok"
        if isinstance(item, Exception):
            raise item
        return SimpleNamespace(content=item)


def _llm_json(steps):
    return json.dumps({"objective": "from-llm", "steps": steps})


@pytest.mark.asyncio
async def test_planner_returns_valid_llm_plan(monkeypatch):
    fake = _FakeLLM([_llm_json(_valid_steps())])
    monkeypatch.setattr(planner, "get_llm", lambda: fake)

    plan, fallback = await plan_workflow({"objective": "triage"})
    assert fallback is False
    assert plan.objective == "from-llm"
    assert fake.calls == 1


@pytest.mark.asyncio
async def test_planner_retries_once_on_malformed_output(monkeypatch):
    fake = _FakeLLM(["not json at all", _llm_json(_valid_steps())])
    monkeypatch.setattr(planner, "get_llm", lambda: fake)

    plan, fallback = await plan_workflow({})
    assert fallback is False
    assert fake.calls == 2


@pytest.mark.asyncio
async def test_planner_falls_back_after_two_bad_attempts(monkeypatch):
    fake = _FakeLLM([RuntimeError("boom"), "no json here"])
    monkeypatch.setattr(planner, "get_llm", lambda: fake)

    plan, fallback = await plan_workflow({})
    assert fallback is True
    assert [s.agent or s.type for s in plan.steps][:2] == [
        "consultation_agent", "scheduling_agent",
    ]
    assert fake.calls == 2


@pytest.mark.asyncio
async def test_planner_rejects_llm_plan_with_fake_agent(monkeypatch):
    steps = _valid_steps()
    steps[0]["agent"] = "hacker_agent"
    fake = _FakeLLM([_llm_json(steps)])
    monkeypatch.setattr(planner, "get_llm", lambda: fake)

    plan, fallback = await plan_workflow({})
    # invalid plan + exhausted retries -> deterministic fallback
    assert fallback is True
    assert plan.steps[0].agent == "consultation_agent"
