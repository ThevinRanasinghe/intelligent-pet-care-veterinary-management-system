import { apiRequest } from './api';
import type { AppointmentResponse } from './schedulingService';
import type { QuotationResponse } from './billingService';
import type { Examination, Prescription } from '../types/domain';

/**
 * ClinicManager staff-creation boundary. The request intentionally has no
 * organizationId — the backend derives it from the authenticated manager's
 * tenant context, so a caller can never steer accounts into another org.
 */
export interface ManagerCreateStaffRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
}

/** Newly created staff account plus the one-time temporary password. */
export interface CreatedStaffAccount {
  id: string;
  firstName: string;
  lastName: string;
  name: string;
  email: string;
  role: string;
  active: boolean;
  mustChangePassword: boolean;
  organizationId: string | null;
  organizationName: string | null;
  createdAt: string;
  temporaryPassword: string;
}

export async function createVeterinarian(request: ManagerCreateStaffRequest): Promise<CreatedStaffAccount> {
  return apiRequest<CreatedStaffAccount>('/manager/users/veterinarians', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export async function createInventoryOfficer(request: ManagerCreateStaffRequest): Promise<CreatedStaffAccount> {
  return apiRequest<CreatedStaffAccount>('/manager/users/inventory-officers', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

/** Veterinarian row in the manager's organization list. */
export interface ManagerVeterinarian {
  id: string;
  name: string;
  specialisation: string;
  branch: string;
  active: boolean;
  userId: string | null;
}

/** Work history for one veterinarian — plain counts, no analytics. */
export interface VeterinarianHistory {
  veterinarian: { id: string; name: string; specialisation: string };
  appointments: {
    completedCount: number;
    upcomingCount: number;
    cancelledCount: number;
    items: AppointmentResponse[];
  };
  examinations: {
    total: number;
    initialCount: number;
    followUpCount: number;
    items: Examination[];
  };
  prescriptions: {
    total: number;
    items: Prescription[];
  };
  medicineRequests: {
    pending: number;
    issued: number;
    unavailable: number;
  };
  bills: {
    total: number;
    paidCount: number;
    pendingCount: number;
    vetChargeTotal: number;
    medicineTotal: number;
    grandTotal: number;
    items: QuotationResponse[];
  };
}

export async function getVeterinarians(): Promise<ManagerVeterinarian[]> {
  return apiRequest<ManagerVeterinarian[]>('/manager/veterinarians');
}

export async function getVeterinarianHistory(
  veterinarianId: string,
  params?: { from?: string; to?: string },
): Promise<VeterinarianHistory> {
  const query = new URLSearchParams();
  if (params?.from) query.append('from', params.from);
  if (params?.to) query.append('to', params.to);
  const queryString = query.toString();
  return apiRequest<VeterinarianHistory>(
    `/manager/veterinarians/${encodeURIComponent(veterinarianId)}/history${queryString ? `?${queryString}` : ''}`,
  );
}
