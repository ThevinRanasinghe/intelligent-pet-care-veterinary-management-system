"""Automated Unit and Workflow Tests for Consultation Request Analysis Agent.

Component: Pet & Consultation Request Management
Architecture: LangGraph + LangChain + Pydantic + Google Gemini (Mocked)

These tests verify deterministic workflow logic, schema validation,
boundary enforcement, retry mechanics, and safe fallback behavior
without requiring external API keys or live network connectivity.
"""

import json
import os
import sys
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from pydantic import ValidationError

# Ensure agentic-service is on sys.path for direct imports
current_dir = os.path.dirname(os.path.abspath(__file__))
service_dir = os.path.abspath(os.path.join(current_dir, ".."))
if service_dir not in sys.path:
    sys.path.insert(0, service_dir)

from consultation_agent.agent import (
    analyze_request_node,
    consultation_agent_app,
    fallback_node,
    retrieve_context_node,
    route_after_context,
    route_after_validation,
    validate_response_node,
)
from consultation_agent.models import (
    AgentState,
    ConsultationAssessment,
    KeyConcern,
    get_safe_fallback,
)


# ============================================================================
# REQUIRED TEST CASES (TC-CONS-AI-01 to TC-CONS-AI-10)
# ============================================================================


def test_tc_cons_ai_01_valid_consultation_assessment():
    """TC-CONS-AI-01: Valid Consultation Assessment.

    Verifies that a valid LLM JSON response string is successfully validated
    and converted into a strongly-typed ConsultationAssessment object in state.
    """
    # Arrange: Set up initial state with valid LLM output JSON
    req_id = "req-101"
    valid_payload = {
        "consultationRequestId": req_id,
        "priority": "High",
        "consultationType": "Urgent",
        "keyConcerns": [
            {
                "concern": "Lethargy and vomiting",
                "reason": "Symptoms persisting for more than 24 hours.",
            }
        ],
        "recommendedChecks": [
            "Abdominal palpation",
            "Hydration check",
            "Temperature check",
        ],
        "suggestedNextStep": "Schedule immediate in-clinic triage examination.",
        "disclaimer": "Preliminary AI consultation assessment — requires veterinary review and confirmation.",
    }
    state: AgentState = {
        "consultation_request_id": req_id,
        "auth_token": None,
        "raw_input_data": {},
        "llm_response": json.dumps(valid_payload),
        "assessment": None,
        "retry_count": 0,
        "error": None,
    }

    # Act: Execute the validation node
    result_state = validate_response_node(state)

    # Assert: Validation succeeds and populated structured assessment
    assert result_state["assessment"] is not None
    assert isinstance(result_state["assessment"], ConsultationAssessment)
    assert result_state["assessment"].consultationRequestId == req_id
    assert result_state["assessment"].priority == "High"
    assert result_state["assessment"].consultationType == "Urgent"
    assert len(result_state["assessment"].keyConcerns) == 1
    assert result_state["assessment"].keyConcerns[0].concern == "Lethargy and vomiting"
    assert len(result_state["assessment"].recommendedChecks) == 3
    assert result_state["assessment"].suggestedNextStep == "Schedule immediate in-clinic triage examination."
    assert result_state["assessment"].disclaimer == "Preliminary AI consultation assessment — requires veterinary review and confirmation."
    assert result_state["error"] is None


@pytest.mark.parametrize("valid_priority", ["Low", "Moderate", "High", "Emergency"])
def test_tc_cons_ai_02_priority_assessment_valid_values(valid_priority: str):
    """TC-CONS-AI-02: Priority Assessment (Valid Values).

    Verifies that all defined triage priority values (Low, Moderate, High, Emergency)
    are accepted by ConsultationAssessment.
    """
    # Arrange & Act
    assessment = ConsultationAssessment(
        consultationRequestId="req-001",
        priority=valid_priority,  # type: ignore
        consultationType="Routine",
        suggestedNextStep="Perform routine checkup.",
    )

    # Assert
    assert assessment.priority == valid_priority


def test_tc_cons_ai_02_priority_assessment_invalid_rejected():
    """TC-CONS-AI-02: Priority Assessment (Invalid Value Rejection).

    Verifies that invalid/unsupported priority values raise a Pydantic ValidationError.
    """
    # Arrange, Act & Assert: Invalid priority should be rejected
    with pytest.raises(ValidationError):
        ConsultationAssessment(
            consultationRequestId="req-001",
            priority="Critical",  # Invalid enum value (not in Literal)
            consultationType="Routine",
            suggestedNextStep="Perform checkup.",
        )


@pytest.mark.parametrize("valid_type", ["Routine", "Urgent", "Emergency"])
def test_tc_cons_ai_03_consultation_type_valid_values(valid_type: str):
    """TC-CONS-AI-03: Consultation Type Classification (Valid Values).

    Verifies that valid consultation classification types (Routine, Urgent, Emergency)
    are accepted.
    """
    # Arrange & Act
    assessment = ConsultationAssessment(
        consultationRequestId="req-002",
        priority="Moderate",
        consultationType=valid_type,  # type: ignore
        suggestedNextStep="Triage pet accordingly.",
    )

    # Assert
    assert assessment.consultationType == valid_type


def test_tc_cons_ai_03_consultation_type_invalid_rejected():
    """TC-CONS-AI-03: Consultation Type Classification (Invalid Value Rejection).

    Verifies that non-standard consultation types are rejected by schema validation.
    """
    # Arrange, Act & Assert
    with pytest.raises(ValidationError):
        ConsultationAssessment(
            consultationRequestId="req-002",
            priority="Moderate",
            consultationType="FollowUp",  # Invalid type
            suggestedNextStep="Triage pet accordingly.",
        )


def test_tc_cons_ai_04_key_concerns_validation():
    """TC-CONS-AI-04: Key Concerns Validation.

    Verifies that structured keyConcerns are parsed into KeyConcern model instances
    with concern and reason fields, and malformed entries are rejected.
    """
    # Arrange
    raw_concerns = [
        {"concern": "Reduced appetite", "reason": "Not eating for 2 days."},
        {"concern": "Mild cough", "reason": "Coughing after exercise."},
    ]

    # Act
    assessment = ConsultationAssessment(
        consultationRequestId="req-003",
        priority="Low",
        consultationType="Routine",
        keyConcerns=raw_concerns,  # type: ignore
        suggestedNextStep="Monitor diet and activity level.",
    )

    # Assert
    assert len(assessment.keyConcerns) == 2
    assert all(isinstance(c, KeyConcern) for c in assessment.keyConcerns)
    assert assessment.keyConcerns[0].concern == "Reduced appetite"
    assert assessment.keyConcerns[0].reason == "Not eating for 2 days."
    assert assessment.keyConcerns[1].concern == "Mild cough"

    # Malformed key concern missing 'reason' field must raise ValidationError
    with pytest.raises(ValidationError):
        ConsultationAssessment(
            consultationRequestId="req-003",
            priority="Low",
            consultationType="Routine",
            keyConcerns=[{"concern": "Incomplete concern without reason"}],  # type: ignore
            suggestedNextStep="Step",
        )


def test_tc_cons_ai_05_recommended_checks():
    """TC-CONS-AI-05: Recommended Checks Validation.

    Verifies that recommendedChecks stores and preserves a list of clinical examination items.
    """
    # Arrange
    checks = [
        "Cardiovascular auscultation",
        "Body condition scoring",
        "Dental inspection",
    ]

    # Act
    assessment = ConsultationAssessment(
        consultationRequestId="req-004",
        priority="Moderate",
        consultationType="Routine",
        recommendedChecks=checks,
        suggestedNextStep="Conduct comprehensive physical examination.",
    )

    # Assert
    assert assessment.recommendedChecks == checks
    assert len(assessment.recommendedChecks) == 3
    assert "Cardiovascular auscultation" in assessment.recommendedChecks


def test_tc_cons_ai_06_suggested_next_step():
    """TC-CONS-AI-06: Suggested Next Step.

    Verifies that suggestedNextStep is preserved and correctly holds a non-empty string.
    """
    # Arrange
    next_step = "Veterinarian should perform blood work and assess hydration status before prescribing treatment."

    # Act
    assessment = ConsultationAssessment(
        consultationRequestId="req-005",
        priority="High",
        consultationType="Urgent",
        suggestedNextStep=next_step,
    )

    # Assert
    assert assessment.suggestedNextStep == next_step
    assert len(assessment.suggestedNextStep) > 0


def test_tc_cons_ai_07_human_review_disclaimer_enforcement():
    """TC-CONS-AI-07: Human Review Disclaimer Enforcement.

    Verifies that the Pydantic field_validator enforces the standard human-review disclaimer
    even if an alternative disclaimer is provided in input.
    """
    # Arrange: Provide a custom/deviant disclaimer text
    custom_disclaimer = "AI has completely diagnosed the pet with certainty."
    expected_disclaimer = "Preliminary AI consultation assessment — requires veterinary review and confirmation."

    # Act: Instantiate model with custom disclaimer
    assessment = ConsultationAssessment(
        consultationRequestId="req-006",
        priority="Low",
        consultationType="Routine",
        suggestedNextStep="Routine check.",
        disclaimer=custom_disclaimer,
    )

    # Assert: Validator overwrote it with the required safety disclaimer
    assert assessment.disclaimer == expected_disclaimer


def test_tc_cons_ai_08_responsibility_boundary():
    """TC-CONS-AI-08: Responsibility Boundary.

    Verifies that the ConsultationAssessment model strictly adheres to decision-support boundaries
    and does NOT contain clinical execution, prescription, billing, scheduling, or approval fields.
    """
    # Arrange: Define allowed decision-support fields vs forbidden clinical execution fields
    allowed_fields = {
        "consultationRequestId",
        "priority",
        "consultationType",
        "keyConcerns",
        "recommendedChecks",
        "suggestedNextStep",
        "disclaimer",
    }
    forbidden_fields = {
        "finalDiagnosis",
        "prescriptions",
        "medicationDosage",
        "billingAmount",
        "invoiceId",
        "scheduledTime",
        "approvalStatus",
        "approvedBy",
    }

    # Act: Retrieve model schema fields
    actual_fields = set(ConsultationAssessment.model_fields.keys())

    # Assert: Only allowed fields exist and none of the forbidden fields exist
    assert actual_fields == allowed_fields
    assert actual_fields.isdisjoint(forbidden_fields)


def test_tc_cons_ai_09_invalid_llm_response_and_retry():
    """TC-CONS-AI-09: Invalid LLM Response and Retry State.

    Verifies that validate_response_node handles malformed JSON, records error,
    leaves assessment as None, and increments retry_count.
    """
    # Arrange: Set state with malformed non-JSON output from LLM
    req_id = "req-007"
    state: AgentState = {
        "consultation_request_id": req_id,
        "auth_token": None,
        "raw_input_data": {},
        "llm_response": "I am an AI and I think this pet is sick but I did not return JSON.",
        "assessment": None,
        "retry_count": 0,
        "error": None,
    }

    # Act
    result_state = validate_response_node(state)

    # Assert: Validation failure recorded and retry count incremented
    assert result_state["assessment"] is None
    assert result_state["error"] is not None
    assert result_state["retry_count"] == 1


def test_tc_cons_ai_10_safe_fallback():
    """TC-CONS-AI-10: Safe Fallback Generation.

    Verifies that get_safe_fallback() generates a deterministic, safe, valid ConsultationAssessment
    with Moderate priority, Routine consultationType, empty lists, and veterinary-review next steps.
    """
    # Arrange
    req_id = "req-008"

    # Act
    fallback = get_safe_fallback(req_id)

    # Assert
    assert isinstance(fallback, ConsultationAssessment)
    assert fallback.consultationRequestId == req_id
    assert fallback.priority == "Moderate"
    assert fallback.consultationType == "Routine"
    assert fallback.keyConcerns == []
    assert fallback.recommendedChecks == []
    assert "Veterinarian review recommended" in fallback.suggestedNextStep
    assert "Preliminary AI consultation assessment — requires veterinary review and confirmation." in fallback.disclaimer


# ============================================================================
# ADDITIONAL WORKFLOW & ROUTING TESTS
# ============================================================================


def test_workflow_route_after_validation_success():
    """Workflow Test: route_after_validation returns 'end' when assessment is populated."""
    # Arrange
    state: AgentState = {
        "consultation_request_id": "req-100",
        "auth_token": None,
        "raw_input_data": {},
        "llm_response": "{}",
        "assessment": get_safe_fallback("req-100"),
        "retry_count": 0,
        "error": None,
    }

    # Act
    route = route_after_validation(state)

    # Assert
    assert route == "end"


def test_workflow_route_after_validation_retry():
    """Workflow Test: route_after_validation returns 'retry' when assessment is None and retry_count < 2."""
    # Arrange
    state: AgentState = {
        "consultation_request_id": "req-100",
        "auth_token": None,
        "raw_input_data": {},
        "llm_response": "bad json",
        "assessment": None,
        "retry_count": 1,
        "error": "JSON parse error",
    }

    # Act
    route = route_after_validation(state)

    # Assert
    assert route == "retry"


def test_workflow_route_after_validation_fallback_on_max_retries():
    """Workflow Test: route_after_validation returns 'fallback' when retry_count reaches limit (>= 2)."""
    # Arrange
    state: AgentState = {
        "consultation_request_id": "req-100",
        "auth_token": None,
        "raw_input_data": {},
        "llm_response": "bad json",
        "assessment": None,
        "retry_count": 2,
        "error": "Validation error",
    }

    # Act
    route = route_after_validation(state)

    # Assert
    assert route == "fallback"


def test_workflow_route_after_context_success():
    """Workflow Test: route_after_context returns 'analyze' when no error is present."""
    # Arrange
    state: AgentState = {
        "consultation_request_id": "req-100",
        "auth_token": None,
        "raw_input_data": {"consultation": {"id": "req-100"}},
        "llm_response": None,
        "assessment": None,
        "retry_count": 0,
        "error": None,
    }

    # Act
    route = route_after_context(state)

    # Assert
    assert route == "analyze"


def test_workflow_route_after_context_error():
    """Workflow Test: route_after_context returns 'fallback' when context retrieval records an error."""
    # Arrange
    state: AgentState = {
        "consultation_request_id": "req-100",
        "auth_token": None,
        "raw_input_data": {},
        "llm_response": None,
        "assessment": None,
        "retry_count": 0,
        "error": "Backend service 500 internal error",
    }

    # Act
    route = route_after_context(state)

    # Assert
    assert route == "fallback"


@pytest.mark.asyncio
async def test_workflow_retrieve_context_node_success():
    """Workflow Test: retrieve_context_node fetches consultation and pet history via mocked tools."""
    # Arrange
    req_id = "req-ctx-01"
    state: AgentState = {
        "consultation_request_id": req_id,
        "auth_token": "Bearer test-token",
        "raw_input_data": {},
        "llm_response": None,
        "assessment": None,
        "retry_count": 0,
        "error": None,
    }

    mock_consultation = {"id": req_id, "petId": "pet-99", "chiefComplaint": "Limping"}
    mock_history = [{"id": "exam-1", "diagnosis": "Sprain"}]

    # Act: Patch tools.py async helpers
    with patch("consultation_agent.agent.fetch_consultation_details", new=AsyncMock(return_value=mock_consultation)), \
         patch("consultation_agent.agent.fetch_previous_history", new=AsyncMock(return_value=mock_history)):
        result_state = await retrieve_context_node(state)

    # Assert
    assert result_state["error"] is None
    assert result_state["raw_input_data"]["consultation"] == mock_consultation
    assert result_state["raw_input_data"]["history"] == mock_history


@pytest.mark.asyncio
async def test_workflow_retrieve_context_node_failure():
    """Workflow Test: retrieve_context_node catches tool exceptions and populates state['error']."""
    # Arrange
    req_id = "req-ctx-err"
    state: AgentState = {
        "consultation_request_id": req_id,
        "auth_token": None,
        "raw_input_data": {},
        "llm_response": None,
        "assessment": None,
        "retry_count": 0,
        "error": None,
    }

    # Act: Simulate HTTP connection failure
    with patch("consultation_agent.agent.fetch_consultation_details", new=AsyncMock(side_effect=Exception("Connection refused"))):
        result_state = await retrieve_context_node(state)

    # Assert
    assert result_state["error"] == "Connection refused"


def test_workflow_analyze_request_node_success():
    """Workflow Test: analyze_request_node invokes LLM with formatted prompts and extracts response."""
    # Arrange
    req_id = "req-analyze-01"
    state: AgentState = {
        "consultation_request_id": req_id,
        "auth_token": None,
        "raw_input_data": {"consultation": {"id": req_id}},
        "llm_response": None,
        "assessment": None,
        "retry_count": 0,
        "error": None,
    }

    mock_llm_json = json.dumps({
        "consultationRequestId": req_id,
        "priority": "Moderate",
        "consultationType": "Routine",
        "keyConcerns": [],
        "recommendedChecks": ["Temperature"],
        "suggestedNextStep": "Schedule exam",
        "disclaimer": "Preliminary AI consultation assessment — requires veterinary review and confirmation.",
    })
    mock_msg = MagicMock()
    mock_msg.content = f"```json\n{mock_llm_json}\n```"

    mock_llm = MagicMock()
    mock_llm.invoke.return_value = mock_msg

    # Act: Mock LLM instance
    with patch("consultation_agent.agent.llm", new=mock_llm):
        result_state = analyze_request_node(state)

    # Assert
    assert result_state["error"] is None
    assert result_state["llm_response"] is not None
    assert "Moderate" in result_state["llm_response"]


def test_workflow_fallback_node():
    """Workflow Test: fallback_node assigns safe fallback assessment to state."""
    # Arrange
    req_id = "req-fb-01"
    state: AgentState = {
        "consultation_request_id": req_id,
        "auth_token": None,
        "raw_input_data": {},
        "llm_response": None,
        "assessment": None,
        "retry_count": 2,
        "error": "Max retries exceeded",
    }

    # Act
    result_state = fallback_node(state)

    # Assert
    assert result_state["assessment"] is not None
    assert result_state["assessment"].consultationRequestId == req_id
    assert result_state["assessment"].priority == "Moderate"


@pytest.mark.asyncio
async def test_full_graph_end_to_end_success():
    """End-to-End Graph Test: Full consultation_agent_app execution with mocked context and LLM."""
    # Arrange
    req_id = "req-e2e-01"
    initial_state = {
        "consultation_request_id": req_id,
        "auth_token": "test-token",
        "raw_input_data": {},
        "llm_response": None,
        "assessment": None,
        "retry_count": 0,
        "error": None,
    }

    mock_consultation = {"id": req_id, "petId": "pet-1", "chiefComplaint": "Ear infection"}
    mock_llm_output = json.dumps({
        "consultationRequestId": req_id,
        "priority": "Moderate",
        "consultationType": "Routine",
        "keyConcerns": [{"concern": "Ear scratching", "reason": "Redness observed in left ear."}],
        "recommendedChecks": ["Otoscopic examination"],
        "suggestedNextStep": "Veterinary examination and ear cytology recommended.",
        "disclaimer": "Preliminary AI consultation assessment — requires veterinary review and confirmation.",
    })
    mock_msg = MagicMock()
    mock_msg.content = mock_llm_output

    mock_llm = MagicMock()
    mock_llm.invoke.return_value = mock_msg

    # Act: Run full compiled LangGraph with mocks
    with patch("consultation_agent.agent.fetch_consultation_details", new=AsyncMock(return_value=mock_consultation)), \
         patch("consultation_agent.agent.fetch_previous_history", new=AsyncMock(return_value=[])), \
         patch("consultation_agent.agent.llm", new=mock_llm):
        final_state = await consultation_agent_app.ainvoke(initial_state)

    # Assert
    assert final_state["assessment"] is not None
    assert final_state["assessment"].consultationRequestId == req_id
    assert final_state["assessment"].priority == "Moderate"
    assert len(final_state["assessment"].keyConcerns) == 1
    assert final_state["assessment"].keyConcerns[0].concern == "Ear scratching"


@pytest.mark.asyncio
async def test_full_graph_fallback_on_context_failure():
    """End-to-End Graph Test: Full execution routes to fallback when context retrieval fails."""
    # Arrange
    req_id = "req-e2e-fail"
    initial_state = {
        "consultation_request_id": req_id,
        "auth_token": None,
        "raw_input_data": {},
        "llm_response": None,
        "assessment": None,
        "retry_count": 0,
        "error": None,
    }

    # Act: Simulate failure in fetch_consultation_details
    with patch("consultation_agent.agent.fetch_consultation_details", new=AsyncMock(side_effect=Exception("API Unreachable"))):
        final_state = await consultation_agent_app.ainvoke(initial_state)

    # Assert
    assert final_state["assessment"] is not None
    assert final_state["assessment"].consultationRequestId == req_id
    assert final_state["assessment"].priority == "Moderate"
    assert "Veterinarian review recommended" in final_state["assessment"].suggestedNextStep
