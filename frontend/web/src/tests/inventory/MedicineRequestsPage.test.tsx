import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MedicineRequestsPage } from '../../features/inventory/MedicineRequestsPage';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const pendingRequest = {
  id: 'rx-1',
  treatmentRecordId: 'tr-1',
  medicineId: 'med-1',
  dosage: '1 pill',
  durationDays: 7,
  quantity: 14,
  frequency: 'twice daily',
  instructions: null,
  requestStatus: 'Pending',
  unavailableReason: null,
  reservationId: null,
  processedAt: null,
  medicineName: 'Amoxicillin',
  medicineUnitPrice: 100,
  petId: 'pet-1',
  petName: 'Shadow',
  ownerName: 'Amal',
  veterinarianId: 'vet-1',
  veterinarianName: 'Dr. Silva',
  examinationId: 'exam-1',
  appointmentId: 'appt-1',
  veterinarianCharge: 2500,
  createdAt: '2026-09-26T00:00:00Z',
};

const aiPlan = {
  source: 'agentic-ai',
  requestId: 'tr-1',
  medicineRecommendation: {
    medicineId: 'med-1',
    medicineName: 'Amoxicillin',
    requiredQuantity: 14,
    availableQuantity: 40,
    sufficientStock: true,
    reason: 'Requested item is in stock',
  },
  recommendedBatch: {
    batchId: 'b1',
    batchNumber: 'BN-100',
    quantityAvailable: 40,
    expiryDate: '2027-06-01',
    expiryStatus: 'Valid',
  },
  alternativeMedicines: [
    { medicineId: 'med-9', medicineName: 'Doxycycline', availableQuantity: 12, reason: 'In stock if needed' },
  ],
  inventorySummary: {
    medicineFound: true,
    stockAvailable: true,
    sufficientQuantity: true,
    batchAvailable: true,
    notExpired: true,
    lowStock: false,
  },
  confidence: 'High',
  planningNotes: 'Issue from the earliest valid batch.',
  disclaimer: 'AI-generated medicine and inventory recommendation — deterministic backend validation and authorized staff review are required.',
};

function stubFetch(
  issueResult?: { body: unknown; ok?: boolean; status?: number },
  afterIssueStatus = 'Issued',
  requests: typeof pendingRequest[] = [pendingRequest],
  planResult?: { body: unknown; ok?: boolean; status?: number },
) {
  const issuedIds = new Set<string>();
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';
    if (url.includes('/inventory-plan')) {
      if (planResult && !(planResult.ok ?? true)) {
        return jsonResponse(planResult.body, planResult.ok ?? true, planResult.status ?? 500);
      }
      return jsonResponse(planResult?.body ?? aiPlan);
    }
    const issueMatch = url.match(/\/prescriptions\/(rx-[\w-]+)\/issue$/);
    if (method === 'POST' && issueMatch) {
      issuedIds.add(issueMatch[1]);
      const item = requests.find((r) => r.id === issueMatch[1]) ?? pendingRequest;
      return jsonResponse(issueResult?.body ?? { ...item, requestStatus: afterIssueStatus }, issueResult?.ok ?? true, issueResult?.status ?? 200);
    }
    if (method === 'POST' && url.endsWith('/unavailable')) {
      return jsonResponse({ ...pendingRequest, requestStatus: 'Unavailable', unavailableReason: 'Out of stock' });
    }
    if (url.includes('/prescriptions/requests')) {
      // Reload after a successful issue reflects the new status.
      return jsonResponse(requests.map((r) => ({ ...r, requestStatus: issuedIds.has(r.id) && (issueResult?.ok ?? true) ? afterIssueStatus : 'Pending' })));
    }
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('MedicineRequestsPage', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });
  afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); localStorage.clear(); });

  it('renders the Pending list with denormalised fields', async () => {
    stubFetch();
    renderWithAuth(<MedicineRequestsPage />, { role: 'InventoryOfficer' });

    expect(await screen.findByText(/Amoxicillin/)).toBeInTheDocument();
    expect(screen.getByText('Shadow')).toBeInTheDocument();
    expect(screen.getByText(/Dr\. Silva/)).toBeInTheDocument();
    expect(screen.getByText(/× 14/)).toBeInTheDocument();
    // The Pending badge (the status filter <select> has a Pending option too).
    expect(screen.getAllByText('Pending').length).toBeGreaterThanOrEqual(2);
  });

  it('Issue POSTs /prescriptions/{id}/issue and the row becomes Issued', async () => {
    const fetchMock = stubFetch();
    renderWithAuth(<MedicineRequestsPage />, { role: 'InventoryOfficer' });

    await userEvent.click(await screen.findByRole('button', { name: 'Issue' }));

    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith('/prescriptions/rx-1/issue') && (init as RequestInit)?.method === 'POST')).toBe(true);
    });
    await waitFor(() => expect(screen.getAllByText('Issued').length).toBeGreaterThanOrEqual(2));
    expect(screen.getByText(/issued and dispensed/i)).toBeInTheDocument();
  });

  it('a 409 keeps the request Pending and shows the server message', async () => {
    stubFetch({ body: { message: 'Insufficient stock — only 2 units available.' }, ok: false, status: 409 });
    renderWithAuth(<MedicineRequestsPage />, { role: 'InventoryOfficer' });

    await userEvent.click(await screen.findByRole('button', { name: 'Issue' }));

    expect(await screen.findByText(/Insufficient stock/)).toBeInTheDocument();
    // Still Pending (badge + filter option).
    expect(screen.getAllByText('Pending').length).toBeGreaterThanOrEqual(2);
  });

  it('shows a multi-medicine request as one card and processes items independently', async () => {
    const secondItem = {
      ...pendingRequest,
      id: 'rx-2',
      medicineId: 'med-2',
      medicineName: 'Meloxicam',
      quantity: 3,
      dosage: '2 pills',
    };
    const fetchMock = stubFetch(undefined, 'Issued', [pendingRequest, secondItem]);
    renderWithAuth(<MedicineRequestsPage />, { role: 'InventoryOfficer' });

    // Both items of the same request appear together under one pet heading.
    expect(await screen.findByText(/Amoxicillin/)).toBeInTheDocument();
    expect(screen.getByText(/Meloxicam/)).toBeInTheDocument();
    expect(screen.getByText(/2 medicines/)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Issue' })).toHaveLength(2);

    // Issue only the second item — the first stays Pending.
    await userEvent.click(screen.getAllByRole('button', { name: 'Issue' })[1]);
    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith('/prescriptions/rx-2/issue') && (init as RequestInit)?.method === 'POST')).toBe(true);
    });
    await waitFor(() => expect(screen.getAllByText('Issued').length).toBeGreaterThanOrEqual(1));
    expect(screen.getAllByText('Pending').length).toBeGreaterThanOrEqual(2);
  });

  it('Mark Unavailable requires a reason and POSTs it', async () => {
    const fetchMock = stubFetch();
    renderWithAuth(<MedicineRequestsPage />, { role: 'InventoryOfficer' });

    await userEvent.click(await screen.findByRole('button', { name: 'Mark Unavailable' }));
    await userEvent.click(screen.getByRole('button', { name: 'Confirm unavailable' }));
    expect(await screen.findByText(/reason is required/i)).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText('Unavailable reason'), 'Out of stock');
    await userEvent.click(screen.getByRole('button', { name: 'Confirm unavailable' }));

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/prescriptions/rx-1/unavailable') && (init as RequestInit)?.method === 'POST');
      expect(call).toBeTruthy();
      expect(JSON.parse(String((call![1] as RequestInit).body))).toEqual({ reason: 'Out of stock' });
    });
  });

  // --- Advisory AI inventory analysis -------------------------------------

  it('Inventory Officer sees the AI Inventory Analysis control', async () => {
    stubFetch();
    renderWithAuth(<MedicineRequestsPage />, { role: 'InventoryOfficer' });

    await screen.findByText(/Amoxicillin/);
    expect(screen.getByRole('button', { name: /AI Inventory Analysis/i })).toBeInTheDocument();
  });

  it('non-processing roles do not see the AI control', async () => {
    stubFetch();
    renderWithAuth(<MedicineRequestsPage />, { role: 'Veterinarian' });

    await screen.findByText(/Amoxicillin/);
    expect(screen.queryByRole('button', { name: /AI Inventory Analysis/i })).not.toBeInTheDocument();
  });

  it('clicking the control requests the plan for the treatmentRecordId and renders it', async () => {
    const fetchMock = stubFetch();
    renderWithAuth(<MedicineRequestsPage />, { role: 'InventoryOfficer' });

    await userEvent.click(await screen.findByRole('button', { name: /AI Inventory Analysis/i }));

    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url, init]) =>
        String(url).endsWith('/prescriptions/treatment/tr-1/inventory-plan')
        && ((init as RequestInit)?.method ?? 'GET') === 'GET')).toBe(true);
    });
    // Advisory content renders — recommendation, batch, checks, notes.
    expect(await screen.findByText(/sufficient stock/i)).toBeInTheDocument();
    expect(screen.getByText(/BN-100/)).toBeInTheDocument();
    expect(screen.getByText(/Issue from the earliest valid batch/)).toBeInTheDocument();
    // Advisory disclaimer is visible.
    expect(screen.getByText(/deterministic backend validation and authorized staff review/i)).toBeInTheDocument();
    // Alternatives are shown as informational only, not applied.
    expect(screen.getByText(/veterinarian approval required/i)).toBeInTheDocument();
    expect(screen.getByText(/Doxycycline/)).toBeInTheDocument();
    // No mutation request was made by the analysis itself.
    expect(fetchMock.mock.calls.some(([, init]) => (init as RequestInit)?.method === 'POST')).toBe(false);
  });

  it('an unavailable response renders safely and Issue still works', async () => {
    stubFetch(undefined, 'Issued', [pendingRequest], {
      body: { source: 'unavailable', requestId: 'tr-1', alternativeMedicines: [], inventorySummary: {}, confidence: 'Low', planningNotes: 'AI inventory analysis is currently unavailable — process the medicine request normally.', disclaimer: 'AI-generated medicine and inventory recommendation' },
    });
    renderWithAuth(<MedicineRequestsPage />, { role: 'InventoryOfficer' });

    await userEvent.click(await screen.findByRole('button', { name: /AI Inventory Analysis/i }));

    expect(await screen.findByText(/currently unavailable/i)).toBeInTheDocument();
    // Manual processing is unaffected.
    await userEvent.click(screen.getByRole('button', { name: 'Issue' }));
    expect(await screen.findByText(/issued and dispensed/i)).toBeInTheDocument();
  });

  it('a failed analysis call falls back to the unavailable state', async () => {
    stubFetch(undefined, 'Issued', [pendingRequest], { body: {}, ok: false, status: 500 });
    renderWithAuth(<MedicineRequestsPage />, { role: 'InventoryOfficer' });

    await userEvent.click(await screen.findByRole('button', { name: /AI Inventory Analysis/i }));

    expect(await screen.findByText(/currently unavailable/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Issue' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Mark Unavailable' })).toBeEnabled();
  });

  it('AI state is keyed per treatment record — a second group shows no foreign plan', async () => {
    const otherOrgItem = { ...pendingRequest, id: 'rx-9', treatmentRecordId: 'tr-2', medicineName: 'Meloxicam' };
    stubFetch(undefined, 'Issued', [pendingRequest, otherOrgItem]);
    renderWithAuth(<MedicineRequestsPage />, { role: 'InventoryOfficer' });

    await screen.findByText(/Amoxicillin/);
    await userEvent.click(screen.getAllByRole('button', { name: /AI Inventory Analysis/i })[0]);

    expect(await screen.findByTestId('ai-plan-tr-1')).toBeInTheDocument();
    // The second request group never received an analysis panel.
    expect(screen.queryByTestId('ai-plan-tr-2')).not.toBeInTheDocument();
  });
});
