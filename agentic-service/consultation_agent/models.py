from pydantic import BaseModel, Field, field_validator
from typing import List, Literal, Optional, TypedDict

class KeyConcern(BaseModel):
    concern: str
    reason: str

class ConsultationAssessment(BaseModel):
    consultationRequestId: str
    priority: Literal["Low", "Moderate", "High", "Emergency"]
    consultationType: Literal["Routine", "Urgent", "Emergency"]
    keyConcerns: List[KeyConcern] = Field(default_factory=list)
    recommendedChecks: List[str] = Field(default_factory=list)
    suggestedNextStep: str
    disclaimer: str = "Preliminary AI consultation assessment — requires veterinary review and confirmation."

    @field_validator("disclaimer")
    def force_disclaimer(cls, v):
        return "Preliminary AI consultation assessment — requires veterinary review and confirmation."

def get_safe_fallback(consultation_request_id: str) -> ConsultationAssessment:
    """Returns a deterministic safe fallback if the LLM fails repeatedly."""
    return ConsultationAssessment(
        consultationRequestId=consultation_request_id,
        priority="Moderate",
        consultationType="Routine",
        keyConcerns=[],
        recommendedChecks=[],
        suggestedNextStep="Veterinarian review recommended because an AI consultation assessment could not be generated reliably.",
        disclaimer="AI consultation assessment could not be generated reliably — requires veterinary review and confirmation."
    )

class AgentState(TypedDict):
    consultation_request_id: str
    auth_token: Optional[str]
    raw_input_data: dict
    llm_response: Optional[str]
    assessment: Optional[ConsultationAssessment]
    retry_count: int
    error: Optional[str]
