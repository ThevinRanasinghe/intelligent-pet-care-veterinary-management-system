import type { QuoteLineItem, Quotation } from '../types/domain';
import { ApiError, apiRequest } from './api';

export interface QuotationItemResponse {
  id: string;
  category: string;
  description: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface QuotationResponse {
  id: string;
  appointmentId: string;
  budget: number;
  subtotal: number;
  total: number;
  isWithinBudget: boolean;
  status: string;
  items: QuotationItemResponse[];
  createdAt: string;
  updatedAt: string;
}

export interface QuotationItemRequest {
  category: string;
  description: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateQuotationRequest {
  appointmentId: string;
  budget: number;
  items: QuotationItemRequest[];
}

export interface UpdateQuotationRequest {
  budget: number;
  items: QuotationItemRequest[];
}

const validCategory = (value: string): QuoteLineItem['category'] => {
  const allowed: QuoteLineItem['category'][] = ['Consultation', 'Examination', 'Treatment', 'Medicine', 'Other'];
  return allowed.includes(value as QuoteLineItem['category']) ? (value as QuoteLineItem['category']) : 'Other';
};

function toQuotation(q: QuotationResponse): Quotation {
  const date = q.createdAt ? q.createdAt.slice(0, 10) : '—';
  return {
    id: q.id,
    requestId: q.appointmentId,
    petName: '—',
    ownerName: '—',
    veterinarianName: '—',
    appointmentDate: date,
    appointmentTime: '—',
    branch: '—',
    budget: q.budget,
    subtotal: q.subtotal,
    total: q.total,
    isWithinBudget: q.isWithinBudget,
    status: q.status as Quotation['status'],
    items: q.items.map((item) => ({
      id: item.id,
      category: validCategory(item.category),
      description: item.description,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
    })),
    createdAt: q.createdAt,
    updatedAt: q.updatedAt,
  };
}

export async function getQuotations(): Promise<Quotation[]> {
  const data = await apiRequest<QuotationResponse[]>('/quotations');
  return data.map(toQuotation);
}

export async function getQuotationById(id: string): Promise<Quotation | null> {
  try {
    return toQuotation(await apiRequest<QuotationResponse>(`/quotations/${id}`));
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) return null;
    throw err;
  }
}

export async function createQuotation(request: CreateQuotationRequest): Promise<Quotation> {
  return toQuotation(await apiRequest<QuotationResponse>('/quotations', { method: 'POST', body: JSON.stringify(request) }));
}

export async function updateQuotation(id: string, request: UpdateQuotationRequest): Promise<Quotation> {
  return toQuotation(await apiRequest<QuotationResponse>(`/quotations/${id}`, { method: 'PUT', body: JSON.stringify(request) }));
}

export async function calculateQuotation(id: string): Promise<Quotation> {
  return toQuotation(await apiRequest<QuotationResponse>(`/quotations/${id}/calculate`, { method: 'POST' }));
}

export async function submitQuotationForApproval(id: string): Promise<Quotation> {
  return toQuotation(await apiRequest<QuotationResponse>(`/quotations/${id}/submit`, { method: 'POST' }));
}

export async function finalizeQuotation(id: string): Promise<Quotation> {
  return toQuotation(await apiRequest<QuotationResponse>(`/quotations/${id}/finalize`, { method: 'POST' }));
}

export function calculateQuoteTotal(items: QuoteLineItem[]): number {
  return items.reduce((sum, item) => sum + item.quantity * item.unitPrice, 0);
}
