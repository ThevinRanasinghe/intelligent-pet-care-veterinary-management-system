from pydantic import BaseModel, Field, field_validator
from typing import Any, Dict, List, Literal, Optional, TypedDict


class AppointmentRecommendation(BaseModel):
    appointmentSlotId: str
    veterinarianId: str
    date: str
    startTime: str
    endTime: str
    branch: str
    reason: str
    # Multi-slot + fallback metadata. appointmentSlotId is always the
    # first slot of the consecutive window; slotIds lists them all.
    slotIds: List[str] = Field(default_factory=list)
    slotCount: int = 1
    requiredSlots: int = 1
    estimatedDurationMinutes: int = 60
    usedPreferredTime: bool = True
    usedPreferredDate: bool = True
    fallbackUsed: bool = False
    fallbackType: str = "none"  # none | same_day_nearest_time | next_available_date


class QuotationItemRecommendation(BaseModel):
    category: Literal["Consultation", "Examination", "Treatment", "Medicine", "Other"]
    description: str
    quantity: int
    unitPrice: float
    reason: str


class QuotationProposal(BaseModel):
    budget: float
    items: List[QuotationItemRecommendation] = Field(default_factory=list)
    estimatedSubtotal: float
    estimatedTotal: float
    withinBudget: bool


class ValidationSummary(BaseModel):
    slotFound: bool
    veterinarianAvailable: bool
    noKnownConflict: bool
    withinRequestedTime: bool
    withinBudget: bool


class SchedulingAssessment(BaseModel):
    requestId: str
    recommendedAppointment: Optional[AppointmentRecommendation]
    alternativeSlots: List[AppointmentRecommendation] = Field(default_factory=list)
    quotationProposal: Optional[QuotationProposal]
    validationSummary: ValidationSummary
    confidence: Literal["Low", "Moderate", "High"]
    planningNotes: str
    # Deterministic slot-search evidence: what was asked, what was
    # searched, and which fallback (if any) produced the proposal.
    searchDiagnostics: Dict[str, Any] = Field(default_factory=dict)
    # Stable machine-readable outcome: OK | NO_VALID_SLOT | INVALID_DURATION
    reasonCode: str = "OK"
    disclaimer: str = "AI-generated scheduling and quotation proposal - requires deterministic backend validation and Clinic Manager approval before execution."

    @field_validator("disclaimer")
    def force_disclaimer(cls, v):
        return "AI-generated scheduling and quotation proposal - requires deterministic backend validation and Clinic Manager approval before execution."


def get_safe_fallback(request_id: str, reason_code: str = "NO_VALID_SLOT") -> SchedulingAssessment:
    """Returns a deterministic safe fallback if the LLM fails repeatedly."""
    return SchedulingAssessment(
        requestId=request_id,
        recommendedAppointment=None,
        alternativeSlots=[],
        quotationProposal=None,
        validationSummary=ValidationSummary(
            slotFound=False,
            veterinarianAvailable=False,
            noKnownConflict=False,
            withinRequestedTime=False,
            withinBudget=False
        ),
        confidence="Low",
        planningNotes="AI scheduling and quotation planning could not be generated reliably.",
        reasonCode=reason_code,
        disclaimer="AI planning could not be generated reliably - deterministic backend validation and Clinic Manager review are required."
    )


class SchedulingAgentState(TypedDict):
    request_id: str
    auth_token: Optional[str]
    raw_input_data: dict
    # Consultation agent's structured output, injected by the supervisor.
    consultation_assessment: Optional[dict]
    # Deterministic slot-search outcome (set by select_window node).
    slot_selection: Optional[dict]
    # Extra facts persisted into the step's inputSummary for audit.
    input_summary: Optional[dict]
    llm_response: Optional[str]
    assessment: Optional[SchedulingAssessment]
    retry_count: int
    error: Optional[str]
