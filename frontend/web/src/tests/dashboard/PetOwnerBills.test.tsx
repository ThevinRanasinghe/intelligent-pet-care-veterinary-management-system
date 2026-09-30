import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PetOwnerDashboard } from '../../features/dashboard/PetOwnerDashboard';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const bill = {
  id: 'q-1',
  invoiceNumber: 'INV-AAAA0001',
  appointmentId: 'appt-1',
  budget: 3400,
  subtotal: 3400,
  total: 3400,
  isWithinBudget: true,
  status: 'Finalised',
  paymentStatus: 'Paid',
  paidAt: '2026-09-27T00:00:00Z',
  appointmentDate: '2026-09-26',
  appointmentStartTime: '10:00:00',
  appointmentEndTime: '11:00:00',
  clinicName: 'PetCare Colombo 10',
  petId: 'pet-1',
  petName: 'Shadow',
  ownerId: 'own-1',
  ownerName: 'Amal',
  veterinarianId: 'vet-1',
  veterinarianName: 'Dr. Silva',
  examinationId: 'exam-1',
  examinationDate: '2026-09-26T10:15:00Z',
  veterinarianChargeTotal: 2500,
  medicineTotal: 900,
  items: [
    { id: 'li-1', category: 'Examination', description: 'Veterinarian charge — Dr. Silva', quantity: 1, unitPrice: 2500, totalPrice: 2500 },
    { id: 'li-2', category: 'Medicine', description: 'Amoxicillin (1 tablet)', quantity: 6, unitPrice: 150, totalPrice: 900 },
  ],
  medications: [
    {
      id: 'rx-1',
      prescriptionNumber: 'RX-00000001',
      treatmentRecordId: 'tr-1',
      medicineId: 'med-1',
      medicineName: 'Amoxicillin',
      medicineStrength: '250 mg',
      medicineDosageForm: 'Tablet',
      medicineUnitPrice: 150,
      quantity: 6,
      dosage: '1 tablet',
      frequency: 'twice daily',
      route: 'Oral',
      durationDays: 5,
      instructions: 'Give after food',
      requestStatus: 'Issued',
      veterinarianName: 'Dr. Silva',
      createdAt: '2026-09-26T10:20:00Z',
    },
    {
      id: 'rx-2',
      prescriptionNumber: 'RX-00000002',
      treatmentRecordId: 'tr-1',
      medicineId: 'med-2',
      medicineName: 'Meloxicam',
      medicineStrength: '1.5 mg',
      medicineDosageForm: 'Tablet',
      quantity: 5,
      dosage: '1 tablet',
      frequency: 'once daily',
      route: 'Oral',
      durationDays: 3,
      instructions: 'With breakfast',
      requestStatus: 'Unavailable',
      unavailableReason: 'Out of stock — reorder placed',
      veterinarianName: 'Dr. Silva',
      createdAt: '2026-09-26T10:20:00Z',
    },
  ],
  createdAt: '2026-09-26T10:00:00Z',
  updatedAt: '2026-09-27T00:00:00Z',
};

function stubFetch() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes('/appointments/mine')) {
      return jsonResponse([
        {
          id: 'appt-1', petId: 'pet-1', veterinarianId: 'vet-1', appointmentSlotId: 's1',
          scheduledStart: '2026-10-01T10:00:00', scheduledEnd: '2026-10-01T10:30:00',
          status: 'Confirmed', type: 'FollowUp', petName: 'Shadow', veterinarianName: 'Dr. Silva',
          createdAt: '', updatedAt: '',
        },
      ]);
    }
    if (url.includes('/quotations/mine')) return jsonResponse([bill]);
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('PetOwnerDashboard — appointments & bills', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('shows upcoming appointments and bills with a Paid badge', async () => {
    stubFetch();
    renderWithAuth(<PetOwnerDashboard />, { role: 'PetOwner' });

    expect(await screen.findByText('INV-AAAA0001')).toBeInTheDocument();
    expect(screen.getByText('Paid')).toBeInTheDocument();
    expect(screen.getByText('My Appointments')).toBeInTheDocument();
    expect(screen.getByText('My Bills')).toBeInTheDocument();
    expect(screen.getAllByText('Shadow').length).toBeGreaterThan(0);
    expect(screen.getByText(/Follow-up/)).toBeInTheDocument();
  });

  it('opens a complete bill detail: invoice, consultation, charges, medications, payment', async () => {
    stubFetch();
    renderWithAuth(<PetOwnerDashboard />, { role: 'PetOwner' });

    await userEvent.click(await screen.findByText('INV-AAAA0001'));
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    // Invoice + payment
    expect(await screen.findAllByText('INV-AAAA0001')).not.toHaveLength(0);
    expect(screen.getAllByText('Paid').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText(/Paid on:/)).toBeInTheDocument();

    // Consultation details — no internal GUIDs shown
    expect(screen.getAllByText('Shadow').length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Dr\. Silva/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/PetCare Colombo 10/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/10:00/).length).toBeGreaterThan(0);
    expect(screen.queryByText('appt-1')).not.toBeInTheDocument();
    expect(screen.queryByText('q-1')).not.toBeInTheDocument();

    // Bill summary
    expect(screen.getByText(/Veterinarian charge — Dr\. Silva/)).toBeInTheDocument();
    expect(screen.getByText(/Amoxicillin \(1 tablet\)/)).toBeInTheDocument();
    expect(screen.getByText(/Veterinarian charge:/)).toBeInTheDocument();
    expect(screen.getByText(/Total:/)).toBeInTheDocument();

    // Prescription & medication instructions — vet-recorded data
    expect(screen.getByText('PRESCRIPTION & MEDICATION INSTRUCTIONS')).toBeInTheDocument();
    expect(screen.getByText(/Prescribed by:/)).toBeInTheDocument();
    expect(screen.getByText(/Amoxicillin 250 mg \(Tablet\)/)).toBeInTheDocument();
    expect(screen.getAllByText('Oral').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('twice daily')).toBeInTheDocument();
    expect(screen.getByText('Give after food')).toBeInTheDocument();
    expect(screen.getByText(/Meloxicam 1\.5 mg \(Tablet\)/)).toBeInTheDocument();
    expect(screen.getByText(/Out of stock — reorder placed/)).toBeInTheDocument();
    expect(screen.getAllByText('Issued').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Unavailable').length).toBeGreaterThanOrEqual(1);

    // No payment action for owners.
    expect(screen.queryByRole('button', { name: /mark as paid/i })).not.toBeInTheDocument();
  });
});
