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

function stubFetch(issueResult?: { body: unknown; ok?: boolean; status?: number }, afterIssueStatus = 'Issued', requests: typeof pendingRequest[] = [pendingRequest]) {
  const issuedIds = new Set<string>();
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';
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
});
