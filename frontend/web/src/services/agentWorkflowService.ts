import { ApiError, apiRequest } from "./api";

/**
 * AI-supervised consultation workflows (advisory). All calls go to the
 * ASP.NET API — the Python agentic service is never contacted directly.
 * The backend remains authoritative: approvals come from a ClinicManager
 * and the appointment is booked by the consultation workflow, not the AI.
 */

export type AgentWorkflowStep = {
  id: string;
  stepNumber: number;
  agentName?: string | null;
  task: string;
  inputSummary?: unknown;
  output?: unknown;
  toolCalls?: unknown;
  status: string;
  validation?: unknown;
  error?: string | null;
  retryCount: number;
  startedAt: string;
  completedAt?: string | null;
};

export type AgentWorkflowApproval = {
  id: string;
  stepNumber: number;
  status: string;
  proposal?: unknown;
  decidedByUserId?: string | null;
  decidedAt?: string | null;
  comments?: string | null;
  createdAt: string;
};

export type AgentWorkflowEvent = {
  seq: number;
  timestamp: string;
  node: string;
  agentName?: string | null;
  eventType: string;
  detail?: unknown;
};

export type AgentWorkflow = {
  id: string;
  consultationRequestId: string;
  organizationId?: string | null;
  objective: string;
  status: string;
  currentStep: number;
  plan?: unknown;
  proposal?: unknown;
  approvedAction?: unknown;
  delegationCount: number;
  revisionCount: number;
  failureReason?: string | null;
  createdAt: string;
  updatedAt: string;
  completedAt?: string | null;
  steps: AgentWorkflowStep[];
  approvals: AgentWorkflowApproval[];
};

export type AgentWorkflowHistory = {
  workflow: AgentWorkflow;
  steps: AgentWorkflowStep[];
  approvals: AgentWorkflowApproval[];
  events: AgentWorkflowEvent[];
};

/** Reduced view returned to PetOwner callers on the by-consultation route. */
export type AgentWorkflowStatusView = {
  workflowId: string;
  status: string;
  updatedAt: string;
};

const base = "/agent-workflows";

/**
 * The workflow for a consultation, or null when none exists yet. Staff
 * callers receive the full AgentWorkflow; PetOwner callers receive the
 * reduced status view (handled by the backend).
 */
export async function getByConsultation(
  consultationId: string,
): Promise<AgentWorkflow | null> {
  try {
    return await apiRequest<AgentWorkflow>(
      `${base}/by-consultation/${encodeURIComponent(consultationId)}`,
    );
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) return null;
    throw err;
  }
}

export function get(id: string): Promise<AgentWorkflow> {
  return apiRequest<AgentWorkflow>(`${base}/${encodeURIComponent(id)}`);
}

export function start(consultationId: string): Promise<AgentWorkflow> {
  return apiRequest<AgentWorkflow>(`${base}/start`, {
    method: "POST",
    body: JSON.stringify({ consultationRequestId: consultationId }),
  });
}

export function run(id: string): Promise<AgentWorkflow> {
  return apiRequest<AgentWorkflow>(`${base}/${encodeURIComponent(id)}/run`, {
    method: "POST",
  });
}

export function approve(id: string, comments?: string): Promise<AgentWorkflow> {
  return apiRequest<AgentWorkflow>(
    `${base}/${encodeURIComponent(id)}/approve`,
    { method: "POST", body: JSON.stringify({ comments: comments ?? null }) },
  );
}

export function reject(id: string, comments: string): Promise<AgentWorkflow> {
  return apiRequest<AgentWorkflow>(
    `${base}/${encodeURIComponent(id)}/reject`,
    { method: "POST", body: JSON.stringify({ comments }) },
  );
}

export function requestRevision(
  id: string,
  comments: string,
): Promise<AgentWorkflow> {
  return apiRequest<AgentWorkflow>(
    `${base}/${encodeURIComponent(id)}/revision`,
    { method: "POST", body: JSON.stringify({ comments }) },
  );
}

export function history(id: string): Promise<AgentWorkflowHistory> {
  return apiRequest<AgentWorkflowHistory>(
    `${base}/${encodeURIComponent(id)}/history`,
  );
}
