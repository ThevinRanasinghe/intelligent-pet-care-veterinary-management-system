import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SchedulingPage } from '../../features/scheduling/SchedulingPage';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return {
    ok,
    status,
    text: async () => JSON.stringify(body),
  } as Response;
}

const validSlot = {
  id: 'slot-1',
  veterinarianId: 'vet-1',
  date: '2026-01-10',
  startTime: '09:00:00',
  endTime: '09:30:00',
  branch: 'Colombo',
  status: 'Available',
};

const slotMissingVet = {
  id: 'slot-2',
  veterinarianId: '',
  date: '2026-01-12',
  startTime: '11:00:00',
  endTime: '11:30:00',
  branch: 'Kandy',
  status: 'Available',
};

const reservedAppointment = {
  id: 'appt-1',
  petId: 'pet-1',
  veterinarianId: 'vet-2',
  appointmentSlotId: 'slot-3',
  scheduledStart: '2026-01-11T10:00:00',
  scheduledEnd: '2026-01-11T10:30:00',
  status: 'Reserved',
  notes: '',
  createdAt: '2026-01-01T00:00:00',
  updatedAt: '2026-01-01T00:00:00',
};

const ownerRow = { id: 'own-1', fullName: 'Amal Perera', email: 'amal@x.lk', phoneNumber: '077', address: null };
const petRow = { id: 'pet-1', ownerId: 'own-1', name: 'Shadow', species: 'Dog', breed: 'Mixed' };
const orgRow = { id: 'org-1', name: 'Happy Paws', city: 'Colombo', address: null, latitude: null, longitude: null };
const vetRow = { id: 'vet-1', name: 'Dr. A', specialisation: 'General', branch: 'Colombo', active: true, userId: null };

const walkInResponse = {
  consultation: {
    id: 'CON-W1',
    petId: 'pet-1',
    ownerId: 'own-1',
    symptoms: 'Limping',
    urgency: 'Medium',
    status: 'AppointmentConfirmed',
    source: 'WalkIn',
    createdAt: '2026-01-01T00:00:00',
    updatedAt: '2026-01-01T00:00:00',
  },
  appointment: {
    id: 'appt-w1',
    petId: 'pet-1',
    veterinarianId: 'vet-1',
    appointmentSlotId: 'slot-x',
    scheduledStart: '2026-01-10T09:00:00',
    scheduledEnd: '2026-01-10T10:00:00',
    status: 'Confirmed',
    consultationRequestId: 'CON-W1',
    createdAt: '2026-01-01T00:00:00',
    updatedAt: '2026-01-01T00:00:00',
  },
  owner: ownerRow,
  pet: petRow,
};

function setupFetchMock() {
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    if (url.includes('/consultations/walk-in') && init?.method === 'POST') {
      return Promise.resolve(jsonResponse(walkInResponse, true, 201));
    }
    if (url.includes('/consultations/availability')) {
      return Promise.resolve(jsonResponse({
        date: '2026-01-10',
        isPast: false,
        slots: [
          { start: '09:00', end: '10:00', available: true, availableVeterinarianIds: ['vet-1'] },
          { start: '10:00', end: '11:00', available: false, availableVeterinarianIds: [] },
        ],
      }));
    }
    if (url.includes('/pets/owner/')) {
      return Promise.resolve(jsonResponse([petRow]));
    }
    if (url.endsWith('/owners')) {
      return Promise.resolve(jsonResponse([ownerRow]));
    }
    if (url.includes('/lookups/organizations')) {
      return Promise.resolve(jsonResponse([orgRow]));
    }
    if (url.includes('/manager/veterinarians')) {
      return Promise.resolve(jsonResponse([vetRow]));
    }
    if (url.includes('/appointments/available-slots')) {
      return Promise.resolve(jsonResponse([validSlot, slotMissingVet]));
    }
    if (url.endsWith('/appointments') && (!init || init.method === undefined)) {
      return Promise.resolve(jsonResponse([reservedAppointment]));
    }
    if (url.includes(`/appointments/${reservedAppointment.id}`) && init?.method === 'PUT') {
      return Promise.resolve(jsonResponse({ ...reservedAppointment, scheduledStart: '2026-01-11T12:00:00' }));
    }
    return Promise.resolve(jsonResponse([]));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('SchedulingPage form validation', () => {
  let fetchMock: ReturnType<typeof setupFetchMock>;

  beforeEach(() => {
    fetchMock = setupFetchMock();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('opens the walk-in intake prefilled when Book is clicked on a slot', async () => {
    const user = userEvent.setup();
    renderWithAuth(<SchedulingPage />, { organizationId: 'org-1' });
    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());

    await user.click(screen.getAllByRole('button', { name: 'Book' })[0]);

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Create walk-in booking', { selector: 'h3' })).toBeInTheDocument();
    // Slot date pre-fills the appointment date.
    await waitFor(() =>
      expect(within(dialog).getByLabelText(/date/i)).toHaveValue('2026-01-10'));
  });

  it('blocks walk-in submit until an owner is chosen', async () => {
    const user = userEvent.setup();
    renderWithAuth(<SchedulingPage />, { organizationId: 'org-1' });
    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Create walk-in booking' }));

    const dialog = await screen.findByRole('dialog');
    // Wait for the owner/clinic/vet lookups to finish.
    await waitFor(() =>
      expect(within(dialog).getByRole('option', { name: /Amal Perera/i })).toBeInTheDocument());

    const postCallsBefore = fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST').length;
    await user.click(within(dialog).getByRole('button', { name: 'Create walk-in booking' }));

    expect(await within(dialog).findByText(/select an existing owner/i)).toBeInTheDocument();
    const postCallsAfter = fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST').length;
    expect(postCallsAfter).toBe(postCallsBefore);
  });

  it('rejects an invalid start/end time when editing an appointment', async () => {
    const user = userEvent.setup();
    render(<SchedulingPage />);
    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());

    const editButton = screen.getByLabelText('Edit');
    await user.click(editButton);

    const startInput = screen.getByLabelText('Start time');
    const endInput = screen.getByLabelText('End time');
    await user.clear(startInput);
    await user.type(startInput, '10:00');
    await user.clear(endInput);
    await user.type(endInput, '09:00');

    const putCallsBefore = fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT').length;
    await user.click(screen.getByRole('button', { name: /save changes/i }));

    expect(await screen.findByText('End time must be after start time')).toBeInTheDocument();
    const putCallsAfter = fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT').length;
    expect(putCallsAfter).toBe(putCallsBefore);
  });

  it('opens the walk-in intake without a preselected vet for an anonymous slot', async () => {
    const user = userEvent.setup();
    renderWithAuth(<SchedulingPage />, { organizationId: 'org-1' });
    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());

    const bookButtons = screen.getAllByRole('button', { name: 'Book' });
    await user.click(bookButtons[bookButtons.length - 1]);

    const dialog = await screen.findByRole('dialog');
    await waitFor(() =>
      expect(within(dialog).getByLabelText(/veterinarian/i)).toBeInTheDocument());
    // The slot has no veterinarian — nothing is preselected and submission
    // cannot proceed until the manager picks one.
    expect(within(dialog).getByLabelText(/veterinarian/i)).toHaveValue('');
  });

  it('posts the walk-in booking to the consultation workflow endpoint', async () => {
    const user = userEvent.setup();
    renderWithAuth(<SchedulingPage />, { organizationId: 'org-1' });
    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Create walk-in booking' }));
    const dialog = await screen.findByRole('dialog');

    // Wait for lookups, then pick owner → pet loads → pick pet.
    await waitFor(() =>
      expect(within(dialog).getByRole('option', { name: /Amal Perera/i })).toBeInTheDocument());
    await user.selectOptions(within(dialog).getByLabelText('Existing owner'), 'own-1');
    await waitFor(() =>
      expect(within(dialog).getByRole('option', { name: /Shadow/i })).toBeInTheDocument());
    await user.selectOptions(within(dialog).getByLabelText('Existing pet'), 'pet-1');

    // The manager's clinic preselects from their session.
    expect(within(dialog).getByLabelText(/clinic/i)).toHaveValue('org-1');

    await user.type(within(dialog).getByLabelText(/symptoms/i), 'Limping');
    await user.selectOptions(within(dialog).getByLabelText(/veterinarian/i), 'vet-1');
    fireEvent.change(within(dialog).getByLabelText(/date/i), { target: { value: '2026-01-10' } });

    // Availability for that vet/day exposes the 09:00 slot.
    await waitFor(() =>
      expect(within(dialog).getByRole('option', { name: /09:00/ })).toBeInTheDocument());
    await user.selectOptions(within(dialog).getByLabelText(/start time/i), '09:00');

    // Debug aid: state of the select before submit.
    expect(within(dialog).getByLabelText(/start time/i)).toHaveValue('09:00');

    // jsdom does not dispatch form submission on a submit-button click in
    // this setup, so drive the form's submit event directly.
    const submitBtn = within(dialog).getByRole('button', { name: 'Create walk-in booking' });
    fireEvent.submit(submitBtn.closest('form')!);

    await waitFor(() => {
      const postCall = fetchMock.mock.calls.find(([url, init]) =>
        url.includes('/consultations/walk-in') && init?.method === 'POST');
      const errEl = dialog.querySelector('.form-error');
      expect(postCall, `walk-in POST missing; form error: ${errEl?.textContent ?? '(none)'}`).toBeTruthy();
      const body = JSON.parse((postCall![1] as RequestInit).body as string);
      expect(body.ownerId).toBe('own-1');
      expect(body.newOwner).toBeNull();
      expect(body.petId).toBe('pet-1');
      expect(body.newPet).toBeNull();
      expect(body.veterinarianId).toBe('vet-1');
      expect(body.date).toBe('2026-01-10');
      // TimeOnly binds from "HH:mm:ss" — the UI works in "HH:mm".
      expect(body.startTime).toBe('09:00:00');
      expect(body.organizationId).toBe('org-1');
      expect(body.symptoms).toBe('Limping');
    });

    expect(await screen.findByText(/walk-in booked/i)).toBeInTheDocument();
  });
});
