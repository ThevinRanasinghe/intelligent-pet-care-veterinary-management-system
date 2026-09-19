import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SchedulingPage } from '../../features/scheduling/SchedulingPage';

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

function setupFetchMock() {
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    if (url.includes('/appointments/available-slots')) {
      return Promise.resolve(jsonResponse([validSlot, slotMissingVet]));
    }
    if (url.endsWith('/appointments') && (!init || init.method === undefined)) {
      return Promise.resolve(jsonResponse([reservedAppointment]));
    }
    if (url.endsWith('/appointments') && init?.method === 'POST') {
      return Promise.resolve(jsonResponse({ ...reservedAppointment, id: 'appt-new' }));
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

  it('requires a Pet ID before booking an available slot', async () => {
    const user = userEvent.setup();
    render(<SchedulingPage />);
    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());

    const bookButtons = screen.getAllByRole('button', { name: /book/i });
    await user.click(bookButtons[0]);

    const submit = screen.getByRole('button', { name: /book appointment/i });
    const postCallsBefore = fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST').length;
    await user.click(submit);

    expect(await screen.findByText('Pet ID is required')).toBeInTheDocument();
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

  it('handles a slot with missing veterinarian/slot data by blocking the booking', async () => {
    const user = userEvent.setup();
    render(<SchedulingPage />);
    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());

    const bookButtons = screen.getAllByRole('button', { name: /book/i });
    await user.click(bookButtons[bookButtons.length - 1]);

    await user.type(screen.getByLabelText('Pet ID'), 'pet-123');

    const postCallsBefore = fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST').length;
    await user.click(screen.getByRole('button', { name: /book appointment/i }));

    expect(await screen.findByText(/missing veterinarian or slot information/i)).toBeInTheDocument();
    const postCallsAfter = fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST').length;
    expect(postCallsAfter).toBe(postCallsBefore);
  });

  it('calls the createAppointment API on a valid booking submission', async () => {
    const user = userEvent.setup();
    render(<SchedulingPage />);
    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());

    const bookButtons = screen.getAllByRole('button', { name: /book/i });
    await user.click(bookButtons[0]);

    await user.type(screen.getByLabelText('Pet ID'), 'pet-999');
    await user.click(screen.getByRole('button', { name: /book appointment/i }));

    await waitFor(() => {
      const postCall = fetchMock.mock.calls.find(([url, init]) => url.endsWith('/appointments') && init?.method === 'POST');
      expect(postCall).toBeTruthy();
      const body = JSON.parse((postCall![1] as RequestInit).body as string);
      expect(body.petId).toBe('pet-999');
      expect(body.veterinarianId).toBe(validSlot.veterinarianId);
    });

    expect(await screen.findByText('Appointment booked')).toBeInTheDocument();
  });
});
