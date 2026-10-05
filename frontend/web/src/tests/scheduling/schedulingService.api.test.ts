import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../services/api';
import {
  cancelAppointment,
  checkVetConflict,
  createAppointment,
  getAppointmentSlots,
} from '../../services/schedulingService';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return {
    ok,
    status,
    text: async () => (body === undefined ? '' : JSON.stringify(body)),
  } as Response;
}

describe('schedulingService API integration', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('GETs available slots and maps the response to domain shape', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([
      { id: 's1', veterinarianId: 'vet-1', date: '2026-01-10', startTime: '09:00:00', endTime: '09:30:00', branch: 'Colombo', status: 'Available' },
    ]));

    const slots = await getAppointmentSlots();

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/appointments/available-slots'),
      expect.objectContaining({ headers: expect.any(Object) }),
    );
    expect(slots).toHaveLength(1);
    expect(slots[0]).toMatchObject({ id: 's1', veterinarianId: 'vet-1', branch: 'Colombo', status: 'Available' });
  });

  it('POSTs a new appointment and returns the created record on success', async () => {
    const created = {
      id: 'appt-1', petId: 'pet-1', veterinarianId: 'vet-1', appointmentSlotId: 's1',
      scheduledStart: '2026-01-10T09:00:00', scheduledEnd: '2026-01-10T09:30:00',
      status: 'Reserved', notes: '', createdAt: '2026-01-01T00:00:00', updatedAt: '2026-01-01T00:00:00',
    };
    fetchMock.mockResolvedValueOnce(jsonResponse(created));

    const result = await createAppointment({
      petId: 'pet-1', veterinarianId: 'vet-1', appointmentSlotId: 's1',
      scheduledStart: '2026-01-10T09:00:00', scheduledEnd: '2026-01-10T09:30:00',
    });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/appointments');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body as string)).toMatchObject({ petId: 'pet-1', veterinarianId: 'vet-1' });
    expect(result).toEqual(created);
  });

  it('throws an ApiError with backend details when booking conflicts (409)', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ detail: 'Veterinarian already booked for this time' }, false, 409));

    await expect(createAppointment({
      petId: 'pet-1', veterinarianId: 'vet-1', appointmentSlotId: 's1',
      scheduledStart: '2026-01-10T09:00:00', scheduledEnd: '2026-01-10T09:30:00',
    })).rejects.toBeInstanceOf(ApiError);
  });

  it('POSTs a conflict check and returns a boolean result', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(true));

    const result = await checkVetConflict({
      veterinarianId: 'vet-1', scheduledStart: '2026-01-10T09:00:00', scheduledEnd: '2026-01-10T09:30:00',
    });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/appointments/check-conflict');
    expect(init.method).toBe('POST');
    expect(result).toBe(true);
  });

  it('propagates failure when the conflict-check request errors', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ title: 'Bad request' }, false, 400));

    await expect(checkVetConflict({
      veterinarianId: 'vet-1', scheduledStart: '2026-01-10T09:00:00', scheduledEnd: '2026-01-10T09:30:00',
    })).rejects.toBeInstanceOf(ApiError);
  });

  it('DELETEs an appointment to cancel it', async () => {
    fetchMock.mockResolvedValueOnce({ ok: true, status: 204, text: async () => '' } as Response);

    await cancelAppointment('appt-1');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/appointments/appt-1');
    expect(init.method).toBe('DELETE');
  });
});
