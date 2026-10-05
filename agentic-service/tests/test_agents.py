"""End-to-end graph behaviour with a fake LLM and stubbed backend tools.

Covers: malformed output -> safe fallback, schema validation, scheduling
fabrication rejection, inventory batch fabrication rejection, and backend
failure -> safe fallback.
"""
from datetime import date, timedelta

import pytest

import consultation_agent.agent as consultation
import inventory_agent.agent as inventory
import scheduling_agent.agent as scheduling
import shared.llm as shared_llm


class _FakeResponse:
    def __init__(self, content):
        self.content = content


class FakeLLM:
    """Stand-in for ChatGoogleGenerativeAI returning canned content."""

    def __init__(self, content):
        self._content = content
        self.calls = 0

    async def ainvoke(self, messages):
        self.calls += 1
        return _FakeResponse(self._content)


def _state(base):
    return {
        **base,
        "auth_token": "Bearer test",
        "raw_input_data": {},
        "llm_response": None,
        "assessment": None,
        "retry_count": 0,
        "error": None,
    }


def _use_fake_llm(monkeypatch, content):
    monkeypatch.setattr(shared_llm, "_llm", FakeLLM(content))


# --- consultation agent -----------------------------------------------------

@pytest.mark.asyncio
async def test_malformed_llm_output_falls_back(monkeypatch):
    async def fake_consultation(cid, token=None):
        return {"id": cid, "petId": "p1", "symptoms": "cough"}

    async def fake_history(pid, token=None):
        return []

    monkeypatch.setattr(consultation, "fetch_consultation_details", fake_consultation)
    monkeypatch.setattr(consultation, "fetch_previous_history", fake_history)
    _use_fake_llm(monkeypatch, "this is not json at all")

    result = await consultation.consultation_agent_app.ainvoke(
        _state({"consultation_request_id": "c1"})
    )
    assessment = result["assessment"]
    assert assessment is not None
    assert "could not be generated reliably" in assessment.disclaimer.lower() \
        or "requires veterinary review" in assessment.disclaimer.lower()


@pytest.mark.asyncio
async def test_valid_llm_output_validates(monkeypatch):
    async def fake_consultation(cid, token=None):
        return {"id": cid, "petId": "p1"}

    async def fake_history(pid, token=None):
        return []

    monkeypatch.setattr(consultation, "fetch_consultation_details", fake_consultation)
    monkeypatch.setattr(consultation, "fetch_previous_history", fake_history)
    _use_fake_llm(monkeypatch, """
        {"consultationRequestId":"x","priority":"High","consultationType":"Urgent",
         "keyConcerns":[{"concern":"vomiting","reason":"3 days"}],
         "recommendedChecks":["exam"],"suggestedNextStep":"see vet",
         "disclaimer":"whatever"}
    """)

    result = await consultation.consultation_agent_app.ainvoke(
        _state({"consultation_request_id": "c9"})
    )
    assert result["assessment"].priority == "High"
    # Disclaimer is enforced regardless of LLM output
    assert "requires veterinary review" in result["assessment"].disclaimer
    # The request id is enforced, not trusted from the model
    assert result["assessment"].consultationRequestId == "c9"


@pytest.mark.asyncio
async def test_backend_401_falls_back_safely(monkeypatch):
    from shared.backend import BackendApiError

    async def fail(cid, token=None):
        raise BackendApiError("unauthorized", 401, "unauthorized")

    monkeypatch.setattr(consultation, "fetch_consultation_details", fail)
    result = await consultation.consultation_agent_app.ainvoke(
        _state({"consultation_request_id": "c1"})
    )
    # Backend auth failure -> deterministic safe fallback, not a crash.
    assert result["assessment"] is not None
    assert "requires veterinary review" in result["assessment"].disclaimer


# --- scheduling agent -------------------------------------------------------

@pytest.mark.asyncio
async def test_fabricated_slot_is_rejected(monkeypatch):
    target_day = (date.today() + timedelta(days=2)).isoformat()

    async def fake_consultation(rid, token=None):
        return {"id": rid, "preferredDate": f"{target_day}T10:00:00"}

    async def fake_slots(token=None, veterinarian_id=None, date=None):
        return [{"id": "real-slot", "veterinarianId": "v1",
                 "date": target_day, "startTime": "10:00", "endTime": "11:00",
                 "branch": "Colombo", "status": "Available"}]

    async def fake_conflict(vet, date, start, end, token=None):
        return False

    monkeypatch.setattr(scheduling, "fetch_consultation_request", fake_consultation)
    monkeypatch.setattr(scheduling, "fetch_available_slots", fake_slots)
    monkeypatch.setattr(scheduling, "check_conflict", fake_conflict)
    _use_fake_llm(monkeypatch, """
        {"requestId":"r1",
         "recommendedAppointment":{"appointmentSlotId":"hallucinated-slot",
            "veterinarianId":"v1","date":"2026-01-05","startTime":"10:00",
            "endTime":"11:00","branch":"Colombo","reason":"x"},
         "alternativeSlots":[],
         "quotationProposal":{"budget":0,"items":[],"estimatedSubtotal":0,
            "estimatedTotal":0,"withinBudget":true},
         "validationSummary":{"slotFound":true,"veterinarianAvailable":true,
            "noKnownConflict":true,"withinRequestedTime":true,"withinBudget":true},
         "confidence":"High","planningNotes":"n","disclaimer":"d"}
    """)

    result = await scheduling.scheduling_agent_app.ainvoke(_state({"request_id": "r1"}))
    # The LLM's slot choices are discarded entirely — the deterministic
    # selection is authoritative, so a hallucinated id can never survive.
    rec = result["assessment"].recommendedAppointment
    assert rec is not None
    assert rec.appointmentSlotId == "real-slot"
    assert rec.slotIds == ["real-slot"]


@pytest.mark.asyncio
async def test_conflict_check_failure_is_not_treated_as_free(monkeypatch):
    from shared.backend import BackendApiError

    target_day = (date.today() + timedelta(days=2)).isoformat()

    async def fake_consultation(rid, token=None):
        return {"id": rid, "preferredDate": f"{target_day}T10:00:00"}

    async def fake_slots(token=None, veterinarian_id=None, date=None):
        return [{"id": "real-slot", "veterinarianId": "v1",
                 "date": target_day, "startTime": "10:00", "endTime": "11:00",
                 "branch": "Colombo", "status": "Available"}]

    async def failing_conflict(vet, date, start, end, token=None):
        raise BackendApiError("timeout", kind="timeout")

    monkeypatch.setattr(scheduling, "fetch_consultation_request", fake_consultation)
    monkeypatch.setattr(scheduling, "fetch_available_slots", fake_slots)
    monkeypatch.setattr(scheduling, "check_conflict", failing_conflict)
    _use_fake_llm(monkeypatch, """
        {"requestId":"r1",
         "recommendedAppointment":{"appointmentSlotId":"real-slot",
            "veterinarianId":"v1","date":"2026-01-05","startTime":"10:00",
            "endTime":"11:00","branch":"Colombo","reason":"x"},
         "alternativeSlots":[],"quotationProposal":null,
         "validationSummary":{"slotFound":true,"veterinarianAvailable":true,
            "noKnownConflict":true,"withinRequestedTime":true,"withinBudget":true},
         "confidence":"High","planningNotes":"n","disclaimer":"d"}
    """)

    result = await scheduling.scheduling_agent_app.ainvoke(_state({"request_id": "r1"}))
    # Failed conflict check -> safe fallback, never "no conflict".
    assert result["assessment"].validationSummary.noKnownConflict is False
    assert result["assessment"].confidence == "Low"


@pytest.mark.asyncio
async def test_rate_limit_is_not_retried(monkeypatch):
    """A 429/quota error can never recover inside one request — the graph
    must take the fast-path to the fallback after a single LLM call
    instead of burning the shared quota on doomed retries."""

    class GoogleRateLimitError(Exception):
        pass

    class RateLimitedLLM:
        def __init__(self):
            self.calls = 0

        async def ainvoke(self, messages):
            self.calls += 1
            raise GoogleRateLimitError("429 RESOURCE_EXHAUSTED quota")

    target_day = (date.today() + timedelta(days=2)).isoformat()

    async def fake_consultation(rid, token=None):
        return {"id": rid, "preferredDate": f"{target_day}T10:00:00"}

    async def fake_slots(token=None, veterinarian_id=None, date=None):
        # A real slot keeps the deterministic selector on the LLM path —
        # with no usable window the graph short-circuits to the
        # no-proposal node without ever calling the model.
        return [{"id": "real-slot", "veterinarianId": "v1",
                 "date": target_day, "startTime": "10:00", "endTime": "11:00",
                 "branch": "Colombo", "status": "Available"}]

    llm = RateLimitedLLM()
    monkeypatch.setattr(scheduling, "fetch_consultation_request", fake_consultation)
    monkeypatch.setattr(scheduling, "fetch_available_slots", fake_slots)
    monkeypatch.setattr(shared_llm, "_llm", llm)

    result = await scheduling.scheduling_agent_app.ainvoke(_state({"request_id": "r1"}))
    assert llm.calls == 1  # fast-path — no wasted retries on a quota error
    assert result["assessment"] is not None
    assert result["assessment"].confidence == "Low"


@pytest.mark.asyncio
async def test_empty_slots_skip_llm_entirely(monkeypatch):
    """When the deterministic search finds no window, the LLM is never
    invoked — a no-proposal outcome needs no model call (saves quota)."""

    class CountingLLM:
        def __init__(self):
            self.calls = 0

        async def ainvoke(self, messages):
            self.calls += 1
            return _FakeResponse("{}")

    async def fake_consultation(rid, token=None):
        return {"id": rid, "preferredDate": "2099-01-05T10:00:00"}

    async def fake_slots(token=None, veterinarian_id=None, date=None):
        return []

    llm = CountingLLM()
    monkeypatch.setattr(scheduling, "fetch_consultation_request", fake_consultation)
    monkeypatch.setattr(scheduling, "fetch_available_slots", fake_slots)
    monkeypatch.setattr(shared_llm, "_llm", llm)

    result = await scheduling.scheduling_agent_app.ainvoke(_state({"request_id": "r1"}))
    assert llm.calls == 0
    assert result["assessment"] is not None
    assert result["assessment"].recommendedAppointment is None
    assert result["assessment"].reasonCode == "NO_VALID_SLOT"


# --- inventory agent --------------------------------------------------------

_INVENTORY_LLM = """
    {"requestId":"tr1",
     "medicineRecommendation":{"medicineId":"m1","medicineName":"Amoxicillin",
        "requiredQuantity":10,"availableQuantity":999,"sufficientStock":true,
        "reason":"x"},
     "recommendedBatch":{"batchId":"fake-batch","batchNumber":"INVENTED",
        "quantityAvailable":999,"expiryDate":"2099-01-01","expiryStatus":"Valid"},
     "alternativeMedicines":[],
     "inventorySummary":{"medicineFound":true,"stockAvailable":true,
        "sufficientQuantity":true,"batchAvailable":true,"notExpired":true,
        "lowStock":false},
     "confidence":"High","planningNotes":"n","disclaimer":"d"}
"""


def _stub_inventory_context(monkeypatch):
    async def fake_record(tid, token=None):
        return {"id": tid, "treatmentPlan": "antibiotics"}

    async def fake_items(tid, token=None):
        return [{"id": "rx1", "medicineId": "m1", "quantity": 10}]

    async def fake_medicines(token=None):
        return [{"id": "m1", "name": "Amoxicillin", "availableQuantity": 25,
                 "isLowStock": False}]

    async def fake_batches(mid, token=None):
        return [{"id": "real-batch", "batchNumber": "BN-100", "quantity": 40,
                 "expiryDate": "2027-06-01", "isExpired": False}]

    async def fake_low_stock(token=None):
        return []

    monkeypatch.setattr(inventory, "fetch_treatment_record", fake_record)
    monkeypatch.setattr(inventory, "fetch_prescription_items", fake_items)
    monkeypatch.setattr(inventory, "fetch_all_medicines", fake_medicines)
    monkeypatch.setattr(inventory, "fetch_medicine_batches", fake_batches)
    monkeypatch.setattr(inventory, "fetch_low_stock_medicines", fake_low_stock)


@pytest.mark.asyncio
async def test_fabricated_batch_is_rejected(monkeypatch):
    _stub_inventory_context(monkeypatch)
    _use_fake_llm(monkeypatch, _INVENTORY_LLM)

    result = await inventory.inventory_agent_app.ainvoke(
        _state({"treatment_record_id": "tr1"})
    )
    # LLM invented batchId "fake-batch" and fake numbers — the run must
    # retry then fall back; fabricated batch data must never pass through.
    assessment = result["assessment"]
    assert assessment.recommendedBatch is None
    assert assessment.confidence == "Low"


@pytest.mark.asyncio
async def test_real_batch_accepted_and_facts_overwritten(monkeypatch):
    _stub_inventory_context(monkeypatch)
    # LLM picks the real batchId but lies about quantity/expiry —
    # validation must overwrite with real values.
    _use_fake_llm(monkeypatch, """
        {"requestId":"tr1",
         "medicineRecommendation":{"medicineId":"m1","medicineName":"wrong-name",
            "requiredQuantity":10,"availableQuantity":999,"sufficientStock":true,
            "reason":"x"},
         "recommendedBatch":{"batchId":"real-batch","batchNumber":"LIE",
            "quantityAvailable":9999,"expiryDate":"2099-12-31","expiryStatus":"Valid"},
         "alternativeMedicines":[],
         "inventorySummary":{"medicineFound":true,"stockAvailable":true,
            "sufficientQuantity":true,"batchAvailable":true,"notExpired":true,
            "lowStock":true},
         "confidence":"High","planningNotes":"n","disclaimer":"d"}
    """)

    result = await inventory.inventory_agent_app.ainvoke(
        _state({"treatment_record_id": "tr1"})
    )
    assessment = result["assessment"]
    assert assessment.recommendedBatch.batchNumber == "BN-100"
    assert assessment.recommendedBatch.quantityAvailable == 40
    assert assessment.recommendedBatch.expiryDate == "2027-06-01"
    assert assessment.recommendedBatch.expiryStatus == "Valid"
    assert assessment.medicineRecommendation.availableQuantity == 25
    assert assessment.medicineRecommendation.medicineName == "Amoxicillin"
    assert assessment.inventorySummary.lowStock is False
