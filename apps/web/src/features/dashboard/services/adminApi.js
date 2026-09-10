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

// ── Clinic Manager: Organization Workspace Profile ──
export async function getMyOrganization() {
  const response = await api.get('/organizations/my-organization');
  return response.data?.data;
}

export async function updateMyOrganization(data) {
  const response = await api.put('/organizations/my-organization', data);
  return response.data?.data;
}

// ── Clinic Manager: Dashboard & Analytics ──
export async function getClinicDashboardSummary() {
  const response = await api.get('/clinic-manager/dashboard');
  return response.data?.data;
}

export async function getRevenueReport({ fromDate = '', toDate = '', period = '' } = {}) {
  const params = new URLSearchParams();
  if (fromDate) params.append('fromDate', fromDate);
  if (toDate)   params.append('toDate', toDate);
  if (period)   params.append('period', period);

  const response = await api.get(`/clinic-manager/reports/revenue?${params.toString()}`);
  return response.data?.data;
}

export async function getCommonConditionsReport() {
  const response = await api.get('/clinic-manager/reports/common-conditions');
  return response.data?.data;
}

export async function getVeterinarianWorkloadReport({ fromDate = '', toDate = '' } = {}) {
  const params = new URLSearchParams();
  if (fromDate) params.append('fromDate', fromDate);
  if (toDate)   params.append('toDate', toDate);

  const response = await api.get(`/clinic-manager/reports/veterinarian-workload?${params.toString()}`);
  return response.data?.data;
}

export async function getClinicAuditLogs({ search = '', action = '', pageNumber = 1, pageSize = 20 } = {}) {
  const params = new URLSearchParams();
  if (search)     params.append('search', search);
  if (action)     params.append('action', action);
  if (pageNumber) params.append('pageNumber', pageNumber);
  if (pageSize)   params.append('pageSize', pageSize);

  const response = await api.get(`/clinic-manager/audit-logs?${params.toString()}`);
  return response.data?.data ?? [];
}

// ── Clinic Manager: Scheduling & Slots ──
export async function getOrganizationVeterinarians() {
  const response = await api.get('/schedules/veterinarians');
  return response.data?.data ?? [];
}

export async function getScheduleSlots({ veterinarianId = '', date = '' } = {}) {
  const params = new URLSearchParams();
  if (veterinarianId) params.append('veterinarianId', veterinarianId);
  if (date)           params.append('date', date);

  const response = await api.get(`/schedules/slots?${params.toString()}`);
  return response.data?.data ?? [];
}

export async function getAvailableSlots({ veterinarianId = '', date = '' } = {}) {
  const params = new URLSearchParams();
  if (veterinarianId) params.append('veterinarianId', veterinarianId);
  if (date)           params.append('date', date);

  const response = await api.get(`/schedules/slots/available?${params.toString()}`);
  return response.data?.data ?? [];
}

export async function createScheduleSlot(data) {
  const response = await api.post('/schedules/slots', data);
  return response.data?.data;
}

export async function updateScheduleSlot(id, data) {
  const response = await api.put(`/schedules/slots/${id}`, data);
  return response.data?.data;
}

export async function deleteScheduleSlot(id) {
  const response = await api.delete(`/schedules/slots/${id}`);
  return response.data;
}

// ── Clinic Manager: Appointments ──
export async function getAppointments({ search = '', status = '', date = '' } = {}) {
  const params = new URLSearchParams();
  if (search) params.append('search', search);
  if (status) params.append('status', status);
  if (date)   params.append('date', date);

  const response = await api.get(`/appointments?${params.toString()}`);
  return response.data?.data ?? [];
}

export async function getAppointmentById(id) {
  const response = await api.get(`/appointments/${id}`);
  return response.data?.data;
}

export async function createAppointment(data) {
  const response = await api.post('/appointments', data);
  return response.data?.data;
}

export async function updateAppointment(id, data) {
  const response = await api.put(`/appointments/${id}`, data);
  return response.data?.data;
}

export async function updateAppointmentStatus(id, status) {
  const response = await api.patch(`/appointments/${id}/status`, { status });
  return response.data?.data;
}

export async function cancelAppointment(id) {
  const response = await api.delete(`/appointments/${id}`);
  return response.data;
}

export async function checkScheduleConflict(data) {
  const response = await api.post('/appointments/check-conflict', data);
  return response.data?.data ?? false;
}

// ── Clinic Manager: AI Proposals & Approvals ──
export async function getProposals({ status = '' } = {}) {
  const params = new URLSearchParams();
  if (status) params.append('status', status);

  const response = await api.get(`/approvals?${params.toString()}`);
  return response.data?.data ?? [];
}

export async function getProposalById(id) {
  const response = await api.get(`/approvals/${id}`);
  return response.data?.data;
}

export async function approveProposal(id, { comment = '' } = {}) {
  const response = await api.post(`/approvals/${id}/approve`, { comment });
  return response.data?.data;
}

export async function rejectProposal(id, { reason = '' } = {}) {
  const response = await api.post(`/approvals/${id}/reject`, { reason });
  return response.data?.data;
}

export async function requestProposalRevision(id, { reason = '' } = {}) {
  const response = await api.post(`/approvals/${id}/request-revision`, { reason });
  return response.data?.data;
}

export async function getProposalHistory(id) {
  const response = await api.get(`/approvals/${id}/history`);
  return response.data?.data ?? [];
}

// ── Clinic Manager: Quotations & Billing ──
export async function getQuotations() {
  const response = await api.get('/quotations');
  return response.data?.data ?? [];
}

export async function getQuotationById(id) {
  const response = await api.get(`/quotations/${id}`);
  return response.data?.data;
}

export async function createQuotation(data) {
  const response = await api.post('/quotations', data);
  return response.data?.data;
}

export async function updateQuotation(id, data) {
  const response = await api.put(`/quotations/${id}`, data);
  return response.data?.data;
}

export async function calculateQuotation(id) {
  const response = await api.post(`/quotations/${id}/calculate`);
  return response.data?.data;
}

export async function submitQuotation(id) {
  const response = await api.post(`/quotations/${id}/submit`);
  return response.data?.data;
}

