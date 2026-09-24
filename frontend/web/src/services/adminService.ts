import { apiRequest } from './api';
import type { Role } from '../types/domain';

export interface AdminUser {
  id: string;
  firstName: string;
  lastName: string;
  name: string;
  email: string;
  role: Role;
  active: boolean;
  mustChangePassword: boolean;
  organizationId: string | null;
  organizationName: string | null;
  createdAt: string;
}

export type OrganizationStatus = 'Pending' | 'Active' | 'Rejected' | 'Suspended' | 'Inactive';

export interface AdminOrganization {
  id: string;
  name: string;
  registrationNumber: string | null;
  email: string;
  phone: string;
  address: string;
  city: string;
  country: string;
  status: OrganizationStatus;
  isActive: boolean;
  rejectionReason: string | null;
  userCount: number;
  createdAt: string;
}

export interface AdminSystemInfo {
  environment: string;
  databaseProvider: string;
  serverTimeUtc: string;
  totalUsers: number;
  activeUsers: number;
  inactiveUsers: number;
  totalOrganizations: number;
  pendingOrganizations: number;
  activeOrganizations: number;
  usersByRole: Record<string, number>;
}

export async function getUsers(): Promise<AdminUser[]> {
  return apiRequest<AdminUser[]>('/admin/users');
}

export async function setUserActive(userId: string, active: boolean): Promise<AdminUser> {
  return apiRequest<AdminUser>(`/admin/users/${userId}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ active }),
  });
}

export interface CreateStaffUserRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
  organizationId: string;
}

/** AdminUser plus the one-time temporary password shown to the admin only. */
export interface CreatedStaffUser extends AdminUser {
  temporaryPassword: string;
}

export async function createVeterinarian(request: CreateStaffUserRequest): Promise<CreatedStaffUser> {
  return apiRequest<CreatedStaffUser>('/admin/users/veterinarians', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export async function createInventoryOfficer(request: CreateStaffUserRequest): Promise<CreatedStaffUser> {
  return apiRequest<CreatedStaffUser>('/admin/users/inventory-officers', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export async function getOrganizations(): Promise<AdminOrganization[]> {
  return apiRequest<AdminOrganization[]>('/admin/organizations');
}

export async function setOrganizationStatus(
  organizationId: string,
  status: Exclude<OrganizationStatus, 'Pending'>,
  reason?: string,
): Promise<AdminOrganization> {
  return apiRequest<AdminOrganization>(`/admin/organizations/${organizationId}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ status, reason: reason ?? null }),
  });
}

export async function getRoles(): Promise<string[]> {
  return apiRequest<string[]>('/admin/roles');
}

export async function getSystemInfo(): Promise<AdminSystemInfo> {
  return apiRequest<AdminSystemInfo>('/admin/system');
}
