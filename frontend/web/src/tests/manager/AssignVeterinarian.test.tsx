import { fireEvent, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ConsultationRequestsPage } from '../../features/consultations/ConsultationRequestsPage';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const consultation = {
  id: 'CON-0001',
  petId: 'pet-1',
  ownerId: 'own-1',
  petName: 'Shadow',
  symptoms: 'Limping on left leg',
  urgency: 'Medium',
  preferredDate: '2026-10-05T00:00:00',
  preferredTime: '10:00:00',
  organizationId: 'org-1',
  organizationName: 'Happy Paws',
  budget: 5000,
  additionalNotes: null,
  status: 'Submitted',
  requestType: 'FollowUp',
  requestedByVeterinarianId: 'vet-1',
  requestedByVeterinarianName: 'Dr. Silva',
  createdAt: '2026-09-26T00:00:00Z',
  updatedAt: '2026-09-26T00:00:00Z',
};

const veterinarians = [
  { id: 'vet-1', name: 'Dr. Silva', specialisation: 'General', branch: 'Colombo', active: true, userId: 'u-1' },
];

/** Nine fixed slots; the 10:00 slot is free for vet-1, 11:00 is booked. */
function dayAvailability(date: string) {
  const starts = ['09:00', '10:00', '11:00', '12:00', '13:00', '14:00', '15:00', '16:00', '17:00'];
  return {
    date,
    isPast: false,
    slots: starts.map((start, i) => {
      const booked = start === '11:00';
      return {
        start,
        end: `${String(9 + i + 1).padStart(2, '0')}:00`,
        available: !booked,
        availableVeterinarianIds: booked ? [] : ['vet-1'],
      };
    }),
  };
}

function stubFetch(
  assignResponse?: { body: unknown; ok?: boolean; status?: number },
  item: Record<string, unknown> = consultation,
) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';
    if (url.endsWith('/owners')) return jsonResponse([{ id: 'own-1', fullName: 'Amal', email: 'amal@x.lk', phoneNumber: '077' }]);
    if (url.endsWith('/pets')) return jsonResponse([{ id: 'pet-1', ownerId: 'own-1', name: 'Shadow', species: 'Dog', breed: 'Mixed' }]);
    if (url.endsWith('/manager/veterinarians')) return jsonResponse(veterinarians);
    if (url.includes('/consultations/availability')) {
      const date = new URL(url).searchParams.get('date') ?? '2026-10-06';
      return jsonResponse(dayAvailability(date));
    }
    if (method === 'POST' && url.includes('/assign')) {
      return jsonResponse(
        assignResponse?.body ?? { id: 'appt-1', petId: 'pet-1', veterinarianId: 'vet-1', status: 'Confirmed' },
        assignResponse?.ok ?? true,
        assignResponse?.status ?? 200,
      );
    }
    if (url.includes('/history')) return jsonResponse([]);
    if (url.includes('/consultations/')) return jsonResponse(item);
    if (url.endsWith('/consultations')) return jsonResponse([item]);
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

/** Owner-scoped stub — the Pet Owner view resolves their owner record by
 *  session email (seedAuth uses manager@petcare.lk) and calls
 *  /consultations/owner/{id} instead of the clinic-wide list. */
function stubOwnerFetch(item: Record<string, unknown>) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.endsWith('/owners')) return jsonResponse([{ id: 'own-1', fullName: 'Amal', email: 'manager@petcare.lk', phoneNumber: '077' }]);
    if (url.includes('/pets/owner/')) return jsonResponse([{ id: 'pet-1', ownerId: 'own-1', name: 'Shadow', species: 'Dog', breed: 'Mixed' }]);
    if (url.includes('/consultations/owner/')) return jsonResponse([item]);
    if (url.includes('/history')) return jsonResponse([]);
    if (url.includes('/consultations/')) return jsonResponse(item);
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

async function openDetails() {
  await userEvent.click(await screen.findByRole('button', { name: /view details/i }));
}

describe('ConsultationRequestsPage — assign veterinarian', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('shows the Assign Veterinarian button to a ClinicManager on a Submitted request', async () => {
    stubFetch();
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager' });
    await openDetails();

    expect(await screen.findByRole('button', { name: 'Assign Veterinarian' })).toBeInTheDocument();
    // requestType badge + requesting vet shown
    expect(screen.getAllByText(/Follow-up/).length).toBeGreaterThan(0);
    expect(screen.getByText(/Dr\. Silva/)).toBeInTheDocument();
  });

  it('does NOT show the Assign button to a Veterinarian', async () => {
    stubFetch();
    renderWithAuth(<ConsultationRequestsPage />, { role: 'Veterinarian' });
    await openDetails();

    expect(screen.queryByRole('button', { name: 'Assign Veterinarian' })).not.toBeInTheDocument();
  });

  it('manager queue shows View Details but never Submit Request', async () => {
    stubFetch();
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager' });
    await openDetails();

    // Manager processes the request — assign + cancel, never re-submit.
    expect(screen.getByRole('button', { name: 'Assign Veterinarian' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel Request' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Submit Request' })).not.toBeInTheDocument();
    expect(screen.queryByText('Review the request before submitting.')).not.toBeInTheDocument();
  });

  it('does NOT show Submit Request to a ClinicManager even on a Draft request', async () => {
    stubFetch(undefined, { ...consultation, status: 'Draft', requestType: 'Initial' });
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager' });
    await openDetails();

    expect(screen.queryByRole('button', { name: 'Submit Request' })).not.toBeInTheDocument();
    expect(screen.queryByText('Review the request before submitting.')).not.toBeInTheDocument();
    // Assign is not offered on Draft — the requester hasn't submitted yet.
    expect(screen.queryByRole('button', { name: 'Assign Veterinarian' })).not.toBeInTheDocument();
  });

  it('shows Submit Request to the Pet Owner on their own Draft request', async () => {
    stubOwnerFetch({ ...consultation, status: 'Draft', requestType: 'Initial' });
    renderWithAuth(<ConsultationRequestsPage />, { role: 'PetOwner' });
    await openDetails();

    expect(await screen.findByRole('button', { name: 'Submit Request' })).toBeInTheDocument();
    expect(screen.getByText('Review the request before submitting.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Assign Veterinarian' })).not.toBeInTheDocument();
  });

  it('POSTs to /consultations/{id}/assign with veterinarian, date and slot start (one-hour end is implied)', async () => {
    const fetchMock = stubFetch();
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager', organizationId: 'org-1' });
    await openDetails();
    await userEvent.click(await screen.findByRole('button', { name: 'Assign Veterinarian' }));

    await userEvent.selectOptions(await screen.findByLabelText('Veterinarian'), 'vet-1');
    fireEvent.change(screen.getByLabelText('Appointment date'), { target: { value: '2026-10-06' } });

    // 10:00–11:00 slot is free for vet-1 — pick it.
    await userEvent.click(await screen.findByRole('button', { name: '10:00 AM – 11:00 AM' }));
    await userEvent.type(screen.getByLabelText('Assignment notes'), 'Urgent case');
    await userEvent.click(screen.getByRole('button', { name: 'Confirm assignment' }));

    const call = fetchMock.mock.calls.find(
      ([url, init]) => String(url).includes('/consultations/CON-0001/assign') && (init as RequestInit)?.method === 'POST',
    );
    expect(call).toBeTruthy();
    const body = JSON.parse(String((call![1] as RequestInit).body));
    expect(body).toMatchObject({
      veterinarianId: 'vet-1',
      date: '2026-10-06',
      startTime: '10:00:00',
      notes: 'Urgent case',
    });
    // The end time is derived server-side — the client never sends a
    // free-form end time.
    expect(body.endTime).toBeUndefined();
    expect(await screen.findByText(/Assigned to Dr\. Silva/)).toBeInTheDocument();
  });

  it('disables slots where the selected vet is not in availableVeterinarianIds', async () => {
    stubFetch();
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager', organizationId: 'org-1' });
    await openDetails();
    await userEvent.click(await screen.findByRole('button', { name: 'Assign Veterinarian' }));

    await userEvent.selectOptions(await screen.findByLabelText('Veterinarian'), 'vet-1');
    fireEvent.change(screen.getByLabelText('Appointment date'), { target: { value: '2026-10-06' } });

    // 11:00 is booked (not in availableVeterinarianIds, slot unavailable).
    const bookedSlot = await screen.findByRole('button', { name: '11:00 AM – 12:00 PM' });
    expect(bookedSlot).toBeDisabled();
    expect(screen.getByRole('button', { name: '10:00 AM – 11:00 AM' })).toBeEnabled();
  });

  it('shows the server message on a 409 conflict', async () => {
    stubFetch({ body: { message: 'The veterinarian already has an appointment at that time.' }, ok: false, status: 409 });
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager', organizationId: 'org-1' });
    await openDetails();
    await userEvent.click(await screen.findByRole('button', { name: 'Assign Veterinarian' }));

    await userEvent.selectOptions(await screen.findByLabelText('Veterinarian'), 'vet-1');
    fireEvent.change(screen.getByLabelText('Appointment date'), { target: { value: '2026-10-06' } });
    await userEvent.click(await screen.findByRole('button', { name: '10:00 AM – 11:00 AM' }));
    await userEvent.click(screen.getByRole('button', { name: 'Confirm assignment' }));

    expect(await screen.findByText(/already has an appointment/)).toBeInTheDocument();
  });
});
