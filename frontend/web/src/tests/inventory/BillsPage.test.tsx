import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { render } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { BillsPage } from '../../features/inventory/BillsPage';
import { AuthProvider } from '../../features/auth/AuthContext';
import RoleRoute from '../../features/auth/components/RoleRoute';
import { UnauthorizedPage } from '../../features/shared/UnauthorizedPage';
import { renderWithAuth, seedAuth } from '../testUtils';
import type { Role } from '../../types/domain';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const bill = {
  id: 'q-1',
  invoiceNumber: 'INV-AAAA0001',
  appointmentId: 'appt-1',
  budget: 2800,
  subtotal: 2800,
  total: 2800,
  isWithinBudget: true,
  status: 'Finalised',
  paymentStatus: 'Pending',
  paidAt: null,
  appointmentDate: '2026-09-26',
  petId: 'pet-1',
  petName: 'Shadow',
  ownerId: 'own-1',
  ownerName: 'Amal',
  ownerEmail: 'amal@x.lk',
  ownerPhone: '077',
  veterinarianId: 'vet-1',
  veterinarianName: 'Dr. Silva',
  examinationId: 'exam-1',
  examinationDate: '2026-09-26T10:05:00',
  veterinarianChargeTotal: 2500,
  medicineTotal: 300,
  items: [
    { id: 'li-1', category: 'Examination', description: 'Veterinarian charge — Dr. Silva', quantity: 1, unitPrice: 2500, totalPrice: 2500 },
    { id: 'li-2', category: 'Medicine', description: 'Amoxicillin (1 pill)', quantity: 3, unitPrice: 100, totalPrice: 300 },
  ],
  createdAt: '2026-09-26T10:00:00Z',
  updatedAt: '2026-09-26T10:00:00Z',
};

function stubFetch(paidResult?: unknown) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';
    if (method === 'POST' && url.endsWith('/mark-paid')) {
      return jsonResponse(paidResult ?? { ...bill, paymentStatus: 'Paid', paidAt: '2026-09-27T00:00:00Z' });
    }
    if (url.endsWith('/quotations')) return jsonResponse([bill]);
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

/** Renders through the same RoleRoute guard used in AppRoutes. */
function renderBillsRoute(role: Role) {
  seedAuth(role);
  return render(
    <MemoryRouter initialEntries={['/inventory/bills']}>
      <AuthProvider>
        <Routes>
          <Route path="/inventory/bills" element={
            <RoleRoute allowedRoles={['InventoryOfficer', 'Administrator']}><BillsPage /></RoleRoute>
          } />
          <Route path="/unauthorized" element={<UnauthorizedPage />} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('BillsPage', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('renders invoice, pet, owner, vet, totals and payment status', async () => {
    stubFetch();
    renderWithAuth(<BillsPage />, { role: 'InventoryOfficer' });

    expect(await screen.findByText('INV-AAAA0001')).toBeInTheDocument();
    expect(screen.getByText('Shadow')).toBeInTheDocument();
    expect(screen.getByText('Amal')).toBeInTheDocument();
    expect(screen.getByText('Dr. Silva')).toBeInTheDocument();
    // The Pending badge (the filter <select> also has a Pending option).
    expect(screen.getAllByText('Pending').length).toBeGreaterThanOrEqual(2);
  });

  it('Mark as Paid POSTs /quotations/{id}/mark-paid and the badge becomes Paid', async () => {
    const fetchMock = stubFetch();
    renderWithAuth(<BillsPage />, { role: 'InventoryOfficer' });

    await userEvent.click(await screen.findByRole('button', { name: 'Details' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Mark as Paid' }));

    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith('/quotations/q-1/mark-paid') && (init as RequestInit)?.method === 'POST')).toBe(true);
    });
    // Detail view updates to Paid (row updated after closing detail).
    await userEvent.click(await screen.findByRole('button', { name: 'Close' }));
    expect(screen.getAllByText('Paid').length).toBeGreaterThan(0);
  });

  it('route-gates Veterinarians out of the page', () => {
    stubFetch();
    renderBillsRoute('Veterinarian');

    expect(screen.queryByText('Bills & Payments')).not.toBeInTheDocument();
    expect(screen.getByText(/access denied/i)).toBeInTheDocument();
  });
});
