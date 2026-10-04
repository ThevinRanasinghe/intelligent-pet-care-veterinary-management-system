from pydantic import BaseModel, Field, field_validator
from typing import List, Literal, Optional, TypedDict

class AppointmentRecommendation(BaseModel):
    appointmentSlotId: str
    veterinarianId: str
    date: str
    startTime: str
    endTime: str
    branch: str
    reason: str

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
    disclaimer: str = "AI-generated scheduling and quotation proposal — requires deterministic backend validation and Clinic Manager approval before execution."

    @field_validator("disclaimer")
    def force_disclaimer(cls, v):
        return "AI-generated scheduling and quotation proposal — requires deterministic backend validation and Clinic Manager approval before execution."

def get_safe_fallback(request_id: str) -> SchedulingAssessment:
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
        disclaimer="AI planning could not be generated reliably — deterministic backend validation and Clinic Manager review are required."
    )

class SchedulingAgentState(TypedDict):
    request_id: str
    auth_token: Optional[str]
    raw_input_data: dict
    llm_response: Optional[str]
    assessment: Optional[SchedulingAssessment]
    retry_count: int
    error: Optional[str]
