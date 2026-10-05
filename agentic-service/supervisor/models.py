"""Supervisor workflow contracts (spec §3).

The workflow state is a plain TypedDict so LangGraph + MemorySaver can
checkpoint it; the JSON-facing models are Pydantic so request/response
validation is explicit. The ASP.NET backend persists `snapshot`-shaped
data ({plan, steps, delegationCount, revisionCount, approval}) and sends
it back for rehydration when the in-memory checkpoint is gone.
"""
import operator
from typing import Annotated, Any, Dict, List, Literal, Optional, TypedDict

from pydantic import BaseModel, Field

from shared.tool_registry import ToolCallRecord  # re-exported contract

ALLOWED_AGENTS = {
    "consultation_agent",
    "scheduling_agent",
    "diagnosis_agent",
    "inventory_agent",
}

STEP_TYPES = {"agent", "human_approval", "backend_action"}

EVENT_CONSULTATION_SUBMITTED = "consultation_submitted"
EVENT_CONSULTATION_ANALYZED = "consultation_analyzed"
EVENT_APPOINTMENT_BOOKED = "appointment_booked"
EVENT_EXAMINATION_RECORDED = "examination_recorded"
EVENT_PRESCRIPTION_CREATED = "prescription_created"

MAX_PLAN_STEPS = 8
MAX_DELEGATIONS = 6
MAX_SUPERVISOR_ITERATIONS = 10
MAX_REVISIONS = 2


class PlanStep(BaseModel):
    step: int
    type: Literal["agent", "human_approval", "backend_action"]
    agent: Optional[str] = None
    action: Optional[str] = None
    purpose: str = ""
    requires: List[str] = Field(default_factory=list)


class WorkflowPlan(BaseModel):
    objective: str
    steps: List[PlanStep] = Field(default_factory=list)


class StepRecord(BaseModel):
    stepNumber: int
    agentName: Optional[str] = None
    task: str = ""
    inputSummary: Dict[str, Any] = Field(default_factory=dict)
    output: Optional[Dict[str, Any]] = None
    toolCalls: List[Dict[str, Any]] = Field(default_factory=list)
    validation: Optional[Dict[str, Any]] = None
    status: str = "Pending"  # Pending | Completed | Failed | NoProposal | Skipped
    error: Optional[str] = None
    retryCount: int = 0
    startedAt: Optional[str] = None
    completedAt: Optional[str] = None


class TrajectoryEvent(BaseModel):
    seq: int
    timestamp: str
    node: str
    agent: Optional[str] = None
    event: str
    detail: Dict[str, Any] = Field(default_factory=dict)


class ProposalAppointment(BaseModel):
    veterinarianId: str
    date: str
    startTime: str
    endTime: str
    appointmentSlotId: str
    # Multi-slot + fallback metadata (defaults keep single-slot
    # proposals backward compatible).
    slotIds: List[str] = Field(default_factory=list)
    slotCount: int = 1
    requiredSlots: int = 1
    estimatedDurationMinutes: int = 60
    usedPreferredTime: bool = True
    usedPreferredDate: bool = True
    fallbackType: str = "none"
    requestedDate: Optional[str] = None
    requestedTime: Optional[str] = None


class Proposal(BaseModel):
    appointment: ProposalAppointment
    quotation: Optional[Dict[str, Any]] = None
    validationSummary: Dict[str, Any] = Field(default_factory=dict)
    confidence: str = "Low"
    planningNotes: str = ""
    disclaimer: str = ""


class ApprovalDecision(BaseModel):
    decision: Literal["Approved", "Rejected", "RevisionRequested"]
    comments: Optional[str] = None
    approverId: Optional[str] = None
    decidedAt: Optional[str] = None


class WorkflowRunResponse(BaseModel):
    workflowId: str
    status: str
    plan: Optional[Dict[str, Any]] = None
    steps: List[Dict[str, Any]] = Field(default_factory=list)
    trajectory: List[Dict[str, Any]] = Field(default_factory=list)
    trajectoryTotal: int = 0
    proposal: Optional[Dict[str, Any]] = None
    approvedAction: Optional[Dict[str, Any]] = None
    delegationCount: int = 0
    revisionCount: int = 0
    failureReason: Optional[str] = None
    interrupted: bool = False


class WorkflowState(TypedDict, total=False):
    # Identity / inputs (supplied by the backend)
    workflowId: str
    consultationRequestId: str
    objective: str
    availableEvents: Dict[str, Any]
    auth_token: Optional[str]
    revisionHint: Optional[str]
    eventRefs: Dict[str, str]  # event name -> reference id (examinationId etc.)
    decision: Optional[Dict[str, Any]]  # pending approval decision (resume)
    approval: Optional[Dict[str, Any]]  # last applied approval decision

    # Plan + progress
    plan: Optional[Dict[str, Any]]
    completedSteps: List[int]
    currentStep: Optional[Dict[str, Any]]
    lastStepRecord: Optional[Dict[str, Any]]
    steps: List[Dict[str, Any]]
    trajectory: Annotated[List[Dict[str, Any]], operator.add]
    proposal: Optional[Dict[str, Any]]
    approvedAction: Optional[Dict[str, Any]]

    # Counters + outcome
    delegationCount: int
    revisionCount: int
    iterations: int
    status: str
    failureReason: Optional[str]
    pendingFailure: Optional[str]
