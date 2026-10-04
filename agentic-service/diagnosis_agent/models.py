from pydantic import BaseModel, Field, field_validator
from typing import List, Literal, Optional, TypedDict

class RecommendedMedicine(BaseModel):
    medicineName: str
    suggestedDosage: str
    suggestedDurationDays: int

class DiagnosisAssessment(BaseModel):
    suspectedCondition: str
    recommendedSeverity: Literal["Low", "Moderate", "High", "Critical"]
    rationale: str
    recommendedProcedures: List[str] = Field(default_factory=list)
    suggestedMedicines: List[RecommendedMedicine] = Field(default_factory=list)
    precautionaryNotes: List[str] = Field(default_factory=list)

def get_safe_fallback(examination_id: str) -> DiagnosisAssessment:
    """Returns a deterministic safe fallback if the LLM fails repeatedly."""
    return DiagnosisAssessment(
        suspectedCondition="Condition could not be determined reliably",
        recommendedSeverity="Moderate",
        rationale="AI recommendation could not be generated reliably — requires veterinarian confirmation before treatment.",
        recommendedProcedures=["Standard physical examination"],
        suggestedMedicines=[],
        precautionaryNotes=["System fallback triggered. Rely on clinical judgement."]
    )

class DiagnosisAgentState(TypedDict):
    examination_id: str
    auth_token: Optional[str]
    raw_input_data: dict
    llm_response: Optional[str]
    assessment: Optional[DiagnosisAssessment]
    retry_count: int
    error: Optional[str]
