import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../services/api';
import {
  calculateQuotation,
  getQuotations,
  submitQuotationForApproval,
  updateQuotation,
} from '../../services/billingService';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return {
    ok,
    status,
    text: async () => (body === undefined ? '' : JSON.stringify(body)),
  } as Response;
}

const quotationResponse = {
  id: 'quo-1',
  appointmentId: 'appt-1',
  budget: 10000,
  subtotal: 4500,
  total: 4500,
  isWithinBudget: true,
  status: 'Draft',
  items: [
    { id: 'li-1', category: 'Consultation', description: 'General checkup', quantity: 1, unitPrice: 3500, totalPrice: 3500 },
  ],
  createdAt: '2026-01-01T00:00:00',
  updatedAt: '2026-01-01T00:00:00',
};

describe('billingService API integration', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('GETs quotations and maps them to domain shape', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([quotationResponse]));

    const quotes = await getQuotations();

    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/quotations'), expect.any(Object));
    expect(quotes).toHaveLength(1);
    expect(quotes[0]).toMatchObject({ id: 'quo-1', total: 4500, budget: 10000, status: 'Draft' });
  });

  it('PUTs an update to a quotation and returns the updated record', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ ...quotationResponse, budget: 12000 }));

    const updated = await updateQuotation('quo-1', {
      budget: 12000,
      items: [{ category: 'Consultation', description: 'General checkup', quantity: 1, unitPrice: 3500 }],
    });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/quotations/quo-1');
    expect(init.method).toBe('PUT');
    expect(updated.budget).toBe(12000);
  });

  it('POSTs to calculate a quotation and returns the backend total', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ ...quotationResponse, total: 5000 }));

    const result = await calculateQuotation('quo-1');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/quotations/quo-1/calculate');
    expect(init.method).toBe('POST');
    expect(result.total).toBe(5000);
  });

  it('POSTs to submit a quotation for approval', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ ...quotationResponse, status: 'PendingApproval' }));

    const result = await submitQuotationForApproval('quo-1');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/quotations/quo-1/submit');
    expect(init.method).toBe('POST');
    expect(result.status).toBe('PendingApproval');
  });

  it('throws an ApiError with the backend detail on a 400 validation failure', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ detail: 'Budget must be a positive number' }, false, 400));

    await expect(updateQuotation('quo-1', { budget: -1, items: [] })).rejects.toBeInstanceOf(ApiError);
  });

  it('throws an ApiError on a 409 conflict when submitting for approval', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ detail: 'Quotation already submitted' }, false, 409));

    await expect(submitQuotationForApproval('quo-1')).rejects.toMatchObject({ status: 409 });
  });
});
