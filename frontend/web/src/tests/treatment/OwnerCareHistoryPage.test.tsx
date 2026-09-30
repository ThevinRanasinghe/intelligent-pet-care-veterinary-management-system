import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OwnerCareHistoryPage } from '../../features/treatment/OwnerCareHistoryPage';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const owner = { id: 'own-1', email: 'manager@petcare.lk', fullName: 'Amal' };
const pet = { id: 'pet-1', name: 'Shadow', species: 'Dog', breed: 'Labrador', ownerId: 'own-1' };
const exam = {
  id: 'exam-1', petId: 'pet-1', veterinarianId: 'vet-1', appointmentId: 'appt-1',
  symptoms: 'Vomiting', notes: '', veterinarianCharge: 2500,
  examinationDate: '2026-09-26T10:15:00Z', createdAt: '2026-09-26T10:15:00Z',
};
const diagnosis = { id: 'diag-1', examinationId: 'exam-1', conditionName: 'Gastritis', description: '', severity: 'Moderate', createdAt: '2026-09-26T10:20:00Z' };
const treatment = { id: 'tr-1', diagnosisId: 'diag-1', procedureName: 'Bland diet + medication', notes: '', status: 'InProgress', createdAt: '', updatedAt: '' };
const prescriptions = [
  {
    id: 'rx-1', prescriptionNumber: 'RX-0000AA11', treatmentRecordId: 'tr-1', medicineId: 'med-1',
    medicineName: 'Amoxicillin', medicineStrength: '250 mg', medicineDosageForm: 'Tablet',
    quantity: 10, dosage: '1 tablet', frequency: 'twice daily', route: 'Oral', durationDays: 5,
    instructions: 'Give after food', requestStatus: 'Issued', veterinarianName: 'Dr. Silva',
    createdAt: '2026-09-26T10:25:00Z',
  },
  {
    id: 'rx-2', prescriptionNumber: 'RX-0000BB22', treatmentRecordId: 'tr-1', medicineId: 'med-2',
    medicineName: 'Meloxicam', medicineStrength: '1.5 mg', medicineDosageForm: 'Tablet',
    quantity: 5, dosage: '1 tablet', frequency: 'once daily', route: 'Oral', durationDays: 3,
    instructions: 'With breakfast', requestStatus: 'Unavailable', unavailableReason: 'Out of stock',
    veterinarianName: 'Dr. Silva', createdAt: '2026-09-26T10:25:00Z',
  },
];
const bill = {
  id: 'q-1', invoiceNumber: 'INV-AAAA0001', appointmentId: 'appt-1', budget: 4000,
  subtotal: 4000, total: 4000, isWithinBudget: true, status: 'Finalised', paymentStatus: 'Paid',
  paidAt: '2026-09-27T00:00:00Z', appointmentDate: '2026-09-26', appointmentStartTime: '10:00:00',
  appointmentEndTime: '11:00:00', clinicName: 'PetCare Colombo 10', petId: 'pet-1', petName: 'Shadow',
  ownerId: 'own-1', ownerName: 'Amal', veterinarianId: 'vet-1', veterinarianName: 'Dr. Silva',
  examinationId: 'exam-1', examinationDate: '2026-09-26T10:15:00Z',
  veterinarianChargeTotal: 2500, medicineTotal: 1500,
  items: [
    { id: 'li-1', category: 'Examination', description: 'Veterinarian charge — Dr. Silva', quantity: 1, unitPrice: 2500, totalPrice: 2500 },
    { id: 'li-2', category: 'Medicine', description: 'Amoxicillin (1 tablet)', quantity: 10, unitPrice: 150, totalPrice: 1500 },
  ],
  medications: prescriptions,
  createdAt: '2026-09-26T10:00:00Z', updatedAt: '2026-09-27T00:00:00Z',
};

function stubFetch() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.endsWith('/owners')) return jsonResponse([owner]);
    if (url.includes('/pets/owner/')) return jsonResponse([pet]);
    if (url.includes('/examinations/pet/')) return jsonResponse([exam]);
    if (url.includes('/diagnoses/examination/')) return jsonResponse([diagnosis]);
    if (url.includes('/treatmentrecords/diagnosis/')) return jsonResponse([treatment]);
    if (url.includes('/prescriptions/treatment/')) return jsonResponse(prescriptions);
    if (url.includes('/quotations/mine')) return jsonResponse([bill]);
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('OwnerCareHistoryPage — prescription & bill linkage', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('shows complete per-medicine prescription instructions and statuses', async () => {
    stubFetch();
    renderWithAuth(<OwnerCareHistoryPage />, { role: 'PetOwner' });

    // Medication instruction cards
    expect(await screen.findByText(/Amoxicillin 250 mg \(Tablet\)/)).toBeInTheDocument();
    expect(screen.getByText(/Meloxicam 1\.5 mg \(Tablet\)/)).toBeInTheDocument();
    expect(screen.getByText('twice daily')).toBeInTheDocument();
    expect(screen.getAllByText('Oral').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Give after food')).toBeInTheDocument();
    expect(screen.getByText('With breakfast')).toBeInTheDocument();
    expect(screen.getAllByText('Issued').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Unavailable').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText(/Out of stock/)).toBeInTheDocument();
    expect(screen.getByText('RX-0000AA11')).toBeInTheDocument();
    // Prescriber + date shown on the prescription section
    expect(screen.getByText(/Dr\. Silva/)).toBeInTheDocument();
    // No raw GUIDs rendered
    expect(screen.queryByText('tr-1')).not.toBeInTheDocument();
    expect(screen.queryByText('rx-1')).not.toBeInTheDocument();
  });

  it('links the prescription to its invoice and opens the full bill', async () => {
    stubFetch();
    renderWithAuth(<OwnerCareHistoryPage />, { role: 'PetOwner' });

    expect(await screen.findByText('Amoxicillin 250 mg (Tablet)')).toBeInTheDocument();
    expect(screen.getByText('INV-AAAA0001')).toBeInTheDocument();
    expect(screen.getByText(/Paid/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'View bill' }));
    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    expect(screen.getAllByText('INV-AAAA0001').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('PRESCRIPTION & MEDICATION INSTRUCTIONS')).toBeInTheDocument();
    expect(screen.getByText(/Total:/)).toBeInTheDocument();

    // The modal offers a visible Close action (footer) in addition to the header X.
    const closeButtons = screen.getAllByRole('button', { name: 'Close' });
    await userEvent.click(closeButtons[closeButtons.length - 1]);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
