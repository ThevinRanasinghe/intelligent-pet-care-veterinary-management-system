import api from '../../../lib/apiClient';

/**
 * Medicine & Inventory Management API client for apps/web
 */

export async function getMedicines({ search = '', category = '', lowStockOnly = false, page = 1, pageSize = 10 } = {}) {
  const params = new URLSearchParams();
  if (search) params.append('search', search);
  if (category) params.append('category', category);
  if (lowStockOnly) params.append('lowStockOnly', 'true');
  if (page) params.append('page', page);
  if (pageSize) params.append('pageSize', pageSize);

  const query = params.toString();
  const endpoint = query ? `/medicines?${query}` : '/medicines';
  const response = await api.get(endpoint);
  return response.data;
}

export async function getMedicineById(id) {
  const response = await api.get(`/medicines/${id}`);
  return response.data;
}

export async function createMedicine(data) {
  const response = await api.post('/medicines', data);
  return response.data;
}

export async function receiveStock(medicineId, data) {
  const response = await api.post(`/medicines/${medicineId}/stock-in`, data);
  return response.data;
}

export async function getBatches(medicineId) {
  const response = await api.get(`/medicines/${medicineId}/batches`);
  return response.data;
}

export async function getTransactions(medicineId) {
  const response = await api.get(`/medicines/${medicineId}/transactions`);
  return response.data;
}

export async function getLowStock() {
  const response = await api.get('/medicines/low-stock');
  return response.data;
}

/**
 * Get batches expiring within the next N days (NOT yet expired).
 * Backend param is 'withinDays'.
 */
export async function getExpiring(days = 30) {
  const response = await api.get(`/medicines/expiring?withinDays=${days}`);
  return response.data;
}

/**
 * Get all batches that will expire in the next 9999 days — effectively all
 * future-expiring stock. Use this to build a full expiry picture.
 */
export async function getAllExpiringBatches() {
  const response = await api.get(`/medicines/expiring?withinDays=9999`);
  return response.data;
}

/**
 * Get all batches that are already expired (expiryDate in the past).
 * Uses the dedicated /api/medicines/expired endpoint.
 */
export async function getExpiredStock() {
  const response = await api.get('/medicines/expired');
  return response.data;
}

export async function reserveMedicine(data) {
  const response = await api.post('/medicine-reservations', data);
  return response.data;
}

export async function cancelReservation(reservationId) {
  const response = await api.post(`/medicine-reservations/${reservationId}/cancel`);
  return response.data;
}

export async function dispenseReservation(reservationId) {
  const response = await api.post(`/medicine-reservations/${reservationId}/dispense`);
  return response.data;
}

export async function getSuppliers() {
  const response = await api.get('/suppliers');
  return response.data;
}

export async function createSupplier(data) {
  const response = await api.post('/suppliers', data);
  return response.data;
}
