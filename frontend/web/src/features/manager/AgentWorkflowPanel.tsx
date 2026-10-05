import { useCallback, useEffect, useState } from "react";

import {
  AgentWorkflow,
  AgentWorkflowHistory,
  approve,
  getByConsultation,
  history as loadHistory,
  reject,
  requestRevision,
  run,
  start,
} from "../../services/agentWorkflowService";
import { messageFrom } from "../../utils/errors";

/**
 * Manager-facing panel for the AI-supervised consultation workflow.
 * The workflow is advisory: it proposes a slot/quotation, a ClinicManager
 * approves or rejects it here, and only the approved proposal is booked —
 * by the backend, never by the AI.
 */

/** Owner's consultation request — supplied by the parent details page so
 * the panel can show "what was asked for" without a second fetch. */
export type OwnerRequestSummary = {
  id: string;
  petName?: string | null;
  ownerName?: string | null;
  symptoms?: string | null;
  preferredDate?: string | null;
  preferredTime?: string | null;
  clinic?: string | null;
  urgency?: string | null;
};

type Props = {
  consultationId: string;
  /** Only ClinicManager may approve/reject/revise (mirrors the backend role). */
  canDecide: boolean;
  /** The consultation request being reviewed, when the parent has it. */
  request?: OwnerRequestSummary | null;
  /** Called after a mutating action so the page can refresh its data. */
  onChanged?: () => void;
};

type QuotationShape = {
  items?: { description?: string; quantity?: number; unitPrice?: number }[];
  estimatedTotal?: number;
  total?: number;
  budget?: number;
  withinBudget?: boolean;
};

type ProposalShape = {
  // The backend nests the appointment under "appointment"; older rows
  // (and unit tests) may place the same fields at the top level.
  appointment?: ProposalShape;
  veterinarianId?: string;
  date?: string;
  startTime?: string;
  endTime?: string;
  slot?: string;
  veterinarianName?: string;
  branch?: string;
  appointmentSlotId?: string;
  slotIds?: string[];
  slotCount?: number;
  requiredSlots?: number;
  estimatedDurationMinutes?: number;
  usedPreferredTime?: boolean;
  usedPreferredDate?: boolean;
  fallbackType?: string;
  requestedDate?: string;
  requestedTime?: string;
  reasonCode?: string;
  quotation?: QuotationShape;
  quotationProposal?: QuotationShape;
  validationSummary?: Record<string, boolean | undefined>;
  confidence?: string;
  planningNotes?: string;
  reason?: string;
  disclaimer?: string;
};

const FALLBACK_LABELS: Record<string, string> = {
  none: "Exact requested time",
  same_day_nearest_time: "Same-day nearest available time",
  next_available_date: "Next available date",
};

/** Human explanation of WHY this window was proposed. */
const FALLBACK_REASONS: Record<string, string> = {
  none: "The requested time was available and is proposed as-is.",
  same_day_nearest_time:
    "Preferred time was unavailable; the nearest suitable consecutive slots were found on the preferred date.",
  next_available_date:
    "No suitable consecutive slots were available during the requested date's opening hours; the next permitted date was selected.",
};

/** Friendly text for persisted failure reasons (no internal jargon). */
const FAILURE_LABELS: Record<string, string> = {
  no_valid_slot:
    "No suitable appointment window is currently available within the configured scheduling range.",
  invalid_duration:
    "The AI assessment produced an invalid appointment duration and was stopped safely.",
};

/** Specialist agent ids → demonstrable display names. */
const AGENT_LABELS: Record<string, string> = {
  consultation_agent: "Consultation Agent",
  scheduling_agent: "Scheduling Agent",
  diagnosis_agent: "Diagnosis Agent",
  inventory_agent: "Inventory Agent",
};

const AGENT_TASKS: Record<string, string> = {
  consultation_agent: "Consultation assessment",
  scheduling_agent: "Appointment scheduling",
  diagnosis_agent: "Diagnosis support",
  inventory_agent: "Inventory planning",
};

/** Supervisor trajectory event ids → evaluator-readable labels. */
const EVENT_LABELS: Record<string, string> = {
  plan_created: "Plan created",
  plan_fallback: "Planner fallback — default plan used",
  plan_reused: "Existing plan reused",
  delegated: "Specialist delegated",
  delegation_cap: "Delegation limit reached",
  step_recorded: "Step recorded",
  step_failed: "Step failed",
  step_validated: "Step validated",
  awaiting_event: "Waiting for event",
  awaiting_decision: "Waiting for manager decision",
  decision_approved: "Manager approved",
  decision_rejected: "Manager rejected",
  decision_revisionrequested: "Manager requested revision",
  invalid_decision: "Invalid decision rejected",
  final_validation_passed: "Final validation passed",
  final_validation_failed: "Final validation failed",
  backend_action_emitted: "Booking handed to backend",
  workflow_completed: "Workflow completed",
  workflow_rejected: "Workflow rejected",
  workflow_finished: "Workflow finished",
  safe_failure: "Safe failure",
  iteration_cap: "Iteration limit reached",
};

/** 10-stage human-facing progression, derived from persisted state. */
const STAGE_DEFS = [
  { id: "submitted", label: "Consultation submitted" },
  { id: "analysis", label: "Consultation analysis" },
  { id: "scheduling", label: "Scheduling proposal" },
  { id: "approval", label: "Manager approval" },
  { id: "booking", label: "Appointment booking" },
  { id: "examination", label: "Examination" },
  { id: "diagnosis", label: "Diagnosis" },
  { id: "prescription", label: "Prescription" },
  { id: "inventory", label: "Inventory" },
  { id: "completed", label: "Completed" },
] as const;

type StageState = "done" | "current" | "failed" | "skipped" | "upcoming";

/** Structured scheduling assessment emitted by the consultation agent. */
type ConsultationAssessmentShape = {
  priority?: string;
  complexity?: string;
  consultationType?: string;
  requiredSlots?: number;
  estimatedDurationMinutes?: number;
  recommendedChecks?: string[];
  schedulingReason?: string;
  confidence?: number;
  keyConcerns?: { concern?: string; reason?: string }[];
};

type PlanStep = {
  stepNumber?: number;
  type?: string;
  agent?: string;
  agentName?: string;
  purpose?: string;
  requires?: string | string[];
};

const cardStyle: React.CSSProperties = {
  marginBottom: "18px",
  padding: "16px",
  border: "1px solid #e8e8e2",
  borderRadius: "10px",
  background: "#fafaf7",
};

const labelStyle: React.CSSProperties = {
  marginBottom: "4px",
  fontSize: "11px",
  fontWeight: 800,
  color: "#333333",
};

const hintStyle: React.CSSProperties = {
  marginBottom: "12px",
  fontSize: "10px",
  color: "#888888",
  lineHeight: 1.5,
};

const errorStyle: React.CSSProperties = {
  marginTop: "10px",
  padding: "10px 12px",
  border: "1px solid #f2b8b5",
  borderRadius: "8px",
  background: "#fff1f0",
  color: "#b42318",
  fontSize: "12px",
};

const buttonStyle: React.CSSProperties = {
  height: "32px",
  padding: "0 14px",
  border: "1px solid #d8d8d2",
  borderRadius: "8px",
  background: "#ffffff",
  color: "#171717",
  fontSize: "11px",
  fontWeight: 700,
  cursor: "pointer",
};

const primaryButtonStyle: React.CSSProperties = {
  ...buttonStyle,
  border: "none",
  background: "#f5c400",
};

const dangerButtonStyle: React.CSSProperties = {
  ...buttonStyle,
  color: "#b42318",
};

const thStyle: React.CSSProperties = {
  padding: "6px 8px",
  textAlign: "left",
  fontSize: "9px",
  fontWeight: 800,
  color: "#777777",
  borderBottom: "1px solid #e8e8e2",
};

const tdStyle: React.CSSProperties = {
  padding: "6px 8px",
  fontSize: "11px",
  color: "#444444",
  borderBottom: "1px solid #f0f0ea",
  verticalAlign: "top",
};

function statusBadgeClass(status: string): string {
  switch (status) {
    case "Completed":
    case "AwaitingExamination":
      return "badge badge-success";
    case "PendingManagerApproval":
    case "AwaitingPrescription":
      return "badge badge-info";
    case "Rejected":
    case "Failed":
      return "badge badge-danger";
    default:
      return "badge badge-warning";
  }
}

function asProposal(raw: unknown): ProposalShape | null {
  if (raw && typeof raw === "object" && !Array.isArray(raw)) {
    return raw as ProposalShape;
  }
  return null;
}

function asPlanSteps(raw: unknown): PlanStep[] {
  if (raw && typeof raw === "object") {
    const steps = (raw as { steps?: unknown }).steps;
    if (Array.isArray(steps)) return steps as PlanStep[];
  }
  if (Array.isArray(raw)) return raw as PlanStep[];
  return [];
}

type ToolCallShape = {
  tool?: string;
  ok?: boolean;
  durationMs?: number;
  duration?: number;
  error?: string;
};

function asToolCalls(raw: unknown): ToolCallShape[] {
  return Array.isArray(raw) ? (raw as ToolCallShape[]) : [];
}

function formatDuration(minutes?: number | null): string {
  if (!minutes || minutes <= 0) return "—";
  if (minutes % 60 === 0) {
    const hours = minutes / 60;
    return `${hours} hour${hours === 1 ? "" : "s"}`;
  }
  return `${minutes} minutes`;
}

function formatRequested(
  date?: string | null,
  time?: string | null,
): string {
  if (!date) return "—";
  const parsed = new Date(`${date}T00:00:00`);
  const label = Number.isNaN(parsed.getTime())
    ? date
    : parsed.toLocaleDateString("en-US", {
        month: "long",
        day: "numeric",
        year: "numeric",
      });
  return time ? `${label}, ${time}` : label;
}

/** Computes the 10-stage progression from persisted workflow state only. */
function stageStates(workflow: AgentWorkflow): StageState[] {
  const steps = workflow.steps ?? [];
  const agentDid = (agent: string, statuses: string[]) =>
    steps.some((s) => s.agentName === agent && statuses.includes(s.status));
  const approved = (workflow.approvals ?? []).some((a) => a.status === "Approved");
  const booked =
    steps.some((s) => s.task === "backend_action" && s.status === "Completed") ||
    workflow.approvedAction != null ||
    ["AwaitingExamination", "AwaitingPrescription", "Completed"].includes(
      workflow.status,
    );
  const examined =
    agentDid("diagnosis_agent", ["Completed"]) ||
    ["AwaitingPrescription", "Completed"].includes(workflow.status);

  const done: Record<string, boolean> = {
    submitted: true, // a persisted workflow exists only after submission
    analysis: agentDid("consultation_agent", ["Completed"]),
    scheduling:
      workflow.proposal != null ||
      agentDid("scheduling_agent", ["Completed", "NoProposal"]),
    approval: approved,
    booking: booked,
    examination: examined,
    diagnosis: agentDid("diagnosis_agent", ["Completed"]),
    prescription:
      agentDid("inventory_agent", ["Completed"]) ||
      workflow.status === "Completed",
    inventory: agentDid("inventory_agent", ["Completed"]),
    completed: workflow.status === "Completed",
  };

  const terminal = workflow.status === "Failed" || workflow.status === "Rejected";
  let currentFound = false;
  return STAGE_DEFS.map(({ id }) => {
    if (done[id]) return "done";
    if (!currentFound && !terminal) {
      currentFound = true;
      return "current";
    }
    if (!currentFound && terminal) {
      currentFound = true;
      return "failed";
    }
    return terminal ? "skipped" : "upcoming";
  });
}

export function AgentWorkflowPanel({ consultationId, canDecide, request, onChanged }: Props) {
  const [workflow, setWorkflow] = useState<AgentWorkflow | null>(null);
  const [checked, setChecked] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [comment, setComment] = useState("");
  const [commentError, setCommentError] = useState("");
  const [historyData, setHistoryData] = useState<AgentWorkflowHistory | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);

  const refresh = useCallback(async () => {
    try {
      setError("");
      const result = await getByConsultation(consultationId);
      setWorkflow(result && typeof result === "object" && !Array.isArray(result) ? result : null);
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setChecked(true);
    }
  }, [consultationId]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  async function act(
    label: string,
    call: () => Promise<AgentWorkflow>,
    requiresComment = false,
  ) {
    if (requiresComment && !comment.trim()) {
      setCommentError("A comment is required for this decision.");
      return;
    }
    setCommentError("");
    setBusy(label);
    setError("");
    try {
      setWorkflow(await call());
      setComment("");
      onChanged?.();
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setBusy(null);
    }
  }

  async function handleRun() {
    if (workflow) {
      await act("run", () => run(workflow.id));
    } else {
      await act("start", async () => {
        const created = await start(consultationId);
        return run(created.id);
      });
    }
  }

  async function toggleHistory() {
    if (historyOpen) {
      setHistoryOpen(false);
      return;
    }
    if (!workflow) return;
    setHistoryLoading(true);
    try {
      setHistoryData(await loadHistory(workflow.id));
      setHistoryOpen(true);
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setHistoryLoading(false);
    }
  }

  if (!checked) {
    return (
      <div style={cardStyle} data-testid="agent-workflow-panel">
        <div style={labelStyle}>AI Workflow</div>
        <div style={{ fontSize: "12px", color: "#666666" }}>
          Checking workflow status…
        </div>
      </div>
    );
  }

  const status = workflow?.status ?? null;
  const proposal = asProposal(workflow?.proposal);
  // The appointment may be nested under "appointment" (newer proposals)
  // or flat on the proposal itself (legacy shapes).
  const appointment = proposal?.appointment ?? proposal;
  const quotation: QuotationShape | null =
    proposal?.quotation ?? proposal?.quotationProposal ?? null;
  // The consultation agent's assessment lives on its step record; surface
  // the scheduling fields so the manager can see WHY N slots are proposed.
  const consultationStep = (workflow?.steps ?? []).find(
    (s) => s.agentName === "consultation_agent" && s.status === "Completed",
  );
  const assessmentRaw: unknown = consultationStep?.output;
  const assessment: ConsultationAssessmentShape | null =
    assessmentRaw && typeof assessmentRaw === "object" && !Array.isArray(assessmentRaw)
      ? (assessmentRaw as ConsultationAssessmentShape)
      : null;
  const pendingApproval = (workflow?.approvals ?? []).find(
    (a) => a.status === "Pending",
  );
  const runnable =
    status === null || ["Created", "Running", "Failed"].includes(status);

  return (
    <div style={cardStyle} data-testid="agent-workflow-panel">
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: "10px",
          marginBottom: "4px",
        }}
      >
        <div style={{ ...labelStyle, marginBottom: 0 }}>AI Workflow</div>
        {status && (
          <span
            className={statusBadgeClass(status)}
            data-testid="workflow-status"
            role="status"
            aria-live="polite"
          >
            {status}
          </span>
        )}
      </div>
      <div style={hintStyle}>
        AI-supervised workflow: triage → scheduling proposal → manager
        approval → booking. Advisory only — the appointment is booked by the
        backend after your approval.
      </div>

      {/* Workflow progression — derived from persisted state only */}
      {workflow && (
        <ol
          aria-label="Workflow progress"
          data-testid="workflow-progress"
          style={{
            display: "flex",
            flexWrap: "wrap",
            gap: "6px",
            padding: 0,
            margin: "0 0 14px",
            listStyle: "none",
          }}
        >
          {STAGE_DEFS.map(({ id, label }, i) => {
            const state = stageStates(workflow)[i];
            const colors: Record<StageState, { bg: string; fg: string; border: string }> = {
              done: { bg: "#ecfdf5", fg: "#166534", border: "#a7f3d0" },
              current: { bg: "#fefce8", fg: "#854d0e", border: "#fde68a" },
              failed: { bg: "#fef2f2", fg: "#b42318", border: "#f2b8b5" },
              skipped: { bg: "#f4f4f2", fg: "#9a9a94", border: "#e8e8e2" },
              upcoming: { bg: "#ffffff", fg: "#777770", border: "#e8e8e2" },
            };
            const c = colors[state];
            return (
              <li
                key={id}
                aria-current={state === "current" ? "step" : undefined}
                title={`${label} — ${state}`}
                style={{
                  padding: "4px 10px",
                  border: `1px solid ${c.border}`,
                  borderRadius: "999px",
                  background: c.bg,
                  color: c.fg,
                  fontSize: "10px",
                  fontWeight: state === "current" ? 800 : 600,
                  whiteSpace: "nowrap",
                }}
              >
                {state === "done" ? "✓ " : state === "failed" ? "✗ " : ""}
                {label}
              </li>
            );
          })}
        </ol>
      )}

      {/* Owner request — what the owner actually asked for */}
      {request && (
        <section
          aria-label="Owner request"
          style={{
            marginBottom: "12px",
            padding: "12px",
            border: "1px solid #e8e8e2",
            borderRadius: "9px",
            background: "#ffffff",
            fontSize: "12px",
            color: "#444444",
            lineHeight: 1.6,
          }}
        >
          <strong>Owner request</strong>
          <div style={{ marginTop: "6px", display: "grid", gap: "4px" }}>
            {(request.petName || request.ownerName) && (
              <div>
                Pet: {request.petName ?? "—"}
                {request.ownerName ? ` (owner: ${request.ownerName})` : ""}
              </div>
            )}
            {(request.preferredDate || request.preferredTime) && (
              <div>
                Preferred: {formatRequested(request.preferredDate, request.preferredTime)}
              </div>
            )}
            {request.urgency && <div>Urgency: {request.urgency}</div>}
            {request.clinic && <div>Clinic: {request.clinic}</div>}
            {request.symptoms && (
              <div
                style={{
                  whiteSpace: "pre-wrap",
                  overflowWrap: "anywhere",
                }}
              >
                Symptoms: {request.symptoms}
              </div>
            )}
          </div>
        </section>
      )}

      {/* Consultation assessment — what the Consultation Agent determined */}
      {assessment && (
        <section
          aria-label="Consultation assessment"
          data-testid="consultation-assessment"
          style={{
            marginBottom: "12px",
            padding: "12px",
            border: "1px solid #e8e8e2",
            borderRadius: "9px",
            background: "#ffffff",
            fontSize: "12px",
            color: "#444444",
            lineHeight: 1.6,
          }}
        >
          <strong>Consultation assessment</strong>
          <div style={{ marginTop: "6px", display: "grid", gap: "4px" }}>
            {assessment.priority && <div>Severity: {assessment.priority}</div>}
            {assessment.complexity && (
              <div>
                Complexity:{" "}
                {assessment.complexity.charAt(0).toUpperCase() +
                  assessment.complexity.slice(1)}
              </div>
            )}
            {assessment.estimatedDurationMinutes != null && (
              <div>
                Estimated appointment time:{" "}
                {formatDuration(assessment.estimatedDurationMinutes)}
              </div>
            )}
            {assessment.requiredSlots != null && (
              <div>
                Required slots: {assessment.requiredSlots}
              </div>
            )}
            {(assessment.recommendedChecks ?? []).length > 0 && (
              <div>
                Recommended examination:{" "}
                {(assessment.recommendedChecks ?? []).join(", ")}
              </div>
            )}
            {assessment.schedulingReason && (
              <div style={{ overflowWrap: "anywhere" }}>
                Reason: {assessment.schedulingReason}
              </div>
            )}
            {assessment.confidence != null && (
              <div>
                Confidence: {Math.round(assessment.confidence * 100)}%
              </div>
            )}
          </div>
        </section>
      )}

      {status === "AwaitingExamination" && (
        <div style={{ fontSize: "12px", color: "#166534", marginBottom: "10px" }}>
          Appointment booked from approved proposal.
        </div>
      )}
      {status === "Rejected" && (
        <div style={{ fontSize: "12px", color: "#b42318", marginBottom: "10px" }}>
          The proposal was rejected by the clinic manager.
        </div>
      )}
      {status === "Failed" && workflow?.failureReason && (
        <div style={{ fontSize: "12px", color: "#b42318", marginBottom: "10px" }}>
          {FAILURE_LABELS[workflow.failureReason] ??
            `Workflow failed safely: ${workflow.failureReason}`}{" "}
          The consultation remains available for manual scheduling.
        </div>
      )}
      {status === "AwaitingEvent" && (
        <div style={{ fontSize: "12px", color: "#666666", marginBottom: "10px" }}>
          The workflow is waiting for the next business event (for example an
          examination or prescription) before it can continue.
        </div>
      )}
      {status === null && (
        <div style={{ fontSize: "12px", color: "#666666", marginBottom: "10px" }}>
          No AI workflow exists for this consultation yet.
        </div>
      )}

      {/* Pending proposal + decision actions */}
      {status === "PendingManagerApproval" && (
        <section
          aria-label="Scheduling proposal awaiting approval"
          data-testid="scheduling-proposal"
          style={{
            marginBottom: "12px",
            padding: "12px",
            border: "1px solid #e8e8e2",
            borderRadius: "9px",
            background: "#ffffff",
            fontSize: "12px",
            color: "#444444",
            lineHeight: 1.6,
          }}
        >
          <strong>Scheduling proposal — pending your approval</strong>
          {proposal && (
            <div style={{ marginTop: "6px", display: "grid", gap: "6px" }}>
              {(appointment?.requestedDate || appointment?.requestedTime) && (
                <div>
                  Requested:{" "}
                  {formatRequested(
                    appointment.requestedDate,
                    appointment.requestedTime,
                  )}
                </div>
              )}
              {(appointment?.veterinarianName || appointment?.veterinarianId) && (
                <div>
                  Veterinarian:{" "}
                  {appointment.veterinarianName ?? appointment.veterinarianId}
                </div>
              )}
              {(appointment?.date || appointment?.slot) && (
                <div>
                  Proposed:{" "}
                  {formatRequested(appointment.date ?? appointment.slot)}{" "}
                  {appointment.startTime}
                  {appointment.endTime ? ` – ${appointment.endTime}` : ""}
                  {appointment.branch ? ` — ${appointment.branch}` : ""}
                </div>
              )}
              {appointment && (appointment.slotCount ?? 0) > 0 && (
                <div>
                  Required slots: {appointment.slotCount}
                  {appointment.estimatedDurationMinutes
                    ? ` (${formatDuration(appointment.estimatedDurationMinutes)})`
                    : ""}
                </div>
              )}
              {appointment && appointment.usedPreferredTime != null && (
                <div>
                  Preferred time available:{" "}
                  {appointment.usedPreferredTime ? "Yes" : "No"}
                </div>
              )}
              {appointment?.fallbackType && (
                <div>
                  Fallback:{" "}
                  {FALLBACK_LABELS[appointment.fallbackType] ??
                    appointment.fallbackType}
                </div>
              )}
              {appointment?.fallbackType && (
                <div style={{ overflowWrap: "anywhere" }}>
                  Why:{" "}
                  {FALLBACK_REASONS[appointment.fallbackType] ??
                    "The proposed window was selected from available published slots."}
                </div>
              )}
              {quotation && (
                <div>
                  Quotation total:{" "}
                  {quotation.estimatedTotal ?? quotation.total ?? "—"}
                  {quotation.budget
                    ? ` (budget: ${quotation.budget} — ${
                        quotation.withinBudget ? "within budget" : "over budget"
                      })`
                    : ""}
                  {quotation.items && quotation.items.length > 0 && (
                    <ul style={{ margin: "4px 0 0", paddingLeft: "18px" }}>
                      {quotation.items.map((item, i) => (
                        <li key={i}>
                          {item.description} ×{item.quantity ?? 1} —{" "}
                          {item.unitPrice}
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              )}
              {proposal.validationSummary && (
                <ul style={{ margin: "4px 0 0", paddingLeft: "18px" }}>
                  {Object.entries(proposal.validationSummary).map(
                    ([key, value]) => (
                      <li key={key}>
                        {key}: {value ? "Yes" : "No"}
                      </li>
                    ),
                  )}
                </ul>
              )}
              {proposal.confidence && (
                <div>Confidence: {proposal.confidence}</div>
              )}
              {(proposal.planningNotes || proposal.reason) && (
                <div>Notes: {proposal.planningNotes ?? proposal.reason}</div>
              )}
              {proposal.disclaimer && (
                <div style={{ fontSize: "10px", color: "#888888" }}>
                  {proposal.disclaimer}
                </div>
              )}
            </div>
          )}

          {canDecide && (
            <div style={{ marginTop: "10px" }}>
              <textarea
                aria-label="Decision comment"
                placeholder="Comment (required to reject or request a revision)"
                value={comment}
                onChange={(event) => setComment(event.target.value)}
                rows={2}
                style={{
                  width: "100%",
                  marginBottom: "8px",
                  padding: "8px 10px",
                  border: "1px solid #d8d8d2",
                  borderRadius: "8px",
                  fontSize: "12px",
                  resize: "vertical",
                  boxSizing: "border-box",
                }}
              />
              {commentError && (
                <div
                  role="alert"
                  style={{
                    marginBottom: "8px",
                    fontSize: "11px",
                    color: "#b42318",
                  }}
                >
                  {commentError}
                </div>
              )}
              <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
                <button
                  type="button"
                  style={primaryButtonStyle}
                  disabled={busy !== null}
                  onClick={() =>
                    workflow &&
                    window.confirm("Approve this proposal and book the appointment?") &&
                    void act("approve", () => approve(workflow.id, comment.trim() || undefined))
                  }
                >
                  {busy === "approve" ? "Approving…" : "Approve"}
                </button>
                <button
                  type="button"
                  style={dangerButtonStyle}
                  disabled={busy !== null}
                  onClick={() =>
                    workflow &&
                    window.confirm("Reject this proposal?") &&
                    void act("reject", () => reject(workflow.id, comment.trim()), true)
                  }
                >
                  {busy === "reject" ? "Rejecting…" : "Reject"}
                </button>
                <button
                  type="button"
                  style={buttonStyle}
                  disabled={busy !== null}
                  onClick={() =>
                    workflow &&
                    void act(
                      "revision",
                      () => requestRevision(workflow.id, comment.trim()),
                      true,
                    )
                  }
                >
                  {busy === "revision" ? "Sending…" : "Request revision"}
                </button>
              </div>
            </div>
          )}
        </section>
      )}

      {/* Specialist agents — what each agent actually did (persisted steps) */}
      {(workflow?.steps ?? []).filter((s) => s.agentName).length > 0 && (
        <section
          aria-label="Specialist agents"
          data-testid="specialist-agents"
          style={{
            marginBottom: "12px",
            fontSize: "11px",
            color: "#444444",
            display: "grid",
            gap: "4px",
          }}
        >
          {(workflow?.steps ?? [])
            .filter((s) => s.agentName)
            .map((s) => (
              <div key={s.id}>
                <strong>
                  {AGENT_LABELS[s.agentName ?? ""] ?? s.agentName}
                </strong>
                {" — "}
                {AGENT_TASKS[s.agentName ?? ""] ?? s.task}
                {" — "}
                {s.status === "NoProposal" ? "No proposal found" : s.status}
                {s.error ? ` — ${s.error}` : ""}
              </div>
            ))}
        </section>
      )}

      <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
        {runnable && (
          <button
            type="button"
            style={primaryButtonStyle}
            disabled={busy !== null}
            onClick={() => void handleRun()}
          >
            {busy === "run" || busy === "start"
              ? "Running…"
              : status === null
                ? "Start AI workflow"
                : "Run AI workflow"}
          </button>
        )}
        {workflow && (
          <button
            type="button"
            style={buttonStyle}
            disabled={historyLoading}
            aria-expanded={historyOpen}
            onClick={() => void toggleHistory()}
          >
            {historyLoading
              ? "Loading history…"
              : historyOpen
                ? "Hide execution history"
                : "View execution history"}
          </button>
        )}
      </div>

      {error && (
        <div style={errorStyle} role="alert">
          AI workflow action failed — the normal workflow is unaffected.{" "}
          {error}
        </div>
      )}

      {historyOpen && historyData && (
        <div style={{ marginTop: "14px", display: "grid", gap: "14px" }}>
          {/* Plan steps */}
          {asPlanSteps(historyData.workflow.plan).length > 0 && (
            <div>
              <div style={labelStyle}>Plan</div>
              <table style={{ width: "100%", borderCollapse: "collapse" }}>
                <thead>
                  <tr>
                    <th style={thStyle}>Step</th>
                    <th style={thStyle}>Type</th>
                    <th style={thStyle}>Agent</th>
                    <th style={thStyle}>Purpose</th>
                    <th style={thStyle}>Requires</th>
                  </tr>
                </thead>
                <tbody>
                  {asPlanSteps(historyData.workflow.plan).map((step, i) => (
                    <tr key={i}>
                      <td style={tdStyle}>{step.stepNumber ?? i + 1}</td>
                      <td style={tdStyle}>{step.type ?? "—"}</td>
                      <td style={tdStyle}>{step.agent ?? step.agentName ?? "—"}</td>
                      <td style={tdStyle}>{step.purpose ?? "—"}</td>
                      <td style={tdStyle}>
                        {Array.isArray(step.requires)
                          ? step.requires.join(", ")
                          : (step.requires ?? "—")}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Agent steps */}
          {historyData.steps.length > 0 && (
            <div>
              <div style={labelStyle}>Agent steps</div>
              <table style={{ width: "100%", borderCollapse: "collapse" }}>
                <thead>
                  <tr>
                    <th style={thStyle}>Step</th>
                    <th style={thStyle}>Agent</th>
                    <th style={thStyle}>Task</th>
                    <th style={thStyle}>Status</th>
                    <th style={thStyle}>Retries</th>
                    <th style={thStyle}>Tool calls</th>
                  </tr>
                </thead>
                <tbody>
                  {historyData.steps.map((step) => (
                    <tr key={step.id}>
                      <td style={tdStyle}>{step.stepNumber}</td>
                      <td style={tdStyle}>{step.agentName ?? "—"}</td>
                      <td style={tdStyle}>{step.task}</td>
                      <td style={tdStyle}>
                        {step.status}
                        {step.validation ? (
                          <div style={{ fontSize: "9px", color: "#888888" }}>
                            {JSON.stringify(step.validation)}
                          </div>
                        ) : null}
                        {step.error ? (
                          <div style={{ fontSize: "9px", color: "#b42318" }}>
                            {step.error}
                          </div>
                        ) : null}
                      </td>
                      <td style={tdStyle}>{step.retryCount}</td>
                      <td style={tdStyle}>
                        {asToolCalls(step.toolCalls).map((call, i) => (
                          <div key={i}>
                            {call.tool ?? "?"} —{" "}
                            {call.ok === false ? "✗ failed" : "✓ ok"}
                            {call.durationMs != null
                              ? ` (${call.durationMs}ms)`
                              : call.duration != null
                                ? ` (${call.duration}ms)`
                                : ""}
                            {call.error ? (
                              <div style={{ fontSize: "9px", color: "#b42318" }}>
                                {call.error}
                              </div>
                            ) : null}
                          </div>
                        ))}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Approvals */}
          {historyData.approvals.length > 0 && (
            <div>
              <div style={labelStyle}>Approvals</div>
              <table style={{ width: "100%", borderCollapse: "collapse" }}>
                <thead>
                  <tr>
                    <th style={thStyle}>Status</th>
                    <th style={thStyle}>Decided by</th>
                    <th style={thStyle}>Decided at</th>
                    <th style={thStyle}>Comments</th>
                  </tr>
                </thead>
                <tbody>
                  {historyData.approvals.map((approval) => (
                    <tr key={approval.id}>
                      <td style={tdStyle}>{approval.status}</td>
                      <td style={tdStyle}>
                        {approval.decidedByUserId ?? "—"}
                      </td>
                      <td style={tdStyle}>
                        {approval.decidedAt
                          ? new Date(approval.decidedAt).toLocaleString()
                          : "—"}
                      </td>
                      <td style={tdStyle}>{approval.comments ?? "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Trajectory events */}
          {historyData.events.length > 0 && (
            <div>
              <div style={labelStyle}>Trajectory events</div>
              <table style={{ width: "100%", borderCollapse: "collapse" }}>
                <thead>
                  <tr>
                    <th style={thStyle}>Seq</th>
                    <th style={thStyle}>Timestamp</th>
                    <th style={thStyle}>Node</th>
                    <th style={thStyle}>Event</th>
                    <th style={thStyle}>Agent</th>
                  </tr>
                </thead>
                <tbody>
                  {historyData.events.map((event) => {
                    const detail = (event.detail ?? {}) as {
                      waitingFor?: string[];
                      reason?: string;
                    };
                    const waitingFor =
                      Array.isArray(detail.waitingFor) && detail.waitingFor.length > 0
                        ? ` — waiting for ${detail.waitingFor.join(", ")}`
                        : "";
                    const reason = detail.reason ? ` — ${detail.reason}` : "";
                    return (
                      <tr key={event.seq}>
                        <td style={tdStyle}>{event.seq}</td>
                        <td style={tdStyle}>
                          {event.timestamp
                            ? new Date(event.timestamp).toLocaleString()
                            : "—"}
                        </td>
                        <td style={tdStyle}>{event.node}</td>
                        <td style={tdStyle}>
                          {EVENT_LABELS[event.eventType] ?? event.eventType}
                          {waitingFor}
                          {reason}
                          <div style={{ fontSize: "9px", color: "#999999" }}>
                            {event.eventType}
                          </div>
                        </td>
                        <td style={tdStyle}>
                          {AGENT_LABELS[event.agentName ?? ""] ??
                            event.agentName ??
                            "—"}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
