import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MyAppointmentsPage } from '../../features/vet/MyAppointmentsPage';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const today = new Date().toISOString().slice(0, 10);
const yesterday = new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
const nextWeek = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);

const appt = (overrides: Record<string, unknown>) => ({
  id: 'appt-1',
  petId: 'pet-1',
  veterinarianId: 'vet-1',
  appointmentSlotId: 'slot-1',
  scheduledStart: `${today}T10:00:00`,
  scheduledEnd: `${today}T10:30:00`,
  status: 'Confirmed',
  type: 'Initial',
  petName: 'Shadow',
  ownerName: 'Amal',
  veterinarianName: 'Dr. Silva',
  symptoms: 'Limping on left leg',
  notes: null,
  consultationRequestId: 'CON-0001',
  examinationId: null,
  createdAt: '',
  updatedAt: '',
  ...overrides,
});

/** All nine fixed slots free for every vet. */
function freeDayAvailability(date: string) {
  const starts = ['09:00', '10:00', '11:00', '12:00', '13:00', '14:00', '15:00', '16:00', '17:00'];
  return {
    date,
    isPast: false,
    slots: starts.map((start, i) => ({
      start,
      end: `${String(9 + i + 1).padStart(2, '0')}:00`,
      available: true,
      availableVeterinarianIds: ['vet-1'],
    })),
  };
}

/** Every day of the requested month marked available (not past). */
function monthAvailability(url: string) {
  const params = new URL(url).searchParams;
  const year = Number(params.get('year'));
  const month = Number(params.get('month'));
  const daysInMonth = new Date(year, month, 0).getDate();
  return Array.from({ length: daysInMonth }, (_, i) => {
    const day = String(i + 1).padStart(2, '0');
    const m = String(month).padStart(2, '0');
    return { date: `${year}-${m}-${day}`, available: true, fullyBooked: false, isPast: false };
  });
}

function stubFetch(handlers: { appointments?: unknown; medicines?: unknown; post?: (url: string, body: unknown) => unknown } = {}) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';
    if (method === 'POST' && handlers.post) {
      return jsonResponse(handlers.post(url, init?.body ? JSON.parse(String(init.body)) : undefined));
    }
    if (url.includes('/consultations/availability/month')) return jsonResponse(monthAvailability(url));
    if (url.includes('/consultations/availability')) {
      const date = new URL(url).searchParams.get('date') ?? '';
      return jsonResponse(freeDayAvailability(date));
    }
    if (url.includes('/appointments/mine')) return jsonResponse(handlers.appointments ?? []);
    if (url.includes('/lookups/medicines')) return jsonResponse(handlers.medicines ?? [{ id: 'med-1', name: 'Amoxicillin' }]);
    if (url.includes('/diagnoses/examination/')) return jsonResponse([{ id: 'diag-1', examinationId: 'exam-1', conditionName: 'Infection', description: '', severity: 'Moderate', createdAt: '' }]);
    if (url.includes('/treatmentrecords/diagnosis/')) return jsonResponse([{ id: 'tr-1', diagnosisId: 'diag-1', procedureName: 'Medication', notes: '', status: 'Planned', createdAt: '', updatedAt: '' }]);
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('MyAppointmentsPage', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('renders appointments from /appointments/mine', async () => {
    stubFetch({ appointments: [appt({})] });
    renderWithAuth(<MyAppointmentsPage />, { role: 'Veterinarian' });

    expect(await screen.findByText('Shadow')).toBeInTheDocument();
    expect(screen.getByText('Amal')).toBeInTheDocument();
    expect(screen.getByText('Limping on left leg')).toBeInTheDocument();
  });

  it('shows a loading state then filters rows with the chips', async () => {
    stubFetch({
      appointments: [
        appt({ id: 'a-today', petName: 'TodayPet' }),
        appt({ id: 'a-future', petName: 'FuturePet', scheduledStart: `${nextWeek}T10:00:00`, scheduledEnd: `${nextWeek}T10:30:00`, status: 'Confirmed' }),
        appt({ id: 'a-done', petName: 'DonePet', scheduledStart: `${yesterday}T10:00:00`, scheduledEnd: `${yesterday}T10:30:00`, status: 'Completed', examinationId: 'exam-9' }),
      ],
    });
    renderWithAuth(<MyAppointmentsPage />, { role: 'Veterinarian' });

    // All (default chip): all three visible
    expect(await screen.findByText('TodayPet')).toBeInTheDocument();
    expect(screen.getByText('FuturePet')).toBeInTheDocument();
    expect(screen.getByText('DonePet')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Today' }));
    expect(screen.getByText('TodayPet')).toBeInTheDocument();
    expect(screen.queryByText('FuturePet')).not.toBeInTheDocument();
    expect(screen.queryByText('DonePet')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Upcoming' }));
    expect(screen.getByText('TodayPet')).toBeInTheDocument();
    expect(screen.getByText('FuturePet')).toBeInTheDocument();
    expect(screen.queryByText('DonePet')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Completed' }));
    expect(screen.getByText('DonePet')).toBeInTheDocument();
    expect(screen.queryByText('TodayPet')).not.toBeInTheDocument();
  });

  it('shows an error message when the list fails to load', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => {
      throw new Error('network down');
    }));
    renderWithAuth(<MyAppointmentsPage />, { role: 'Veterinarian' });
    expect(await screen.findByText(/network down/)).toBeInTheDocument();
  });

  it('Record Examination POSTs with appointmentId and veterinarianCharge', async () => {
    const fetchMock = stubFetch({
      appointments: [appt({})],
      post: (url) => {
        if (url.endsWith('/examinations')) return { id: 'exam-1' };
        if (url.endsWith('/diagnoses')) return { id: 'diag-1' };
        return { id: 'tr-1' };
      },
    });
    renderWithAuth(<MyAppointmentsPage />, { role: 'Veterinarian' });

    await userEvent.click(await screen.findByRole('button', { name: 'Open' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Record Examination' }));

    // Symptoms are prefilled from the consultation.
    expect(screen.getByLabelText('Symptoms')).toHaveValue('Limping on left leg');
    fireEvent.change(screen.getByLabelText('Veterinarian charge'), { target: { value: '2500' } });
    await userEvent.type(screen.getByLabelText('Diagnosis condition'), 'Sprain');
    await userEvent.type(screen.getByLabelText('Treatment procedure'), 'Bandage');
    await userEvent.click(screen.getByRole('button', { name: 'Save examination' }));

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(
        ([url, init]) => String(url).endsWith('/examinations') && (init as RequestInit)?.method === 'POST',
      );
      expect(call).toBeTruthy();
      const body = JSON.parse(String((call![1] as RequestInit).body));
      expect(body).toMatchObject({
        appointmentId: 'appt-1',
        veterinarianCharge: 2500,
        symptoms: 'Limping on left leg',
        petId: 'pet-1',
        veterinarianId: 'vet-1',
        consultationRequestId: 'CON-0001',
      });
    });
    expect(await screen.findByText(/appointment is now completed/i)).toBeInTheDocument();
  });

  it('sends a medicine request with quantity and shows it as Pending', async () => {
    const fetchMock = stubFetch({
      appointments: [appt({ status: 'Completed', examinationId: 'exam-1' })],
      post: (url) => {
        if (url.endsWith('/prescriptions')) {
          return [{ id: 'rx-1', medicineId: 'med-1', medicineName: 'Amoxicillin', quantity: 14, requestStatus: 'Pending' }];
        }
        return {};
      },
    });
    renderWithAuth(<MyAppointmentsPage />, { role: 'Veterinarian' });

    await userEvent.click(await screen.findByRole('button', { name: 'Open' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Add Prescription / Medicine Request' }));

    await screen.findByRole('option', { name: 'Amoxicillin' });
    await userEvent.selectOptions(screen.getByLabelText('Medicine 1'), 'med-1');
    fireEvent.change(screen.getByLabelText('Quantity 1'), { target: { value: '14' } });
    await userEvent.type(screen.getByLabelText('Dosage 1'), '1 pill');
    await userEvent.click(screen.getByRole('button', { name: 'Send medicine request' }));

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(
        ([url, init]) => String(url).endsWith('/prescriptions') && (init as RequestInit)?.method === 'POST',
      );
      expect(call).toBeTruthy();
      expect(JSON.parse(String((call![1] as RequestInit).body))).toMatchObject({
        treatmentRecordId: 'tr-1',
        items: [{ medicineId: 'med-1', quantity: 14 }],
      });
    });
    expect(await screen.findByText(/sent to the inventory officer/i)).toBeInTheDocument();
  });

  it('adds a second medicine row, prevents duplicate selection, and submits both in one request', async () => {
    const fetchMock = stubFetch({
      appointments: [appt({ status: 'Completed', examinationId: 'exam-1' })],
      medicines: [{ id: 'med-1', name: 'Amoxicillin' }, { id: 'med-2', name: 'Meloxicam' }],
      post: (url) => (url.endsWith('/prescriptions')
        ? [
            { id: 'rx-1', medicineId: 'med-1', medicineName: 'Amoxicillin', quantity: 2, requestStatus: 'Pending' },
            { id: 'rx-2', medicineId: 'med-2', medicineName: 'Meloxicam', quantity: 1, requestStatus: 'Pending' },
          ]
        : {}),
    });
    renderWithAuth(<MyAppointmentsPage />, { role: 'Veterinarian' });

    await userEvent.click(await screen.findByRole('button', { name: 'Open' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Add Prescription / Medicine Request' }));

    // One row initially
    expect(screen.getByLabelText('Medicine 1')).toBeInTheDocument();
    expect(screen.queryByLabelText('Medicine 2')).not.toBeInTheDocument();

    // Add a second row
    await userEvent.click(screen.getByRole('button', { name: '+ Add medicine' }));
    expect(screen.getByLabelText('Medicine 2')).toBeInTheDocument();
    expect(screen.getByText('2 / 10 medicines')).toBeInTheDocument();

    // Pick med-1 in row 1 — it disappears from row 2's options
    await userEvent.selectOptions(screen.getByLabelText('Medicine 1'), 'med-1');
    await userEvent.type(screen.getByLabelText('Dosage 1'), '1 pill');
    const row2 = screen.getByLabelText('Medicine 2');
    expect(row2.querySelector('option[value="med-1"]')).not.toBeInTheDocument();

    // Fill row 2 with the other medicine and submit — both items go in one POST
    await userEvent.selectOptions(row2, 'med-2');
    await userEvent.type(screen.getByLabelText('Dosage 2'), '2 pills');
    await userEvent.click(screen.getByRole('button', { name: 'Send medicine request' }));

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(
        ([url, init]) => String(url).endsWith('/prescriptions') && (init as RequestInit)?.method === 'POST',
      );
      expect(call).toBeTruthy();
      const body = JSON.parse(String((call![1] as RequestInit).body));
      expect(body.items).toHaveLength(2);
      expect(body.items.map((i: { medicineId: string }) => i.medicineId)).toEqual(['med-1', 'med-2']);
    });
  });

  it('removes a medicine row and cannot exceed 10 rows', async () => {
    stubFetch({ appointments: [appt({ status: 'Completed', examinationId: 'exam-1' })] });
    renderWithAuth(<MyAppointmentsPage />, { role: 'Veterinarian' });

    await userEvent.click(await screen.findByRole('button', { name: 'Open' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Add Prescription / Medicine Request' }));

    await userEvent.click(screen.getByRole('button', { name: '+ Add medicine' }));
    expect(screen.getByLabelText('Medicine 2')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Remove medicine 2' }));
    expect(screen.queryByLabelText('Medicine 2')).not.toBeInTheDocument();

    for (let i = 0; i < 9; i++) {
      await userEvent.click(screen.getByRole('button', { name: '+ Add medicine' }));
    }
    expect(screen.getByText('10 / 10 medicines')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '+ Add medicine' })).toBeDisabled();
  });

  it('Request Follow-up POSTs to /consultations/follow-up with a fixed one-hour slot', async () => {
    const fetchMock = stubFetch({
      appointments: [appt({ status: 'Completed', examinationId: 'exam-1' })],
      post: (url) => (url.endsWith('/consultations/follow-up') ? { id: 'CON-0002' } : {}),
    });
    renderWithAuth(<MyAppointmentsPage />, { role: 'Veterinarian', organizationId: 'org-9' });

    await userEvent.click(await screen.findByRole('button', { name: 'Open' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Request Follow-up' }));

    // Pick any available day in the calendar, then a fixed slot.
    const calendar = await screen.findByTestId('booking-calendar');
    const dayButton = Array.from(calendar.querySelectorAll<HTMLButtonElement>('.booking-day'))
      .find((button) => !button.disabled && button.getAttribute('aria-label'));
    expect(dayButton).toBeTruthy();
    const pickedDate = dayButton!.getAttribute('aria-label')!;
    await userEvent.click(dayButton!);

    await userEvent.click(await screen.findByRole('button', { name: '10:00 AM – 11:00 AM' }));
    await userEvent.type(screen.getByLabelText('Reason'), 'Recheck healing');
    await userEvent.click(screen.getByRole('button', { name: 'Send follow-up request' }));

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(
        ([url, init]) => String(url).endsWith('/consultations/follow-up') && (init as RequestInit)?.method === 'POST',
      );
      expect(call).toBeTruthy();
      const body = JSON.parse(String((call![1] as RequestInit).body));
      expect(body.petId).toBe('pet-1');
      expect(body.examinationId).toBe('exam-1');
      expect(body.reason).toBe('Recheck healing');
      expect(body.preferredDate).toBe(pickedDate);
      expect(body.preferredTime).toBe('10:00:00');
    });
    expect(await screen.findByText(/Follow-up request sent to the clinic manager/i)).toBeInTheDocument();
  });

  it('requires both a date and a slot before the follow-up can be sent', async () => {
    stubFetch({
      appointments: [appt({ status: 'Completed', examinationId: 'exam-1' })],
      post: () => ({ id: 'CON-0002' }),
    });
    renderWithAuth(<MyAppointmentsPage />, { role: 'Veterinarian', organizationId: 'org-9' });

    await userEvent.click(await screen.findByRole('button', { name: 'Open' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Request Follow-up' }));

    await screen.findByTestId('booking-calendar');
    const send = screen.getByRole('button', { name: 'Send follow-up request' });
    expect(send).toBeDisabled();

    // Reason alone is not enough — a date and a slot are still needed.
    await userEvent.type(screen.getByLabelText('Reason'), 'Recheck healing');
    expect(send).toBeDisabled();
  });

  it('shows a loading indicator before appointments resolve', async () => {
    let resolve!: (value: unknown) => void;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((r) => { resolve = (v) => r(jsonResponse(v)); })));
    renderWithAuth(<MyAppointmentsPage />, { role: 'Veterinarian' });
    expect(screen.getByText(/Loading appointments/)).toBeInTheDocument();
    resolve([]);
    expect(await screen.findByText(/No appointments match/)).toBeInTheDocument();
  });
});
