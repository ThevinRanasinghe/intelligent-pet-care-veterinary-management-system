import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { VeterinarianHistoryPage } from '../../features/manager/VeterinarianHistoryPage';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const history = {
  veterinarian: { id: 'vet-1', name: 'Dr. Silva', specialisation: 'General' },
  appointments: {
    completedCount: 3,
    upcomingCount: 1,
    cancelledCount: 1,
    items: [
      {
        id: 'appt-1', petId: 'pet-1', veterinarianId: 'vet-1', appointmentSlotId: 's1',
        scheduledStart: '2026-09-20T10:00:00', scheduledEnd: '2026-09-20T10:30:00',
        status: 'Completed', type: 'Initial', petName: 'Shadow', ownerName: 'Amal',
        createdAt: '', updatedAt: '',
      },
    ],
  },
  examinations: {
    total: 3,
    initialCount: 2,
    followUpCount: 1,
    items: [
      { id: 'exam-1', petId: 'pet-1', veterinarianId: 'vet-1', symptoms: 'Fever', notes: '', examinationDate: '2026-09-20T10:05:00', veterinarianCharge: 2500, createdAt: '' },
    ],
  },
  prescriptions: {
    total: 2,
    items: [
      { id: 'rx-1', treatmentRecordId: 'tr-1', medicineId: 'med-1', dosage: '1 pill', durationDays: 7, quantity: 14, requestStatus: 'Issued', medicineName: 'Amoxicillin', petName: 'Shadow', createdAt: '2026-09-20T10:10:00' },
    ],
  },
  medicineRequests: { pending: 1, issued: 1, unavailable: 0 },
  bills: {
    total: 2,
    paidCount: 1,
    pendingCount: 1,
    vetChargeTotal: 5000,
    medicineTotal: 1400,
    grandTotal: 6400,
    items: [
      {
        id: 'q-1', invoiceNumber: 'INV-AAAA0001', appointmentId: 'appt-1', budget: 6400, subtotal: 6400, total: 6400,
        isWithinBudget: true, status: 'Finalised', paymentStatus: 'Paid', paidAt: '2026-09-21T00:00:00Z',
        petName: 'Shadow', ownerName: 'Amal', veterinarianChargeTotal: 5000, medicineTotal: 1400,
        items: [], createdAt: '', updatedAt: '',
      },
    ],
  },
};

function stubFetch() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes('/history')) return jsonResponse(history);
    if (url.endsWith('/manager/veterinarians')) {
      return jsonResponse([
        { id: 'vet-1', name: 'Dr. Silva', specialisation: 'General', branch: 'Colombo', active: true, userId: 'u-1' },
      ]);
    }
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('VeterinarianHistoryPage', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('fetches the vet history and renders counts plus a bill row', async () => {
    const fetchMock = stubFetch();
    renderWithAuth(<VeterinarianHistoryPage />, { role: 'ClinicManager' });

    await userEvent.selectOptions(await screen.findByLabelText('Veterinarian'), 'vet-1');
    await userEvent.click(screen.getByRole('button', { name: /load history/i }));

    const historyCall = fetchMock.mock.calls.find(([url]) => String(url).includes('/manager/veterinarians/vet-1/history'));
    expect(historyCall).toBeTruthy();

    expect(await screen.findByText('INV-AAAA0001')).toBeInTheDocument();
    expect(screen.getByText(/3 completed · 1 upcoming · 1 cancelled/)).toBeInTheDocument();
    expect(screen.getByText(/3 total · 2 initial · 1 follow-up/)).toBeInTheDocument();
    expect(screen.getByText(/2 prescriptions · 1 pending · 1 issued · 0 unavailable/)).toBeInTheDocument();
    expect(screen.getByText(/2 bills · 1 paid · 1 pending/)).toBeInTheDocument();
    expect(screen.getAllByText('Shadow').length).toBeGreaterThan(0);
  });
});
