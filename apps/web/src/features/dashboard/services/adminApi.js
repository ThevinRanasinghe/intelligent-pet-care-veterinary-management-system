import api from '../../../lib/apiClient';

/**
 * SuperAdmin and ClinicManager User/Organization Management API
 */

// ── Organizations (SuperAdmin) ──
export async function getOrganizations({ search = '', status = '' } = {}) {
  const params = new URLSearchParams();
  if (search) params.append('search', search);
  if (status) params.append('status', status);

  const response = await api.get(`/organizations?${params.toString()}`);
  return response.data?.data ?? [];
}

export async function getOrganizationById(id) {
  const response = await api.get(`/organizations/${id}`);
  return response.data?.data;
}

export async function approveOrganization(id) {
  const response = await api.post(`/organizations/${id}/approve`);
  return response.data;
}

export async function rejectOrganization(id, reason) {
  const response = await api.post(`/organizations/${id}/reject`, { reason });
  return response.data;
}

export async function suspendOrganization(id) {
  const response = await api.post(`/organizations/${id}/suspend`);
  return response.data;
}

export async function activateOrganization(id) {
  const response = await api.post(`/organizations/${id}/activate`);
  return response.data;
}

// ── Staff Management (ClinicManager) ──
export async function getOrganizationStaff({ search = '', role = '', status = '' } = {}) {
  const params = new URLSearchParams();
  if (search) params.append('search', search);
  if (role)   params.append('role', role);
  if (status) params.append('status', status);

  const response = await api.get(`/organization/users?${params.toString()}`);
  return response.data?.data ?? [];
}

export async function getStaffMemberById(id) {
  const response = await api.get(`/organization/users/${id}`);
  return response.data?.data;
}

export async function createStaffMember(data) {
  const response = await api.post('/organization/users', data);
  return response.data?.data;
}

export async function updateStaffMember(id, data) {
  const response = await api.put(`/organization/users/${id}`, data);
  return response.data?.data;
}

export async function updateStaffStatus(id, status) {
  const response = await api.patch(`/organization/users/${id}/status`, { status });
  return response.data?.data;
}

export async function resetStaffPassword(id, temporaryPassword) {
  const response = await api.post(`/organization/users/${id}/reset-password`, { temporaryPassword });
  return response.data;
}

export async function verifyStaffMember(id) {
  const response = await api.post(`/organization/users/${id}/verify`);
  return response.data?.data;
}

// ── User Account & Profile ──
export async function getProfile() {
  const response = await api.get('/account/profile');
  return response.data?.data;
}

export async function updateProfile(data) {
  const response = await api.put('/account/profile', data);
  return response.data?.data;
}

export async function changePassword(data) {
  const response = await api.post('/account/change-password', data);
  return response.data;
}
