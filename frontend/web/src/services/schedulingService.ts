import type { AppointmentSlot } from '../types/domain';
import { appointmentSlots } from './mockData';

export function getAppointmentSlots(): AppointmentSlot[] {
  return [...appointmentSlots];
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
