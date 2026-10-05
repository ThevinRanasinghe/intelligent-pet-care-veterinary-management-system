import type {
  InventoryTransaction,
  Medicine,
  MedicineBatch,
  MedicineReservation,
  PagedResult,
  Supplier,
} from '../types/domain';
import { apiRequest } from './api';

export interface CreateMedicineRequest {
  name: string;
  category: string;
  description: string;
  dosageForm: string;
  strength: string;
  unitPrice: number;
  manufacturer: string;
  reorderLevel: number;
}

export interface ReceiveStockRequest {
  supplierId: string;
  batchNumber: string;
  quantity: number;
  expiryDate: string; // YYYY-MM-DD
}

export interface ReserveMedicineRequest {
  medicineId: string;
  quantity: number;
  referenceType?: string;
  referenceId?: string;
}

export interface CreateSupplierRequest {
  name: string;
  contactPerson: string;
  phone: string;
  email: string;
  address: string;
}

export interface SearchMedicinesParams {
  search?: string;
  category?: string;
  lowStockOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export async function getMedicines(params?: SearchMedicinesParams): Promise<PagedResult<Medicine>> {
  const query = new URLSearchParams();
  if (params?.search) query.append('search', params.search);
  if (params?.category) query.append('category', params.category);
  if (params?.lowStockOnly) query.append('lowStockOnly', 'true');
  if (params?.page) query.append('page', params.page.toString());
  if (params?.pageSize) query.append('pageSize', params.pageSize.toString());

  const queryString = query.toString();
  const endpoint = queryString ? `/medicines?${queryString}` : '/medicines';
  return apiRequest<PagedResult<Medicine>>(endpoint);
}

export async function getMedicineById(id: string): Promise<Medicine> {
  return apiRequest<Medicine>(`/medicines/${id}`);
}

export async function createMedicine(data: CreateMedicineRequest): Promise<Medicine> {
  return apiRequest<Medicine>('/medicines', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function receiveStock(medicineId: string, data: ReceiveStockRequest): Promise<MedicineBatch> {
  return apiRequest<MedicineBatch>(`/medicines/${medicineId}/stock-in`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function getBatches(medicineId: string): Promise<MedicineBatch[]> {
  return apiRequest<MedicineBatch[]>(`/medicines/${medicineId}/batches`);
}

export async function getTransactions(medicineId: string): Promise<InventoryTransaction[]> {
  return apiRequest<InventoryTransaction[]>(`/medicines/${medicineId}/transactions`);
}

export async function getLowStock(): Promise<Medicine[]> {
  return apiRequest<Medicine[]>('/medicines/low-stock');
}

export async function getExpiring(days: number = 30): Promise<MedicineBatch[]> {
  return apiRequest<MedicineBatch[]>(`/medicines/expiring?withinDays=${days}`);
}

export async function getReservations(): Promise<MedicineReservation[]> {
  return apiRequest<MedicineReservation[]>('/medicine-reservations');
}

export async function reserveMedicine(data: ReserveMedicineRequest): Promise<MedicineReservation> {
  return apiRequest<MedicineReservation>('/medicine-reservations', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function cancelReservation(reservationId: string): Promise<void> {
  return apiRequest<void>(`/medicine-reservations/${reservationId}/cancel`, {
    method: 'POST',
  });
}

export async function dispenseReservation(reservationId: string): Promise<void> {
  return apiRequest<void>(`/medicine-reservations/${reservationId}/dispense`, {
    method: 'POST',
  });
}

export async function getSuppliers(): Promise<Supplier[]> {
  return apiRequest<Supplier[]>('/suppliers');
}

export async function createSupplier(data: CreateSupplierRequest): Promise<Supplier> {
  return apiRequest<Supplier>('/suppliers', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}
