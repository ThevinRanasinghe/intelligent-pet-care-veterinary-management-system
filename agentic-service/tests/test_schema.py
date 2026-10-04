"""Pydantic schema checks: invalid output must fail validation."""
import pytest
from pydantic import ValidationError

from consultation_agent.models import ConsultationAssessment
from diagnosis_agent.models import DiagnosisAssessment
from inventory_agent.models import InventoryAssessment, InventorySummary
from scheduling_agent.models import SchedulingAssessment, ValidationSummary


def test_consultation_rejects_bad_priority():
    with pytest.raises(ValidationError):
        ConsultationAssessment(
            consultationRequestId="c1",
            priority="SuperUrgent",
            consultationType="Routine",
            suggestedNextStep="x",
        )


def test_diagnosis_rejects_bad_severity():
    with pytest.raises(ValidationError):
        DiagnosisAssessment(
            suspectedCondition="x",
            recommendedSeverity="Catastrophic",
            rationale="r",
        )


def test_diagnosis_schema_valid():
    a = DiagnosisAssessment(
        suspectedCondition="gastritis",
        recommendedSeverity="Moderate",
        rationale="r",
        suggestedMedicines=[{"medicineName": "m", "suggestedDosage": "1 tab",
                             "suggestedDurationDays": 5}],
    )
    assert a.suggestedMedicines[0].suggestedDurationDays == 5


def test_scheduling_requires_validation_summary():
    with pytest.raises(ValidationError):
        SchedulingAssessment(
            requestId="r1",
            confidence="High",
            planningNotes="n",
        )


def test_inventory_summary_requires_all_flags():
    with pytest.raises(ValidationError):
        InventorySummary(medicineFound=True, stockAvailable=True,
                         sufficientQuantity=True, batchAvailable=True,
                         notExpired=True)  # missing lowStock


def test_inventory_disclaimer_enforced():
    a = InventoryAssessment(
        requestId="r",
        inventorySummary=InventorySummary(
            medicineFound=True, stockAvailable=True, sufficientQuantity=True,
            batchAvailable=True, notExpired=True, lowStock=False),
        confidence="High",
        planningNotes="n",
        disclaimer="",
    )
    assert "staff review" in a.disclaimer
