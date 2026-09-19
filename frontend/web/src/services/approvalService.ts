import type { ApprovalProposal, ValidationCheck } from '../types/domain';
import { ApiError, apiRequest } from './api';
import { getCurrentUser } from './authService';

export interface ApprovalResponse {
  id: string;
  quotationId: string;
  quotationTotal: number;
  quotationBudget: number;
  status: string;
  reviewedBy?: string;
  reviewedAt?: string;
  comment?: string;
}

export interface ApprovalHistoryResponse {
  id: string;
  approvalId: string;
  previousStatus: string;
  newStatus: string;
  changedBy: string;
  reason?: string;
  changedAt: string;
}

export interface ApproveRequest {
  reviewedBy: string;
  comment?: string;
}

export interface RejectRequest {
  reviewedBy: string;
  reason: string;
}

export interface RequestRevisionRequest {
  reviewedBy: string;
  reason: string;
}

const FALLBACK_MANAGER_ID = '00000000-0000-0000-0000-000000000001';

/** The signed-in user's id, so ReviewedBy reflects who actually made the decision. */
function currentReviewerId(): string {
  return getCurrentUser()?.userId ?? FALLBACK_MANAGER_ID;
}

function toProposal(a: ApprovalResponse): ApprovalProposal {
  const budgetCheck: ValidationCheck = {
    key: 'budget',
    label: 'Budget rule',
    passed: a.quotationTotal <= a.quotationBudget,
    detail: a.quotationTotal <= a.quotationBudget
      ? `Quotation total is within the LKR ${a.quotationBudget.toLocaleString()} budget`
      : `Quotation total exceeds the LKR ${a.quotationBudget.toLocaleString()} budget`,
  };

  return {
    id: a.id,
    workflowId: `QUO-${a.quotationId.slice(0, 8)}`,
    requestId: a.quotationId,
    petName: `Quotation ${a.quotationId.slice(0, 8)}`,
    ownerName: '—',
    urgency: 'Routine',
    proposedVet: '—',
    proposedDate: a.reviewedAt ? a.reviewedAt.slice(0, 10) : new Date().toISOString().slice(0, 10),
    proposedTime: '—',
    branch: '—',
    quotationTotal: a.quotationTotal,
    budget: a.quotationBudget,
    status: a.status as ApprovalProposal['status'],
    submittedAt: a.reviewedAt ?? new Date().toISOString(),
    summary: `Approval review for quotation ${a.quotationId}. Current status is ${a.status}.`,
    validationChecks: [budgetCheck],
  };
}

export async function getApprovalProposals(): Promise<ApprovalProposal[]> {
  const data = await apiRequest<ApprovalResponse[]>('/approvals/pending');
  return data.map(toProposal);
}

export async function getApprovalById(id: string): Promise<ApprovalProposal | null> {
  try {
    return toProposal(await apiRequest<ApprovalResponse>(`/approvals/${id}`));
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) return null;
    throw err;
  }
}

export async function getApprovalHistory(id: string): Promise<ApprovalHistoryResponse[]> {
  return await apiRequest<ApprovalHistoryResponse[]>(`/approvals/${id}/history`);
}

export async function approve(id: string, comment?: string): Promise<ApprovalProposal> {
  return toProposal(await apiRequest<ApprovalResponse>(`/approvals/${id}/approve`, {
    method: 'POST',
    body: JSON.stringify({ reviewedBy: currentReviewerId(), comment } as ApproveRequest),
  }));
}

export async function reject(id: string, reason: string): Promise<ApprovalProposal> {
  return toProposal(await apiRequest<ApprovalResponse>(`/approvals/${id}/reject`, {
    method: 'POST',
    body: JSON.stringify({ reviewedBy: currentReviewerId(), reason } as RejectRequest),
  }));
}

export async function requestRevision(id: string, reason: string): Promise<ApprovalProposal> {
  return toProposal(await apiRequest<ApprovalResponse>(`/approvals/${id}/revision`, {
    method: 'POST',
    body: JSON.stringify({ reviewedBy: currentReviewerId(), reason } as RequestRevisionRequest),
  }));
}
