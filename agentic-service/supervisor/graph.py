"""Explicit supervisor StateGraph (spec §3).

Flow:
    START -> supervisor_plan -> select_next
        -> run_specialist -> record_step -> validate_step -> select_next
        -> approval_gate (interrupt; resume via Command(resume=decision))
        -> final_validation -> select_next
        -> backend_action_marker -> select_next
        -> await_event | finalize | safe_failure -> END

Routing is CODE: ``select_next`` picks the first plan step that is not
completed and whose prerequisites (requires ⊆ availableEvents, plus the
sequential ordering of the plan) are satisfied. A step that cannot run
pauses the graph at ``await_event`` instead of being skipped.
"""
import logging
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Tuple

from langgraph.checkpoint.memory import MemorySaver
from langgraph.graph import END, START, StateGraph
from langgraph.types import interrupt

from .models import (
    EVENT_CONSULTATION_ANALYZED,
    EVENT_EXAMINATION_RECORDED,
    EVENT_PRESCRIPTION_CREATED,
    MAX_DELEGATIONS,
    MAX_REVISIONS,
    MAX_SUPERVISOR_ITERATIONS,
    WorkflowPlan,
    WorkflowState,
)
from .planner import plan_workflow
from .specialists import run_specialist as _run_specialist
from .validation import final_validation as _final_validation
from .validation import validate_proposal as _validate_proposal

logger = logging.getLogger("SupervisorGraph")

_TERMINAL_STATUSES = {"Completed", "Rejected", "Failed"}
_APPROVAL_DECISIONS = {"Approved", "Rejected", "RevisionRequested"}

# Which event gates each specialist's step — the awaiting status to emit.
_EVENT_STATUS = {
    "diagnosis_agent": "AwaitingExamination",
    "inventory_agent": "AwaitingPrescription",
}


def _utcnow() -> str:
    return datetime.now(timezone.utc).isoformat()


def _event(state: Dict[str, Any], node: str, event: str,
           agent: Optional[str] = None, detail: Optional[Dict[str, Any]] = None):
    seq = len(state.get("trajectory") or []) + 1
    return {
        "seq": seq,
        "timestamp": _utcnow(),
        "node": node,
        "agent": agent,
        "event": event,
        "detail": detail or {},
    }


# ---------------------------------------------------------------- routing


def _events(state: Dict[str, Any]) -> set:
    return set((state.get("availableEvents") or {}).keys())


def _pick_next(state: Dict[str, Any]) -> Tuple[str, Optional[Dict[str, Any]]]:
    """First non-completed plan step that is runnable.

    Returns (kind, step): kind in
    {agent, human_approval, backend_action, await_event, done, fail}.
    """
    if state.get("pendingFailure") or state.get("failureReason"):
        return "fail", None
    if state.get("status") in _TERMINAL_STATUSES:
        return "done", None

    plan = state.get("plan") or {}
    steps: List[Dict[str, Any]] = plan.get("steps") or []
    completed = set(state.get("completedSteps") or [])
    events = _events(state)

    for step in sorted(steps, key=lambda s: s.get("step", 0)):
        if step.get("step") in completed:
            continue
        requires = set(step.get("requires") or [])
        if not requires.issubset(events):
            return "await_event", step
        step_type = step.get("type")
        if step_type == "agent":
            return "agent", step
        if step_type == "human_approval":
            if not state.get("proposal"):
                # No validated proposal yet — cannot gate on nothing.
                return "await_event", step
            return "human_approval", step
        if step_type == "backend_action":
            if not state.get("approvedAction"):
                return "await_event", step
            return "backend_action", step
        return "fail", None  # unknown step type — validated plans prevent this

    return "done", None


def _awaiting_status(step: Optional[Dict[str, Any]]) -> str:
    if step and step.get("agent") in _EVENT_STATUS:
        return _EVENT_STATUS[step["agent"]]
    return "AwaitingEvent"


def _awaiting_events(step: Optional[Dict[str, Any]], state: Dict[str, Any]) -> List[str]:
    if not step:
        return []
    missing = sorted(set(step.get("requires") or []) - _events(state))
    if step.get("type") == "human_approval" and not state.get("proposal"):
        missing.append("validated_proposal")
    if step.get("type") == "backend_action" and not state.get("approvedAction"):
        missing.append("approval")
    return missing


# ------------------------------------------------------------------ nodes


async def supervisor_plan(state: Dict[str, Any]) -> Dict[str, Any]:
    events: List[Dict[str, Any]] = []
    if state.get("plan"):
        events.append(_event(state, "supervisor_plan", "plan_reused"))
        return {"trajectory": events}

    plan, used_fallback = await plan_workflow(state)
    events.append(
        _event(
            state,
            "supervisor_plan",
            "plan_fallback" if used_fallback else "plan_created",
            detail={"steps": len(plan.steps)},
        )
    )
    return {
        "plan": plan.model_dump(),
        "status": "Running",
        "trajectory": events,
    }


def select_next(state: Dict[str, Any]) -> Dict[str, Any]:
    iterations = int(state.get("iterations") or 0) + 1
    kind, step = _pick_next(state)
    update: Dict[str, Any] = {"iterations": iterations}

    if iterations > MAX_SUPERVISOR_ITERATIONS:
        update["pendingFailure"] = "iteration_cap"
        update["currentStep"] = {"kind": "fail", "step": None}
        update["trajectory"] = [
            _event(state, "select_next", "iteration_cap",
                   detail={"iterations": iterations})
        ]
        return update

    update["currentStep"] = {"kind": kind, "step": step}
    update["trajectory"] = [
        _event(state, "select_next", f"route_{kind}",
               agent=(step or {}).get("agent"),
               detail={"step": (step or {}).get("step")})
    ]
    if kind == "human_approval":
        update["status"] = "PendingManagerApproval"
    if kind == "await_event":
        update["status"] = _awaiting_status(step)
    return update


def route_after_select(state: Dict[str, Any]) -> str:
    kind = (state.get("currentStep") or {}).get("kind")
    return {
        "agent": "run_specialist",
        "human_approval": "approval_gate",
        "backend_action": "backend_action_marker",
        "await_event": "await_event",
        "done": "finalize",
        "fail": "safe_failure",
    }.get(kind, "safe_failure")


def _input_id_for(state: Dict[str, Any], agent: str) -> Optional[str]:
    refs = state.get("eventRefs") or {}
    if agent == "diagnosis_agent":
        return refs.get(EVENT_EXAMINATION_RECORDED)
    if agent == "inventory_agent":
        return refs.get(EVENT_PRESCRIPTION_CREATED)
    return state.get("consultationRequestId")


async def run_specialist(state: Dict[str, Any]) -> Dict[str, Any]:
    step = (state.get("currentStep") or {}).get("step") or {}
    agent = step.get("agent")
    delegations = int(state.get("delegationCount") or 0) + 1
    events: List[Dict[str, Any]] = []

    if delegations > MAX_DELEGATIONS:
        events.append(
            _event(state, "run_specialist", "delegation_cap",
                   agent=agent, detail={"delegations": delegations})
        )
        return {
            "delegationCount": delegations,
            "pendingFailure": "delegation_cap",
            "trajectory": events,
        }

    input_id = _input_id_for(state, agent)

    # The scheduling agent consumes the consultation assessment to derive
    # the required slot count/duration deterministically.
    context = None
    if agent == "scheduling_agent":
        consultation_output = next(
            (s.get("output") for s in reversed(state.get("steps") or [])
             if s.get("agentName") == "consultation_agent"
             and s.get("status") == "Completed"
             and s.get("output")),
            None,
        )
        context = {"consultation_assessment": consultation_output}

    events.append(
        _event(state, "run_specialist", "delegated",
               agent=agent, detail={"step": step.get("step"), "inputId": input_id})
    )
    record = await _run_specialist(
        agent,
        input_id or "",
        state.get("auth_token"),
        step.get("step", 0),
        task=step.get("purpose", ""),
        context=context,
    )
    return {
        "delegationCount": delegations,
        "lastStepRecord": record,
        "trajectory": events,
    }


def record_step(state: Dict[str, Any]) -> Dict[str, Any]:
    record = dict(state.get("lastStepRecord") or {})
    steps = list(state.get("steps") or [])
    steps.append(record)
    completed = list(state.get("completedSteps") or [])

    events = [_event(state, "record_step", "step_recorded",
                     agent=record.get("agentName"),
                     detail={"step": record.get("stepNumber"),
                             "status": record.get("status")})]
    update: Dict[str, Any] = {
        "steps": steps,
        "completedSteps": completed,
        "trajectory": events,
    }

    if record.get("status") == "Completed":
        completed.append(record.get("stepNumber"))
        # consultation_analyzed gates the scheduling step.
        if record.get("agentName") == "consultation_agent":
            available = dict(state.get("availableEvents") or {})
            available[EVENT_CONSULTATION_ANALYZED] = True
            update["availableEvents"] = available
    elif record.get("status") == "Failed":
        events.append(
            _event(state, "record_step", "step_failed",
                   agent=record.get("agentName"),
                   detail={"error": record.get("error")})
        )
        return {
            "steps": steps,
            "completedSteps": completed,
            "pendingFailure": f"step_failed:{record.get('error')}",
            "trajectory": events,
        }

    return update


async def validate_step(state: Dict[str, Any]) -> Dict[str, Any]:
    steps = list(state.get("steps") or [])
    if not steps:
        return {}
    record = steps[-1]
    events: List[Dict[str, Any]] = []

    if record.get("agentName") == "scheduling_agent" and record.get("status") == "Completed":
        proposal, failure = await _validate_proposal(
            record.get("output"), state.get("auth_token")
        )
        record["validation"] = {
            "checked": True,
            "proposal": proposal is not None,
            "failure": failure,
        }
        events.append(
            _event(state, "validate_step", "step_validated",
                   agent="scheduling_agent",
                   detail={"failure": failure, "proposal": proposal is not None})
        )
        update: Dict[str, Any] = {"steps": steps, "trajectory": events}
        if failure:
            update["pendingFailure"] = failure
        elif proposal is None:
            record["status"] = "NoProposal"
            reason_code = (record.get("output") or {}).get("reasonCode")
            update["pendingFailure"] = (
                "invalid_duration" if reason_code == "INVALID_DURATION" else "no_valid_slot"
            )
        else:
            update["proposal"] = proposal.model_dump()
        return update

    record["validation"] = {"checked": True}
    events.append(
        _event(state, "validate_step", "step_validated",
               agent=record.get("agentName"), detail={"failure": None})
    )
    return {"steps": steps, "trajectory": events}


async def approval_gate(state: Dict[str, Any]) -> Dict[str, Any]:
    events: List[Dict[str, Any]] = [
        _event(state, "approval_gate", "awaiting_decision",
               detail={"interrupted": state.get("decision") is None})
    ]

    decision = state.get("decision")
    if decision is None:
        # Pauses the graph here; the backend resumes with
        # Command(resume={"decision": ..., "comments": ..., ...}).
        decision = interrupt(
            {
                "workflowId": state.get("workflowId"),
                "proposal": state.get("proposal"),
                "stepSummaries": [
                    {"step": s.get("stepNumber"), "agent": s.get("agentName"),
                     "status": s.get("status")}
                    for s in (state.get("steps") or [])
                ],
            }
        )

    # The decision comes ONLY from the resumed Command or the rehydrated
    # input — never from LLM output.
    if isinstance(decision, dict) and "decision" in decision:
        decision = decision.get("decision") if set(decision) == {"decision"} else decision
    if not isinstance(decision, dict):
        decision = {"decision": decision}
    verdict = decision.get("decision")

    if verdict not in _APPROVAL_DECISIONS:
        events.append(
            _event(state, "approval_gate", "invalid_decision",
                   detail={"decision": verdict})
        )
        return {
            "decision": None,
            "pendingFailure": "invalid_decision",
            "trajectory": events,
        }

    events.append(
        _event(state, "approval_gate", f"decision_{str(verdict).lower()}",
               detail={"comments": bool((decision or {}).get("comments"))})
    )

    update: Dict[str, Any] = {
        "decision": None,
        "approval": decision,
        "trajectory": events,
    }
    steps = list(state.get("steps") or [])
    completed = list(state.get("completedSteps") or [])
    gate_step = ((state.get("currentStep") or {}).get("step") or {}).get("step")

    if verdict == "Rejected":
        steps.append({
            "stepNumber": gate_step, "agentName": None,
            "task": "human_approval", "status": "Completed",
            "output": {"decision": "Rejected"},
            "completedAt": _utcnow(),
        })
        completed.append(gate_step)
        update.update(steps=steps, completedSteps=completed, status="Rejected")
        return update

    if verdict == "RevisionRequested":
        revision_count = int(state.get("revisionCount") or 0) + 1
        steps.append({
            "stepNumber": gate_step, "agentName": None,
            "task": "human_approval", "status": "Completed",
            "output": {"decision": "RevisionRequested"},
            "completedAt": _utcnow(),
        })
        update.update(
            steps=steps,
            revisionCount=revision_count,
            proposal=None,
            revisionHint=(decision or {}).get("comments") or "Revision requested.",
        )
        if revision_count > MAX_REVISIONS:
            update["pendingFailure"] = "revision_limit"
            return update
        # Re-run scheduling with the manager's hint: drop its completed
        # marker AND keep the recorded attempt for audit.
        completed = [n for n in completed if n is not None]
        scheduling_step = next(
            (s.get("step") for s in (state.get("plan") or {}).get("steps", [])
             if s.get("agent") == "scheduling_agent"),
            None,
        )
        completed = [n for n in completed if n != scheduling_step]
        update.update(completedSteps=completed, status="Running")
        return update

    # Approved — mark gate completed; final_validation decides the action.
    steps.append({
        "stepNumber": gate_step, "agentName": None,
        "task": "human_approval", "status": "Completed",
        "output": {"decision": "Approved"},
        "completedAt": _utcnow(),
    })
    completed.append(gate_step)
    update.update(steps=steps, completedSteps=completed, status="Approved")
    return update


def route_after_approval(state: Dict[str, Any]) -> str:
    if state.get("pendingFailure"):
        return "safe_failure"
    if (state.get("approval") or {}).get("decision") == "Approved" and state.get("status") == "Approved":
        return "final_validation"
    return "select_next"


async def final_validation(state: Dict[str, Any]) -> Dict[str, Any]:
    events: List[Dict[str, Any]] = []
    failure = await _final_validation(state.get("proposal"), state.get("auth_token"))
    if failure:
        events.append(
            _event(state, "final_validation", "final_validation_failed",
                   detail={"reason": failure})
        )
        return {"pendingFailure": failure, "trajectory": events}

    appointment = dict((state.get("proposal") or {}).get("appointment") or {})
    events.append(
        _event(state, "final_validation", "final_validation_passed",
               detail={"action": "book_appointment"})
    )
    return {
        "approvedAction": {"action": "book_appointment", **appointment},
        "status": "Approved",
        "trajectory": events,
    }


def backend_action_marker(state: Dict[str, Any]) -> Dict[str, Any]:
    """The Python graph NEVER writes business data. This node only marks
    the backend_action step completed — the backend executes
    ``approvedAction`` via ConsultationWorkflowService.AssignConsultationAsync."""
    step = (state.get("currentStep") or {}).get("step") or {}
    steps = list(state.get("steps") or [])
    completed = list(state.get("completedSteps") or [])
    steps.append({
        "stepNumber": step.get("step"),
        "agentName": None,
        "task": "backend_action",
        "status": "Completed",
        "output": {"action": state.get("approvedAction", {}).get("action"),
                   "emittedTo": "backend"},
        "completedAt": _utcnow(),
    })
    completed.append(step.get("step"))
    return {
        "steps": steps,
        "completedSteps": completed,
        "status": "AwaitingExamination",
        "trajectory": [
            _event(state, "backend_action_marker", "backend_action_emitted",
                   detail={"action": (state.get("approvedAction") or {}).get("action")})
        ],
    }


def await_event(state: Dict[str, Any]) -> Dict[str, Any]:
    step = (state.get("currentStep") or {}).get("step")
    return {
        "status": _awaiting_status(step),
        "trajectory": [
            _event(state, "await_event", "awaiting_event",
                   detail={"waitingFor": _awaiting_events(step, state)})
        ],
    }


def finalize(state: Dict[str, Any]) -> Dict[str, Any]:
    status = state.get("status")
    if status not in _TERMINAL_STATUSES:
        status = "Completed"
    event_name = {
        "Completed": "workflow_completed",
        "Rejected": "workflow_rejected",
    }.get(status, "workflow_finished")
    return {
        "status": status,
        "trajectory": [_event(state, "finalize", event_name)],
    }


def safe_failure(state: Dict[str, Any]) -> Dict[str, Any]:
    reason = state.get("pendingFailure") or state.get("failureReason") or "unknown"
    return {
        "status": "Failed",
        "failureReason": reason,
        "trajectory": [
            _event(state, "safe_failure", "safe_failure",
                   detail={"reason": reason})
        ],
    }


# ------------------------------------------------------------------ build


def build_graph():
    builder = StateGraph(WorkflowState)

    builder.add_node("supervisor_plan", supervisor_plan)
    builder.add_node("select_next", select_next)
    builder.add_node("run_specialist", run_specialist)
    builder.add_node("record_step", record_step)
    builder.add_node("validate_step", validate_step)
    builder.add_node("approval_gate", approval_gate)
    builder.add_node("final_validation", final_validation)
    builder.add_node("backend_action_marker", backend_action_marker)
    builder.add_node("await_event", await_event)
    builder.add_node("finalize", finalize)
    builder.add_node("safe_failure", safe_failure)

    builder.add_edge(START, "supervisor_plan")
    builder.add_edge("supervisor_plan", "select_next")
    builder.add_conditional_edges("select_next", route_after_select)
    builder.add_edge("run_specialist", "record_step")
    builder.add_edge("record_step", "validate_step")
    builder.add_edge("validate_step", "select_next")
    builder.add_conditional_edges(
        "approval_gate",
        route_after_approval,
        {
            "final_validation": "final_validation",
            "select_next": "select_next",
            "safe_failure": "safe_failure",
        },
    )
    builder.add_edge("final_validation", "select_next")
    builder.add_edge("backend_action_marker", "select_next")
    builder.add_edge("await_event", END)
    builder.add_edge("finalize", END)
    builder.add_edge("safe_failure", END)

    return builder.compile(checkpointer=MemorySaver())


workflow_graph = build_graph()
