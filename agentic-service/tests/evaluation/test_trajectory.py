"""Trajectory assertions per spec §6 (deterministic graph run)."""
import json

import pytest
from langgraph.types import Command

from supervisor.models import MAX_DELEGATIONS
from supervisor.planner import DEFAULT_PLAN
from tests.evaluation.helpers import (
    FakeLLM,
    approve,
    build_graph,
    cfg,
    default_apps,
    delegated_agents,
    initial_state,
    patch_all,
    restore_apps,
)


@pytest.fixture
def run_workflow(monkeypatch):
    original = patch_all(
        monkeypatch,
        llm=FakeLLM([json.dumps(DEFAULT_PLAN.model_dump())] * 3),
        apps=default_apps(),
    )
    yield
    restore_apps(original)


@pytest.mark.asyncio
async def test_supervisor_ran_first_and_delegations(run_workflow):
    graph = build_graph()
    await graph.ainvoke(initial_state("traj-1"), config=cfg("traj-1"))
    snap = await graph.aget_state(cfg("traj-1"))
    traj = snap.values["trajectory"]

    # supervisor ran first
    assert traj[0]["node"] == "supervisor_plan"
    assert traj[0]["event"] in ("plan_created", "plan_fallback")
    # seq is monotonically increasing
    seqs = [e["seq"] for e in traj]
    assert seqs == sorted(seqs)

    # expected agents delegated; no unknown agent ever delegated
    assert "consultation_agent" in delegated_agents(snap.values)
    assert "scheduling_agent" in delegated_agents(snap.values)
    for agent in delegated_agents(snap.values):
        assert agent in {
            "consultation_agent", "scheduling_agent",
            "diagnosis_agent", "inventory_agent",
        }

    # delegation cap respected
    assert snap.values["delegationCount"] <= MAX_DELEGATIONS


@pytest.mark.asyncio
async def test_validation_precedes_approval_gate(run_workflow):
    graph = build_graph()
    await graph.ainvoke(initial_state("traj-2"), config=cfg("traj-2"))
    snap = await graph.aget_state(cfg("traj-2"))
    events = [e["event"] for e in snap.values["trajectory"]]

    # proposal validation happens before the gate routes to approval
    assert "step_validated" in events
    assert "route_human_approval" in events
    assert events.index("step_validated") < events.index("route_human_approval")
    # the graph is interrupted at the approval gate (pending node)
    assert "approval_gate" in snap.next

    # interrupted before any approvedAction
    assert snap.next
    assert not snap.values.get("approvedAction")


@pytest.mark.asyncio
async def test_rejected_path_records_no_action(run_workflow):
    graph = build_graph()
    await graph.ainvoke(initial_state("traj-3"), config=cfg("traj-3"))
    values = await approve(graph, "traj-3", decision="Rejected")

    assert values["status"] == "Rejected"
    assert not values.get("approvedAction")
    events = [e["event"] for e in values["trajectory"]]
    assert "decision_rejected" in events
    assert "workflow_rejected" in events


@pytest.mark.asyncio
async def test_resume_path_and_final_status(run_workflow):
    graph = build_graph()
    await graph.ainvoke(initial_state("traj-4"), config=cfg("traj-4"))
    values = await approve(graph, "traj-4", decision="Approved")

    events = [e["event"] for e in values["trajectory"]]
    assert "decision_approved" in events
    assert "final_validation_passed" in events
    assert "backend_action_emitted" in events
    assert values["status"] == "AwaitingExamination"
    assert values["approvedAction"]["action"] == "book_appointment"


@pytest.mark.asyncio
async def test_failed_path_records_reason(run_workflow, monkeypatch):
    import supervisor.validation as validation

    async def _conflict(*a, **k):
        return True

    monkeypatch.setattr(validation, "check_conflict", _conflict)
    graph = build_graph()
    await graph.ainvoke(initial_state("traj-5"), config=cfg("traj-5"))
    snap = await graph.aget_state(cfg("traj-5"))

    events = [e["event"] for e in snap.values["trajectory"]]
    assert "safe_failure" in events
    assert snap.values["status"] == "Failed"
    assert snap.values["failureReason"] == "conflict_detected"
