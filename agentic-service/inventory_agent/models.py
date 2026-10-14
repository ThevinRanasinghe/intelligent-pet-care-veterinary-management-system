from typing import TypedDict, Optional, List, Dict, Any
from pydantic import BaseModel, Field, field_validator

class MedicineRecommendation(BaseModel):
    medicineId: str
    medicineName: str
    requiredQuantity: int
    availableQuantity: int
    sufficientStock: bool
    reason: str

class RecommendedBatch(BaseModel):
    batchId: str
    batchNumber: str
    quantityAvailable: int
    expiryDate: str
    expiryStatus: str

class InventorySummary(BaseModel):
    medicineFound: bool
    stockAvailable: bool
    sufficientQuantity: bool
    batchAvailable: bool
    notExpired: bool
    lowStock: bool

class InventoryAssessment(BaseModel):
    requestId: str
    medicineRecommendation: Optional[MedicineRecommendation] = None
    recommendedBatch: Optional[RecommendedBatch] = None
    alternativeMedicines: List[Any] = Field(default_factory=list)
    inventorySummary: InventorySummary
    confidence: str
    planningNotes: str
    disclaimer: str = "AI-generated medicine and inventory recommendation — deterministic backend validation and authorized staff review are required before any inventory action."

    @field_validator("disclaimer")
    def force_disclaimer(cls, v):
        return "AI-generated medicine and inventory recommendation — deterministic backend validation and authorized staff review are required before any inventory action."

class InventoryAgentState(TypedDict):
    # The input identifier is a TreatmentRecordId: in the PetCare workflow a
    # "medicine request" is the batch of Prescription rows created under a
    # treatment record.
    treatment_record_id: str
    auth_token: Optional[str]
    raw_input_data: Dict[str, Any]
    llm_response: Optional[str]
    assessment: Optional[InventoryAssessment]
    retry_count: int
    error: Optional[str]

def get_safe_fallback(request_id: str) -> InventoryAssessment:
    return InventoryAssessment(
        requestId=request_id,
        medicineRecommendation=None,
        recommendedBatch=None,
        alternativeMedicines=[],
        inventorySummary=InventorySummary(
            medicineFound=False,
            stockAvailable=False,
            sufficientQuantity=False,
            batchAvailable=False,
            notExpired=False,
            lowStock=False
        ),
        confidence="Low",
        planningNotes="AI medicine and inventory planning could not be generated reliably.",
        disclaimer="AI planning could not be generated reliably — deterministic backend validation and authorized staff review are required."
    )
