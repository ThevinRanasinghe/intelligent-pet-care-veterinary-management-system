"""FastAPI surface: health probe and internal-key authentication."""
import pytest
from fastapi.testclient import TestClient


@pytest.fixture
def client():
    import main

    return TestClient(main.app)


class _FakeGraph:
    """Minimal stand-in for a compiled LangGraph app."""

    async def ainvoke(self, state):
        from consultation_agent.models import ConsultationAssessment

        state["assessment"] = ConsultationAssessment(
            consultationRequestId=state["consultation_request_id"],
            priority="Low",
            consultationType="Routine",
            keyConcerns=[],
            recommendedChecks=[],
            suggestedNextStep="ok",
        )
        return state


def test_health_is_public(client):
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.json()["status"] == "ok"


def test_agent_endpoint_requires_internal_key(client):
    resp = client.post("/api/agents/consultation-analysis/c1")
    assert resp.status_code == 401


def test_wrong_internal_key_rejected(client):
    resp = client.post(
        "/api/agents/consultation-analysis/c1",
        headers={"X-Internal-Key": "wrong"},
    )
    assert resp.status_code == 401


def test_valid_internal_key_invokes_agent(client, monkeypatch):
    import main

    monkeypatch.setattr(main, "consultation_agent_app", _FakeGraph())
    resp = client.post(
        "/api/agents/consultation-analysis/c1",
        headers={"X-Internal-Key": "test-internal-key"},
    )
    assert resp.status_code == 200
    body = resp.json()
    assert body["consultationRequestId"] == "c1"
    assert "requires veterinary review" in body["disclaimer"]


def test_missing_internal_key_config_returns_503(client, monkeypatch):
    monkeypatch.delenv("AGENTIC_INTERNAL_KEY", raising=False)
    resp = client.post(
        "/api/agents/consultation-analysis/c1",
        headers={"X-Internal-Key": "test-internal-key"},
    )
    assert resp.status_code == 503


class _FakeInventoryGraph:
    """Minimal stand-in for the compiled inventory LangGraph app."""

    async def ainvoke(self, state):
        from inventory_agent.models import InventoryAssessment, InventorySummary

        state["assessment"] = InventoryAssessment(
            requestId=state["treatment_record_id"],
            inventorySummary=InventorySummary(
                medicineFound=True,
                stockAvailable=True,
                sufficientQuantity=True,
                batchAvailable=True,
                notExpired=True,
                lowStock=False,
            ),
            confidence="High",
            planningNotes="Issue from the earliest valid batch.",
        )
        return state


def test_inventory_endpoint_requires_internal_key(client):
    resp = client.post("/api/agents/inventory-planning/tr-1")
    assert resp.status_code == 401


def test_inventory_endpoint_uses_treatment_record_id(client, monkeypatch):
    """The inventory route keys the assessment by the TreatmentRecordId."""
    import main

    monkeypatch.setattr(main, "inventory_agent_app", _FakeInventoryGraph())
    resp = client.post(
        "/api/agents/inventory-planning/tr-42",
        headers={"X-Internal-Key": "test-internal-key"},
    )
    assert resp.status_code == 200
    body = resp.json()
    assert body["requestId"] == "tr-42"
    assert "authorized staff review" in body["disclaimer"]
