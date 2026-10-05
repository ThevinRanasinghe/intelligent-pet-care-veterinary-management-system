"""Inventory tools: real API fields, pagination, batch retrieval."""
import pytest
import respx

from inventory_agent.tools import (
    check_stock_availability,
    fetch_all_medicines,
    fetch_medicine_batches,
    fetch_prescription_items,
    get_available_quantity,
)

BASE = "http://petcare.test/api"


@pytest.mark.asyncio
async def test_medicine_pagination_fetches_all_pages():
    page1 = {"items": [{"id": f"m{i}", "availableQuantity": 5} for i in range(200)],
             "total": 250, "page": 1, "pageSize": 200}
    page2 = {"items": [{"id": f"m{i}", "availableQuantity": 5} for i in range(200, 250)],
             "total": 250, "page": 2, "pageSize": 200}
    with respx.mock(base_url=BASE) as mock:
        mock.get("/medicines", params={"page": 1, "pageSize": 200}).respond(200, json=page1)
        mock.get("/medicines", params={"page": 2, "pageSize": 200}).respond(200, json=page2)
        meds = await fetch_all_medicines("Bearer t")
    assert len(meds) == 250


@pytest.mark.asyncio
async def test_available_quantity_field_used():
    meds = [{"id": "m1", "availableQuantity": 7, "totalQuantity": 10, "reservedQuantity": 3}]
    assert get_available_quantity(meds, "m1") == 7
    assert check_stock_availability(meds, "m1", 7) is True
    assert check_stock_availability(meds, "m1", 8) is False
    assert get_available_quantity(meds, "missing") == 0


@pytest.mark.asyncio
async def test_prescription_items_404_returns_empty():
    with respx.mock(base_url=BASE) as mock:
        mock.get("/prescriptions/treatment/tr1").respond(404)
        assert await fetch_prescription_items("tr1") == []


@pytest.mark.asyncio
async def test_fetch_batches_returns_real_data():
    batches = [{"id": "b1", "batchNumber": "BN-1", "quantity": 40,
                "expiryDate": "2027-01-01", "isExpired": False}]
    with respx.mock(base_url=BASE) as mock:
        mock.get("/medicines/m1/batches").respond(200, json=batches)
        assert (await fetch_medicine_batches("m1"))[0]["batchNumber"] == "BN-1"
