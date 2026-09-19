import { describe, expect, it } from 'vitest';
import { appointmentSlots } from '../services/mockData';
import { hasVetConflict } from '../services/schedulingService';
import { calculateQuoteTotal } from '../services/billingService';

describe('Scheduling business rules', () => {
  it('detects overlapping veterinarian slots', () => {
    expect(hasVetConflict(appointmentSlots, {
      veterinarianId: 'vet-01', date: '2026-08-20', startTime: '09:15', endTime: '09:45',
    })).toBe(true);
  });

  it('allows a non-overlapping slot', () => {
    expect(hasVetConflict(appointmentSlots, {
      veterinarianId: 'vet-01', date: '2026-08-20', startTime: '10:30', endTime: '11:00',
    })).toBe(false);
  });
});

describe('Billing business rules', () => {
  it('calculates a quotation total from line items', () => {
    expect(calculateQuoteTotal([
      { id:'1', category:'Consultation', description:'Consultation', quantity:1, unitPrice:3500 },
      { id:'2', category:'Medicine', description:'Medicine', quantity:2, unitPrice:1000 },
    ])).toBe(5500);
  });
});
