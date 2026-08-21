import type { AppointmentSlot } from '../types/domain';
import { ApiError, apiRequest } from './api';

export interface AppointmentSlotResponse {
  id: string;
  veterinarianId: string;
  date: string;
  startTime: string;
  endTime: string;
  branch: string;
  status: string;
}

export interface AppointmentResponse {
  id: string;
  petId: string;
  veterinarianId: string;
  appointmentSlotId: string;
  scheduledStart: string;
  scheduledEnd: string;
  status: string;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateAppointmentRequest {
  petId: string;
  veterinarianId: string;
  appointmentSlotId: string;
  scheduledStart: string;
  scheduledEnd: string;
  notes?: string;
}

export interface UpdateAppointmentRequest {
  scheduledStart: string;
  scheduledEnd: string;
  notes?: string;
}

export interface ConflictCheckRequest {
  veterinarianId: string;
  scheduledStart: string;
  scheduledEnd: string;
  appointmentId?: string;
}

function toLocalSlot(slot: AppointmentSlotResponse): AppointmentSlot {
  return {
    id: slot.id,
    veterinarianId: slot.veterinarianId,
    veterinarianName: slot.veterinarianId,
    date: slot.date,
    startTime: slot.startTime,
    endTime: slot.endTime,
    branch: slot.branch,
    status: slot.status as AppointmentSlot['status'],
  };
}

export async function getAppointmentSlots(): Promise<AppointmentSlot[]> {
  const data = await apiRequest<AppointmentSlotResponse[]>('/appointments/available-slots');
  return data.map(toLocalSlot);
}

export async function getAppointmentById(id: string): Promise<AppointmentResponse | null> {
  try {
    return await apiRequest<AppointmentResponse>(`/appointments/${id}`);
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) return null;
    throw err;
  }
}

export async function createAppointment(request: CreateAppointmentRequest): Promise<AppointmentResponse> {
  return await apiRequest<AppointmentResponse>('/appointments', { method: 'POST', body: JSON.stringify(request) });
}

export async function updateAppointment(id: string, request: UpdateAppointmentRequest): Promise<AppointmentResponse> {
  return await apiRequest<AppointmentResponse>(`/appointments/${id}`, { method: 'PUT', body: JSON.stringify(request) });
}

export async function cancelAppointment(id: string): Promise<void> {
  await apiRequest<void>(`/appointments/${id}`, { method: 'DELETE' });
}

export async function checkVetConflict(request: ConflictCheckRequest): Promise<boolean> {
  return await apiRequest<boolean>('/appointments/check-conflict', { method: 'POST', body: JSON.stringify(request) });
}

export function hasVetConflict(slots: AppointmentSlot[], candidate: Pick<AppointmentSlot, 'veterinarianId' | 'date' | 'startTime' | 'endTime'>): boolean {
  const toMinutes = (value: string) => {
    const [hour, minute] = value.split(':').map(Number);
    return hour * 60 + minute;
  };

  const start = toMinutes(candidate.startTime);
  const end = toMinutes(candidate.endTime);

  return slots.some((slot) => {
    if (slot.veterinarianId !== candidate.veterinarianId || slot.date !== candidate.date || slot.status === 'Cancelled') return false;
    const existingStart = toMinutes(slot.startTime);
    const existingEnd = toMinutes(slot.endTime);
    return start < existingEnd && end > existingStart;
  });
}
