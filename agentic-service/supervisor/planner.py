"""LLM planning node for the supervisor.

The LLM proposes a WorkflowPlan; deterministic code validates it
(``validate_plan``). One retry embeds the validation error in the
prompt; on a second failure the graph falls back to DEFAULT_PLAN and
records a ``plan_fallback`` trajectory event. The supervisor never calls
tools and never emits business output — only a plan.
"""
import json
import logging
from typing import Any, Dict, List, Optional

from langchain_core.messages import HumanMessage, SystemMessage
from pydantic import ValidationError

from shared.llm import content_to_text, extract_json_object, get_llm

from .models import (
    ALLOWED_AGENTS,
    EVENT_CONSULTATION_ANALYZED,
    EVENT_APPOINTMENT_BOOKED,
    EVENT_CONSULTATION_SUBMITTED,
    EVENT_EXAMINATION_RECORDED,
    EVENT_PRESCRIPTION_CREATED,
    MAX_PLAN_STEPS,
    STEP_TYPES,
    PlanStep,
    WorkflowPlan,
)

logger = logging.getLogger("SupervisorPlanner")


class PlanValidationError(ValueError):
    """The proposed plan violates a deterministic ordering/shape rule."""


# The canonical end-to-end plan, used when the LLM cannot produce a
# valid one after retry. Ordering mirrors the real business workflow.
DEFAULT_PLAN = WorkflowPlan(
    objective="Coordinate the consultation-to-billing workflow.",
    steps=[
        PlanStep(step=1, type="agent", agent="consultation_agent",
                 purpose="Triage the consultation request."),
        PlanStep(step=2, type="agent", agent="scheduling_agent",
                 purpose="Propose an appointment slot and quotation.",
                 requires=["consultation_analyzed"]),
        PlanStep(step=3, type="human_approval",
                 purpose="ClinicManager reviews the proposal."),
        PlanStep(step=4, type="backend_action", action="book_appointment",
                 purpose="Backend books the approved appointment."),
        PlanStep(step=5, type="agent", agent="diagnosis_agent",
                 purpose="Advisory diagnosis support once an examination exists.",
                 requires=["examination_recorded"]),
        PlanStep(step=6, type="agent", agent="inventory_agent",
                 purpose="Advisory medicine/inventory plan once a prescription exists.",
                 requires=["prescription_created"]),
    ],
)

# Ordering prerequisites: step kind/agent -> condition on state.
# (evaluated by graph.route_next; validated statically here)

# The complete vocabulary of event names that can appear in a step's
# "requires". Anything else can never be satisfied -> the step would wait
# forever, so the plan must be rejected up front.
SATISFIABLE_EVENTS = frozenset({
    EVENT_CONSULTATION_SUBMITTED,
    EVENT_CONSULTATION_ANALYZED,
    EVENT_APPOINTMENT_BOOKED,
    EVENT_EXAMINATION_RECORDED,
    EVENT_PRESCRIPTION_CREATED,
})


def validate_plan(plan: WorkflowPlan) -> None:
    """Deterministic plan checks. Raises PlanValidationError."""
    if not plan.steps:
        raise PlanValidationError("plan contains no steps")
    if len(plan.steps) > MAX_PLAN_STEPS:
        raise PlanValidationError(
            f"plan has {len(plan.steps)} steps (max {MAX_PLAN_STEPS})"
        )

    agents = [s.agent for s in plan.steps if s.type == "agent"]
    for step in plan.steps:
        if step.type not in STEP_TYPES:
            raise PlanValidationError(f"unknown step type '{step.type}'")
        if step.type == "agent" and step.agent not in ALLOWED_AGENTS:
            raise PlanValidationError(f"unsupported agent '{step.agent}'")
        if step.type == "agent" and not step.agent:
            raise PlanValidationError("agent step missing 'agent'")
        if step.type == "backend_action" and step.action != "book_appointment":
            raise PlanValidationError(
                f"unsupported backend action '{step.action}'"
            )
        unknown = set(step.requires or []) - SATISFIABLE_EVENTS
        if unknown:
            raise PlanValidationError(
                f"step {step.step} requires unsatisfiable event(s) "
                f"{sorted(unknown)} — allowed: {sorted(SATISFIABLE_EVENTS)}"
            )

    def index_of(predicate) -> int:
        for i, step in enumerate(plan.steps):
            if predicate(step):
                return i
        return -1

    consultation_idx = index_of(lambda s: s.agent == "consultation_agent")
    scheduling_idx = index_of(lambda s: s.agent == "scheduling_agent")
    approval_idx = index_of(lambda s: s.type == "human_approval")
    action_idx = index_of(
        lambda s: s.type == "backend_action" and s.action == "book_appointment"
    )
    diagnosis_idx = index_of(lambda s: s.agent == "diagnosis_agent")
    inventory_idx = index_of(lambda s: s.agent == "inventory_agent")

    if consultation_idx == -1:
        raise PlanValidationError("plan lacks a consultation_agent step")
    if scheduling_idx == -1:
        raise PlanValidationError("plan lacks a scheduling_agent step")
    if scheduling_idx < consultation_idx:
        raise PlanValidationError("scheduling_agent must run after consultation_agent")
    scheduling_step = plan.steps[scheduling_idx]
    if EVENT_CONSULTATION_ANALYZED not in scheduling_step.requires:
        raise PlanValidationError(
            "scheduling_agent step must require 'consultation_analyzed'"
        )
    if approval_idx == -1 or approval_idx != scheduling_idx + 1:
        raise PlanValidationError(
            "human_approval must immediately follow the scheduling step"
        )
    if action_idx == -1 or action_idx != approval_idx + 1:
        raise PlanValidationError(
            "backend_action 'book_appointment' must immediately follow human_approval"
        )
    if diagnosis_idx != -1:
        step = plan.steps[diagnosis_idx]
        if EVENT_EXAMINATION_RECORDED not in step.requires:
            raise PlanValidationError(
                "diagnosis_agent step must require 'examination_recorded'"
            )
        if diagnosis_idx <= scheduling_idx:
            raise PlanValidationError("diagnosis_agent must run after scheduling")
    if inventory_idx != -1:
        step = plan.steps[inventory_idx]
        if EVENT_PRESCRIPTION_CREATED not in step.requires:
            raise PlanValidationError(
                "inventory_agent step must require 'prescription_created'"
            )
        if diagnosis_idx != -1 and inventory_idx < diagnosis_idx:
            raise PlanValidationError("inventory_agent must run after diagnosis")


_SYSTEM_PROMPT = """You are the supervisor planner for a veterinary consultation workflow.
Produce a JSON plan of steps the workflow should execute.

Allowed agents: consultation_agent, scheduling_agent, diagnosis_agent, inventory_agent.
Allowed step types: "agent", "human_approval", "backend_action" (only action "book_appointment").

Required ordering: consultation_agent before scheduling_agent; a human_approval step
immediately after scheduling_agent; the backend_action "book_appointment" immediately
after human_approval. scheduling_agent steps must list "consultation_analyzed" in
"requires"; diagnosis_agent steps must list "examination_recorded" in
"requires" and come after scheduling; inventory_agent steps must list
"prescription_created" in "requires".

The ONLY event names allowed in "requires" are: consultation_submitted,
consultation_analyzed, appointment_booked, examination_recorded,
prescription_created. Any other value can never occur and will be
rejected. Steps that only need the previous step's output (human_approval,
backend_action) must leave "requires" empty.

Output ONLY a raw JSON object:
{"objective": "...", "steps": [{"step": 1, "type": "agent",
"agent": "consultation_agent", "action": null, "purpose": "...",
"requires": []}]}"""


async def plan_workflow(state: Dict[str, Any]):
    """Returns (WorkflowPlan, used_fallback) — LLM → retry once → DEFAULT_PLAN."""
    revision_hint = state.get("revisionHint")
    events = state.get("availableEvents") or {}
    prompt = (
        f"Objective: {state.get('objective', '')}\n"
        f"ConsultationRequestId: {state.get('consultationRequestId', '')}\n"
        f"Available business events: {json.dumps(sorted(events.keys()))}\n"
        + (f"Manager revision hint: {revision_hint}\n" if revision_hint else "")
        + "Return the plan JSON now."
    )

    last_error: Optional[str] = None
    for attempt in range(2):
        sys_prompt = _SYSTEM_PROMPT
        if last_error:
            sys_prompt += f"\n\nPREVIOUS PLAN INVALID (fix it): {last_error}"
        try:
            response = await get_llm().ainvoke(
                [SystemMessage(content=sys_prompt), HumanMessage(content=prompt)]
            )
            content = extract_json_object(content_to_text(response.content))
            if content is None:
                raise PlanValidationError("no JSON object in planner output")
            plan = WorkflowPlan(**json.loads(content))
            validate_plan(plan)
            return plan, False
        except (PlanValidationError, ValidationError, json.JSONDecodeError) as exc:
            last_error = f"{type(exc).__name__}: {exc}"
            logger.warning("planner attempt %d invalid: %s", attempt + 1, last_error)
        except Exception as exc:  # noqa: BLE001 — LLM transport/config errors
            last_error = f"{type(exc).__name__}: {exc}"
            logger.warning("planner attempt %d failed: %s", attempt + 1, last_error)

    logger.error("planner exhausted retries — using DEFAULT_PLAN")
    return DEFAULT_PLAN, True
