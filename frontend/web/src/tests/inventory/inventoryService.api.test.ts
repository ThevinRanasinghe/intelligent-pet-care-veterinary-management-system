import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../services/api';
import {
  cancelReservation,
  createMedicine,
  createSupplier,
  dispenseReservation,
  getBatches,
  getExpiring,
  getLowStock,
  getMedicineById,
  getMedicines,
  getSuppliers,
  getTransactions,
  receiveStock,
  reserveMedicine,
} from '../../services/inventoryService';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return {
    ok,
    status,
    text: async () => (body === undefined ? '' : JSON.stringify(body)),
  } as Response;
}

describe('inventoryService API client', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('GETs paginated medicines with query params', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({
        items: [
          {
            id: 'm-1',
            name: 'Amoxicillin',
            category: 'Antibiotics',
            totalQuantity: 100,
            reservedQuantity: 20,
            availableQuantity: 80,
          },
        ],
        total: 1,
        page: 1,
        pageSize: 10,
      })
    );

    const result = await getMedicines({ search: 'Amox', category: 'Antibiotics', page: 1 });

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/medicines?search=Amox&category=Antibiotics&page=1'),
      expect.any(Object)
    );
    expect(result.items).toHaveLength(1);
    expect(result.items[0].name).toBe('Amoxicillin');
  });

  it('GETs medicine by id', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({
        id: 'm-1',
        name: 'Amoxicillin',
        unitPrice: 150,
      })
    );

    const result = await getMedicineById('m-1');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/medicines/m-1'),
      expect.any(Object)
    );
    expect(result.id).toBe('m-1');
  });

  it('POSTs a new medicine to /medicines', async () => {
    const medData = {
      name: 'Meloxicam',
      category: 'Anti-inflammatory',
      description: 'Pain relief',
      dosageForm: 'Oral Suspension',
      strength: '1.5mg/ml',
      unitPrice: 1200,
      manufacturer: 'Boehringer',
      reorderLevel: 5,
    };
    fetchMock.mockResolvedValueOnce(jsonResponse({ id: 'm-2', ...medData }, true, 201));

    const result = await createMedicine(medData);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/medicines');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body as string)).toMatchObject(medData);
    expect(result.id).toBe('m-2');
  });

  it('POSTs stock-in consignment to /medicines/{id}/stock-in', async () => {
    const batchData = {
      supplierId: 's-1',
      batchNumber: 'LOT-999',
      quantity: 50,
      expiryDate: '2026-12-31',
    };
    fetchMock.mockResolvedValueOnce(jsonResponse({ id: 'b-1', ...batchData }));

    const result = await receiveStock('m-1', batchData);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/medicines/m-1/stock-in');
    expect(init.method).toBe('POST');
    expect(result.batchNumber).toBe('LOT-999');
  });

  it('GETs batches for a medicine', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse([{ id: 'b-1', batchNumber: 'LOT-1', quantity: 20 }])
    );

    const batches = await getBatches('m-1');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/medicines/m-1/batches'),
      expect.any(Object)
    );
    expect(batches).toHaveLength(1);
  });

  it('GETs low stock and expiring items', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([{ id: 'm-low', name: 'Low Med' }]));
    const lowStock = await getLowStock();
    expect(lowStock).toHaveLength(1);

    fetchMock.mockResolvedValueOnce(jsonResponse([{ id: 'b-exp', batchNumber: 'EXP-1' }]));
    const expiring = await getExpiring(14);
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/medicines/expiring?withinDays=14'),
      expect.any(Object)
    );
    expect(expiring).toHaveLength(1);
  });

  it('POSTs reservation to /medicine-reservations', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({ id: 'res-1', medicineId: 'm-1', quantity: 5, status: 'Reserved' }, true, 201)
    );

    const result = await reserveMedicine({
      medicineId: 'm-1',
      quantity: 5,
      referenceType: 'Treatment',
    });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/medicine-reservations');
    expect(init.method).toBe('POST');
    expect(result.id).toBe('res-1');
  });

  it('POSTs dispense and cancel for reservation', async () => {
    fetchMock.mockResolvedValueOnce({ ok: true, status: 204, text: async () => '' } as Response);
    await dispenseReservation('res-1');
    expect(fetchMock.mock.calls[0][0]).toContain('/medicine-reservations/res-1/dispense');

    fetchMock.mockResolvedValueOnce({ ok: true, status: 204, text: async () => '' } as Response);
    await cancelReservation('res-1');
    expect(fetchMock.mock.calls[1][0]).toContain('/medicine-reservations/res-1/cancel');
  });

  it('GETs and POSTs suppliers', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse([{ id: 's-1', name: 'Supplier One', status: 'Active' }])
    );
    const list = await getSuppliers();
    expect(list).toHaveLength(1);

    const newSup = {
      name: 'MediSupply',
      contactPerson: 'Perera',
      phone: '0112345678',
      email: 'p@medisupply.com',
      address: 'Colombo',
    };
    fetchMock.mockResolvedValueOnce(jsonResponse({ id: 's-2', ...newSup, status: 'Active' }, true, 201));
    const created = await createSupplier(newSup);
    expect(created.id).toBe('s-2');
  });

  it('propagates ApiError on 409 conflict during reservation', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({ detail: 'Insufficient available stock for medicine' }, false, 409)
    );

    await expect(
      reserveMedicine({ medicineId: 'm-1', quantity: 100 })
    ).rejects.toBeInstanceOf(ApiError);
  });
});
