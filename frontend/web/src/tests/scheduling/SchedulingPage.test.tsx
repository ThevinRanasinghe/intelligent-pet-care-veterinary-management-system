import { render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SchedulingPage } from '../../features/scheduling/SchedulingPage';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return {
    ok,
    status,
    text: async () => JSON.stringify(body),
  } as Response;
}

const availableSlot = {
  id: 'slot-1',
  veterinarianId: 'vet-1',
  date: '2026-01-10',
  startTime: '09:00:00',
  endTime: '09:30:00',
  branch: 'Colombo',
  status: 'Available',
};

const reservedAppointment = {
  id: 'appt-1',
  petId: 'pet-1',
  veterinarianId: 'vet-2',
  appointmentSlotId: 'slot-2',
  scheduledStart: '2026-01-11T10:00:00',
  scheduledEnd: '2026-01-11T10:30:00',
  status: 'Reserved',
  notes: '',
  createdAt: '2026-01-01T00:00:00',
  updatedAt: '2026-01-01T00:00:00',
};

function mockFetchImplementation(fetchMock: ReturnType<typeof vi.fn>) {
  fetchMock.mockImplementation((url: string) => {
    if (url.includes('/appointments/available-slots')) {
      return Promise.resolve(jsonResponse([availableSlot]));
    }
    if (url.endsWith('/appointments')) {
      return Promise.resolve(jsonResponse([reservedAppointment]));
    }
    return Promise.resolve(jsonResponse([]));
  });
}

describe('SchedulingPage component states', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders successfully', async () => {
    mockFetchImplementation(fetch as unknown as ReturnType<typeof vi.fn>);
    render(<SchedulingPage />);
    expect(screen.getByText('Veterinarian scheduling')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());
  });

  it('displays appointment slots and appointments when API returns data', async () => {
    mockFetchImplementation(fetch as unknown as ReturnType<typeof vi.fn>);
    render(<SchedulingPage />);

    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());

    expect(screen.getByText('Colombo')).toBeInTheDocument();
    expect(screen.getAllByText('Available').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Reserved').length).toBeGreaterThan(0);
  });

  it('displays empty state when no slots or appointments exist', async () => {
    (fetch as unknown as ReturnType<typeof vi.fn>).mockImplementation(() =>
      Promise.resolve(jsonResponse([])),
    );
    render(<SchedulingPage />);

    await waitFor(() => expect(screen.getByText('No slots found')).toBeInTheDocument());
  });

  it('displays loading state while the API request is pending', async () => {
    const resolvers: Array<(value: Response) => void> = [];
    (fetch as unknown as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise<Response>((resolve) => { resolvers.push(resolve); }),
    );
    render(<SchedulingPage />);

    expect(screen.getByText('Loading schedule...')).toBeInTheDocument();
    resolvers.forEach((resolve) => resolve(jsonResponse([])));
    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());
  });

  it('displays an API error state when the request fails', async () => {
    (fetch as unknown as ReturnType<typeof vi.fn>).mockImplementation(() =>
      Promise.resolve(jsonResponse({ detail: 'Backend unavailable' }, false, 500)),
    );
    render(<SchedulingPage />);

    await waitFor(() => expect(screen.getByText('Backend unavailable')).toBeInTheDocument());
  });
});

describe('SchedulingPage table row rendering', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
    mockFetchImplementation(fetch as unknown as ReturnType<typeof vi.fn>);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows a Book action for available slots and view/edit/cancel for booked ones', async () => {
    render(<SchedulingPage />);
    await waitFor(() => expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument());

    const rows = screen.getAllByRole('row').slice(1);
    expect(rows.length).toBeGreaterThanOrEqual(2);

    const bookRow = rows.find((row) => within(row).queryByText('Book'));
    expect(bookRow).toBeTruthy();

    const cancelButtons = screen.getAllByLabelText('Cancel');
    expect(cancelButtons.length).toBeGreaterThan(0);
  });
});
