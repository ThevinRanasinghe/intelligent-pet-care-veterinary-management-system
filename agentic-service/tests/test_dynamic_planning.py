"""Dynamic-planning proof (Part 5): different objectives / business state
produce different VALID plans and therefore different delegated agents.

Every test runs the REAL plan_workflow -> validate_plan -> select_next ->
run_specialist pipeline; only the LLM and the four specialist graphs are
mocked (fakes record their invocations so delegation is observable).
"""
import json

import pytest
from langgraph.types import Command

from supervisor.planner import DEFAULT_PLAN
from supervisor.models import PlanStep, WorkflowPlan
from tests.evaluation.helpers import (
    FakeApp,
    FakeAssessment,
    FakeLLM,
    approve,
    build_graph,
    cfg,
    default_apps,
    delegated_agents,
    initial_state,
    patch_all,
    restore_apps,
    run_to_gate,
    trajectory_events,
)


def _plan_json(steps):
    return json.dumps({"objective": "llm-objective", "steps": steps})


def _step(n, type_, **kw):
    s = PlanStep(step=n, type=type_, **kw)
    return s.model_dump()


# A VALID plan that stops after booking — the supervisor decided this
# objective does not need clinical specialists. validate_plan accepts it
# (diagnosis/inventory are optional).
SHORT_PLAN = [
    _step(1, "agent", agent="consultation_agent", purpose="Triage"),
    _step(2, "agent", agent="scheduling_agent", purpose="Propose slot",
          requires=["consultation_analyzed"]),
    _step(3, "human_approval", purpose="Manager decision"),
    _step(4, "backend_action", action="book_appointment", purpose="Book"),
]


@pytest.mark.asyncio
async def test_objective_reaches_the_planner_prompt(monkeypatch):
    """The supervisor actually receives the domain objective — the prompt
    sent to the LLM contains it (planner is not decorative)."""
    from types import SimpleNamespace

    captured = []

    class _CapturingLLM:
        async def ainvoke(self, messages):
            captured.append([str(m.content) for m in messages])
            return SimpleNamespace(
                content=json.dumps(DEFAULT_PLAN.model_dump())
            )

    import supervisor.planner as planner
    monkeypatch.setattr(planner, "get_llm", lambda: _CapturingLLM())
    original = patch_all(monkeypatch, apps=default_apps())
    try:
        graph = build_graph()
        await graph.ainvoke(
            initial_state("dyn-obj", objective="Schedule Bella's dental consult"),
            config=cfg("dyn-obj"),
        )
    finally:
        restore_apps(original)

    prompt_text = "\n".join(part for call in captured for part in call)
    assert "Schedule Bella's dental consult" in prompt_text


@pytest.mark.asyncio
async def test_short_valid_plan_delegates_only_front_stage(monkeypatch):
    """Plan WITHOUT clinical steps -> workflow completes after booking;
    diagnosis/inventory are NEVER delegated. Routing follows the plan,
    not a hardcoded four-agent sequence."""
    original = patch_all(
        monkeypatch,
        llm=FakeLLM([_plan_json(SHORT_PLAN)]),
        apps=default_apps(),
    )
    try:
        graph = build_graph()
        tid = "dyn-short"
        values = await run_to_gate(graph, tid)
        values = await approve(graph, tid)
    finally:
        restore_apps(original)

    assert values["status"] == "Completed"
    assert delegated_agents(values) == ["consultation_agent", "scheduling_agent"]
    agents_in_steps = {s.get("agentName") for s in values["steps"]}
    assert "diagnosis_agent" not in agents_in_steps
    assert "inventory_agent" not in agents_in_steps


@pytest.mark.asyncio
async def test_full_plan_delegates_all_four_same_workflow(monkeypatch):
    """The complete plan runs all four specialists under ONE workflow id —
    diagnosis only after examination, inventory only after prescription."""
    original = patch_all(
        monkeypatch,
        llm=FakeLLM([json.dumps(DEFAULT_PLAN.model_dump())]),
        apps=default_apps(),
    )
    try:
        graph = build_graph()
        tid = "dyn-full"
        values = await run_to_gate(graph, tid)
        assert values["workflowId"] == tid

        # Missing prerequisite -> await, NOT execution.
        assert not any(
            s.get("agentName") == "diagnosis_agent" for s in values["steps"]
        )

        values = await approve(graph, tid)
        assert values["status"] == "AwaitingExamination"
        assert "diagnosis_agent" not in delegated_agents(values)

        # Examination event -> diagnosis_agent runs inside the same thread.
        # trajectory is an operator.add reducer — strip before re-invoking
        # or the checkpointed history is duplicated (mirrors main.py).
        values = dict(values)
        values["trajectory"] = []
        values["availableEvents"] = {"examination_recorded": {"id": "ex-1"}}
        values["eventRefs"] = {"examination_recorded": "ex-1"}
        values["iterations"] = 0
        await graph.ainvoke(values, config=cfg(tid))
        snap = await graph.aget_state(cfg(tid))
        values = snap.values
        assert values["status"] == "AwaitingPrescription"
        assert "diagnosis_agent" in delegated_agents(values)
        assert "inventory_agent" not in delegated_agents(values)

        # Prescription event -> inventory_agent runs; workflow completes.
        values = dict(values)
        values["trajectory"] = []
        values["availableEvents"] = {
            "examination_recorded": {"id": "ex-1"},
            "prescription_created": {"id": "rx-1"},
        }
        values["eventRefs"] = {
            "examination_recorded": "ex-1",
            "prescription_created": "rx-1",
        }
        values["iterations"] = 0
        await graph.ainvoke(values, config=cfg(tid))
        snap = await graph.aget_state(cfg(tid))
        values = snap.values
    finally:
        restore_apps(original)

    assert values["status"] == "Completed"
    assert delegated_agents(values) == [
        "consultation_agent",
        "scheduling_agent",
        "diagnosis_agent",
        "inventory_agent",
    ]
    assert values["workflowId"] == tid
    # Every executed specialist recorded a step WITH output + toolCalls.
    for agent in ("consultation_agent", "scheduling_agent",
                  "diagnosis_agent", "inventory_agent"):
        step = next(s for s in values["steps"] if s.get("agentName") == agent)
        assert step["status"] == "Completed"
        assert step["output"] is not None


@pytest.mark.asyncio
async def test_same_plan_different_state_picks_different_agent(monkeypatch):
    """State-driven routing: the SAME plan yields a different next agent
    depending on which business events have already occurred."""
    original = patch_all(
        monkeypatch,
        llm=FakeLLM([json.dumps(DEFAULT_PLAN.model_dump())]),
        apps=default_apps(),
    )
    try:
        graph = build_graph()

        # State A: consultation analyzed -> scheduling is next.
        state_a = initial_state("dyn-state-a")
        state_a["plan"] = DEFAULT_PLAN.model_dump()
        state_a["completedSteps"] = [1]
        state_a["status"] = "Running"
        state_a["availableEvents"] = {"consultation_analyzed": {"ok": 1}}
        state_a["steps"] = [{
            "stepNumber": 1, "agentName": "consultation_agent",
            "status": "Completed", "output": {"ok": 1},
        }]
        await graph.ainvoke(state_a, config=cfg("dyn-state-a"))
        snap = await graph.aget_state(cfg("dyn-state-a"))
        assert "scheduling_agent" in delegated_agents(snap.values)
        assert "diagnosis_agent" not in delegated_agents(snap.values)

        # State B: front stage done + examination event -> diagnosis next.
        state_b = initial_state("dyn-state-b")
        state_b["plan"] = DEFAULT_PLAN.model_dump()
        state_b["completedSteps"] = [1, 2, 3, 4]
        state_b["status"] = "Approved"
        state_b["availableEvents"] = {"examination_recorded": {"id": "ex-7"}}
        state_b["eventRefs"] = {"examination_recorded": "ex-7"}
        await graph.ainvoke(state_b, config=cfg("dyn-state-b"))
        snap = await graph.aget_state(cfg("dyn-state-b"))
        assert "diagnosis_agent" in delegated_agents(snap.values)
        assert "consultation_agent" not in delegated_agents(snap.values)
    finally:
        restore_apps(original)


@pytest.mark.asyncio
async def test_missing_prerequisite_is_visible_not_executed(monkeypatch):
    """A gated specialist is visibly AWAITED, not silently run or skipped:
    the trajectory records route_await_event + awaiting_event with the
    missing prerequisite named."""
    original = patch_all(
        monkeypatch,
        llm=FakeLLM([json.dumps(DEFAULT_PLAN.model_dump())]),
        apps=default_apps(),
    )
    try:
        graph = build_graph()
        values = await run_to_gate(graph, "dyn-await")
        values = await approve(graph, "dyn-await")
    finally:
        restore_apps(original)

    events = trajectory_events(values)
    assert "route_await_event" in events
    await_events = [
        e for e in values["trajectory"] if e["event"] == "awaiting_event"
    ]
    assert any(
        "examination_recorded" in (e.get("detail") or {}).get("waitingFor", [])
        for e in await_events
    )
    assert "diagnosis_agent" not in delegated_agents(values)


@pytest.mark.asyncio
async def test_llm_plan_shape_determines_delegation_set(monkeypatch):
    """Two different VALID LLM plans -> two different delegation sets.
    This is the core supervisor-autonomy assertion."""
    delegated_by_plan = {}
    for name, steps in (
        ("full", [s.model_dump() for s in DEFAULT_PLAN.steps]),
        ("short", SHORT_PLAN),
    ):
        original = patch_all(
            monkeypatch,
            llm=FakeLLM([_plan_json(steps)]),
            apps=default_apps(),
        )
        try:
            graph = build_graph()
            tid = f"dyn-var-{name}"
            await run_to_gate(graph, tid)
            values = await approve(graph, tid)
            if name == "full":
                # Feed both events so the full plan can complete.
                values = dict(values)
                values["trajectory"] = []
                values["availableEvents"] = {
                    "examination_recorded": {"id": "e"},
                    "prescription_created": {"id": "r"},
                }
                values["eventRefs"] = {
                    "examination_recorded": "e",
                    "prescription_created": "r",
                }
                values["iterations"] = 0
                await graph.ainvoke(values, config=cfg(tid))
                snap = await graph.aget_state(cfg(tid))
                values = snap.values
        finally:
            restore_apps(original)
        delegated_by_plan[name] = set(delegated_agents(values))

    assert delegated_by_plan["short"] == {
        "consultation_agent", "scheduling_agent",
    }
    assert delegated_by_plan["full"] == {
        "consultation_agent", "scheduling_agent",
        "diagnosis_agent", "inventory_agent",
    }
    assert delegated_by_plan["full"] != delegated_by_plan["short"]
