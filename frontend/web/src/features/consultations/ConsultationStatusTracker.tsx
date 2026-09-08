import React, { useState } from "react";
import {
  Activity,
  AlertCircle,
  Calendar,
  Check,
  CheckCircle2,
  Clock,
  Clock3,
  Copy,
  ExternalLink,
  Filter,
  History,
  Image as ImageIcon,
  Loader2,
  MapPin,
  Navigation,
  PawPrint,
  RefreshCw,
  Search,
  Settings2,
  ShieldAlert,
  Sparkles,
  User,
  Wallet,
  XCircle,
} from "lucide-react";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import { Modal } from "../../components/ui/Modal";
import {
  ConsultationStatus,
  consultationService,
  isValidRequestId,
  type ConsultationRequest,
  type ConsultationStatusTrackingDto,
  type ConsultationHistoryItemDto,
  type ConsultationStatusString,
} from "../../services/api";
import { formatDate, formatLkr } from "../../utils/format";

interface ConsultationStatusTrackerProps {
  consultations: ConsultationRequest[];
  loading?: boolean;
  onRefresh: () => void;
  onNewConsultation: () => void;
}

const ALL_STATUS_ENUMS: ConsultationStatusString[] = [
  "Submitted",
  "Processing",
  "PendingApproval",
  "Approved",
  "Rejected",
  "RevisionRequired",
  "AppointmentConfirmed",
];

export const ConsultationStatusTracker: React.FC<ConsultationStatusTrackerProps> = ({
  consultations,
  loading = false,
  onRefresh,
  onNewConsultation,
}) => {
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("All");
  const [copiedId, setCopiedId] = useState<string | null>(null);

  // Quick lookup state
  const [lookupId, setLookupId] = useState("");
  const [lookupLoading, setLookupLoading] = useState(false);
  const [lookupResult, setLookupResult] = useState<ConsultationStatusTrackingDto | null>(null);
  const [lookupError, setLookupError] = useState<string | null>(null);

  // Audit history modal state
  const [historyModalOpen, setHistoryModalOpen] = useState(false);
  const [activeRequestHistory, setActiveRequestHistory] = useState<ConsultationHistoryItemDto[]>([]);
  const [historyRequestId, setHistoryRequestId] = useState<string>("");
  const [historyLoading, setHistoryLoading] = useState(false);

  // Status transition state (Staff action: PATCH /api/Consultations/{id}/status)
  const [statusModalOpen, setStatusModalOpen] = useState(false);
  const [selectedReqForStatus, setSelectedReqForStatus] = useState<ConsultationRequest | null>(null);
  const [newStatusEnum, setNewStatusEnum] = useState<ConsultationStatus>(ConsultationStatus.Processing);
  const [statusComments, setStatusComments] = useState("");
  const [transitioningStatus, setTransitioningStatus] = useState(false);
  const [transitionError, setTransitionError] = useState<string | null>(null);

  const copyToClipboard = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 1800);
  };

  const getStatusTone = (
    status: string
  ): "warning" | "success" | "danger" | "info" | "neutral" => {
    switch (status?.toLowerCase()) {
      case "submitted":
        return "neutral";
      case "processing":
        return "info";
      case "pendingapproval":
        return "warning";
      case "approved":
      case "appointmentconfirmed":
      case "confirmed":
        return "success";
      case "rejected":
        return "danger";
      case "revisionrequired":
        return "warning";
      default:
        return "neutral";
    }
  };

  // Quick lookup handler
  const handleLookupStatus = async (e: React.FormEvent) => {
    e.preventDefault();
    setLookupError(null);
    setLookupResult(null);

    const trimmed = lookupId.trim().toUpperCase();
    if (!trimmed) {
      setLookupError("Please enter a Consultation Short ID (e.g. REQ-5001).");
      return;
    }

    if (!isValidRequestId(trimmed)) {
      setLookupError("Invalid Consultation ID format (expected Short ID: REQ-xxxx, e.g. REQ-5001).");
      return;
    }

    setLookupLoading(true);
    try {
      const res = await consultationService.getStatus(trimmed);
      setLookupResult(res);
    } catch (err: any) {
      setLookupError(err.message || `Consultation request '${trimmed}' not found.`);
    } finally {
      setLookupLoading(false);
    }
  };

  // View Audit History
  const handleViewAuditHistory = async (reqId: string) => {
    setHistoryRequestId(reqId);
    setHistoryModalOpen(true);
    setHistoryLoading(true);
    try {
      const history = await consultationService.getHistory(reqId);
      setActiveRequestHistory(history);
    } catch {
      // If error or offline, fallback to lookup or empty
      const match = consultations.find((c) => c.id === reqId);
      if (match) {
        setActiveRequestHistory([
          {
            id: 1,
            status: match.status,
            comments: match.statusNotes || "Current workflow record",
            changedAt: match.updatedAt || match.createdAt || new Date().toISOString(),
          },
        ]);
      }
    } finally {
      setHistoryLoading(false);
    }
  };

  // Open Status Transition Modal (Staff workflow testing)
  const handleOpenStatusModal = (req: ConsultationRequest) => {
    setSelectedReqForStatus(req);
    // Find numeric enum matching current status or default to next
    const currentEnumVal = (ConsultationStatus as any)[req.status] || ConsultationStatus.Processing;
    setNewStatusEnum(currentEnumVal);
    setStatusComments(`Workflow status transitioned to ${req.status}`);
    setTransitionError(null);
    setStatusModalOpen(true);
  };

  // Submit Status Transition (PATCH /api/Consultations/{id}/status)
  const handleExecuteStatusTransition = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedReqForStatus) return;

    setTransitioningStatus(true);
    setTransitionError(null);

    try {
      await consultationService.updateStatus(selectedReqForStatus.id, {
        status: Number(newStatusEnum),
        comments: statusComments.trim() || null,
      });

      setStatusModalOpen(false);
      onRefresh();
    } catch (err: any) {
      setTransitionError(err.message || "Failed to update workflow status.");
    } finally {
      setTransitioningStatus(false);
    }
  };

  // Filter consultations
  const filteredConsultations = consultations.filter((c) => {
    const matchesSearch =
      c.id.toLowerCase().includes(searchQuery.toLowerCase()) ||
      c.petId.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (c.petName && c.petName.toLowerCase().includes(searchQuery.toLowerCase())) ||
      c.ownerId.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (c.ownerName && c.ownerName.toLowerCase().includes(searchQuery.toLowerCase())) ||
      (c.preferredBranch && c.preferredBranch.toLowerCase().includes(searchQuery.toLowerCase())) ||
      c.symptomsDescription.toLowerCase().includes(searchQuery.toLowerCase());

    const matchesStatus =
      statusFilter === "All" ||
      c.status.toLowerCase() === statusFilter.toLowerCase();

    return matchesSearch && matchesStatus;
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "18px" }}>
      {/* Live Status Lookup Widget */}
      <Card>
        <div className="card-header">
          <div>
            <div className="eyebrow">Quick Status Tracking</div>
            <h3>Quick Status Tracking & Workflow Inspection</h3>
          </div>
          <Activity size={18} style={{ color: "var(--primary)" }} />
        </div>
        <p style={{ margin: "0 0 14px", fontSize: "12px", color: "var(--muted)" }}>
          Track the real-time lifecycle progress of any consultation request by its Short ID (e.g. <code>REQ-5001</code>).
        </p>

        <form onSubmit={handleLookupStatus} style={{ display: "flex", gap: "10px", flexWrap: "wrap" }}>
          <div className="search-input" style={{ flex: 1, minWidth: "260px" }}>
            <Search size={16} />
            <input
              placeholder="Paste Consultation Short ID (e.g. REQ-5001)..."
              value={lookupId}
              onChange={(e) => setLookupId(e.target.value.toUpperCase())}
            />
          </div>
          <Button
            type="submit"
            disabled={lookupLoading}
            icon={lookupLoading ? <Loader2 size={16} className="animate-spin" /> : <Activity size={16} />}
          >
            {lookupLoading ? "Checking..." : "Check Status"}
          </Button>
        </form>

        {/* Lookup Result Box with 7-Step Lifecycle Progress Tracker */}
        {lookupResult && (
          <div
            style={{
              marginTop: "16px",
              padding: "16px",
              borderRadius: "14px",
              background: "var(--surface-soft)",
              border: "1px solid var(--line)",
              display: "flex",
              flexDirection: "column",
              gap: "14px",
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "10px" }}>
              <div>
                <span style={{ fontSize: "10px", textTransform: "uppercase", color: "var(--muted)", fontWeight: 800 }}>
                  CONSULTATION REQUEST
                </span>
                <strong style={{ display: "block", fontSize: "15px", color: "var(--ink)", fontFamily: "monospace" }}>
                  {lookupResult.consultationId}
                </strong>
                <small style={{ color: "var(--muted)", fontSize: "10px" }}>
                  Pet: <code>{lookupResult.petId}</code> · Last update: {lookupResult.updatedAt ? new Date(lookupResult.updatedAt).toLocaleString() : "Recently"}
                </small>
              </div>

              <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                <Badge tone={getStatusTone(lookupResult.status)}>{lookupResult.status}</Badge>
                <Button
                  variant="secondary"
                  onClick={() => handleViewAuditHistory(lookupResult.consultationId)}
                  icon={<History size={13} />}
                  style={{ fontSize: "11px", padding: "4px 8px" }}
                >
                  Audit History
                </Button>
              </div>
            </div>

            {/* Visual Workflow Stepper Bar */}
            <div style={{ background: "#fff", padding: "12px", borderRadius: "10px", border: "1px solid var(--line)" }}>
              <span style={{ fontSize: "10px", fontWeight: 800, textTransform: "uppercase", color: "var(--muted)", display: "block", marginBottom: "8px" }}>
                Workflow Progress (7-Stage Status Enum)
              </span>
              <div style={{ display: "flex", justifyContent: "space-between", position: "relative", gap: "4px", overflowX: "auto" }}>
                {ALL_STATUS_ENUMS.map((st, idx) => {
                  const isCurrent = lookupResult.status.toLowerCase() === st.toLowerCase();
                  return (
                    <div
                      key={st}
                      style={{
                        display: "flex",
                        flexDirection: "column",
                        alignItems: "center",
                        flex: 1,
                        minWidth: "70px",
                      }}
                    >
                      <div
                        style={{
                          width: "22px",
                          height: "22px",
                          borderRadius: "50%",
                          display: "grid",
                          placeItems: "center",
                          fontSize: "10px",
                          fontWeight: 800,
                          background: isCurrent ? "var(--primary)" : "#eef2f0",
                          color: isCurrent ? "#fff" : "#7c8985",
                          border: isCurrent ? "2px solid var(--primary-deep)" : "none",
                        }}
                      >
                        {isCurrent ? <Check size={12} /> : idx + 1}
                      </div>
                      <span
                        style={{
                          fontSize: "8px",
                          textAlign: "center",
                          marginTop: "4px",
                          fontWeight: isCurrent ? 800 : 500,
                          color: isCurrent ? "var(--ink)" : "var(--muted)",
                          lineHeight: 1.1,
                        }}
                      >
                        {st}
                      </span>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        )}

        {/* Lookup Error Box */}
        {lookupError && (
          <div className="form-error" style={{ marginTop: "14px" }}>
            <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
              <AlertCircle size={16} />
              <span>{lookupError}</span>
            </div>
          </div>
        )}
      </Card>

      {/* Consultation Requests Queue */}
      <Card>
        <div className="card-header">
          <div>
            <div className="eyebrow">Consultation Requests</div>
            <h3>Consultation Requests ({consultations.length})</h3>
          </div>
          <div style={{ display: "flex", gap: "8px" }}>
            <Button
              variant="secondary"
              icon={<RefreshCw size={14} className={loading ? "animate-spin" : ""} />}
              onClick={onRefresh}
              disabled={loading}
            >
              Refresh
            </Button>
            <Button
              variant="primary"
              icon={<Sparkles size={14} />}
              onClick={onNewConsultation}
            >
              New Request
            </Button>
          </div>
        </div>

        {/* Filters Bar with all 7 Status Enums */}
        <div className="filter-bar">
          <div className="search-input">
            <Search size={16} />
            <input
              placeholder="Search by Request ID (REQ-xxxx), Pet, Branch, or Symptoms..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>
          <div className="select-input">
            <Filter size={15} />
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
            >
              <option value="All">All Statuses</option>
              {ALL_STATUS_ENUMS.map((st) => (
                <option key={st} value={st}>
                  {st}
                </option>
              ))}
            </select>
          </div>
          <div className="filter-summary">
            {filteredConsultations.length} request{filteredConsultations.length === 1 ? "" : "s"}
          </div>
        </div>

        {/* Table / Empty State */}
        {loading ? (
          <div className="empty-state" style={{ minHeight: "220px" }}>
            <Activity size={32} className="animate-pulse" style={{ color: "var(--primary)" }} />
            <p style={{ marginTop: "12px" }}>Fetching consultation requests from server...</p>
          </div>
        ) : filteredConsultations.length === 0 ? (
          <div className="empty-state" style={{ minHeight: "220px" }}>
            <Calendar size={36} style={{ color: "var(--muted)", opacity: 0.5 }} />
            <h4 style={{ margin: "10px 0 4px", fontSize: "14px" }}>
              {consultations.length === 0
                ? "No consultation requests found."
                : "No requests match your filter."}
            </h4>
            <p style={{ margin: 0, fontSize: "11px", color: "var(--muted)" }}>
              {consultations.length === 0
                ? "Submit a consultation request using the 5-step wizard to begin the clinic workflow."
                : "Try adjusting your search query or status filter."}
            </p>
            {consultations.length === 0 && (
              <Button
                onClick={onNewConsultation}
                style={{ marginTop: "14px" }}
                icon={<Sparkles size={14} />}
              >
                Submit Consultation Request
              </Button>
            )}
          </div>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Request ID</th>
                  <th>Patient & Owner</th>
                  <th>Branch & Location</th>
                  <th>Symptoms</th>
                  <th>Budget</th>
                  <th>Workflow Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredConsultations.map((req) => (
                  <tr key={req.id}>
                    {/* Request Short ID & Date */}
                    <td>
                      <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                        <code style={{ fontSize: "12px", fontWeight: 800, color: "var(--ink)" }}>
                          {req.id}
                        </code>
                        <button
                          type="button"
                          onClick={() => copyToClipboard(req.id, req.id)}
                          style={{
                            background: "none",
                            border: "none",
                            color: copiedId === req.id ? "var(--success)" : "var(--muted)",
                            cursor: "pointer",
                          }}
                          title="Copy Request Short ID"
                        >
                          {copiedId === req.id ? <Check size={12} /> : <Copy size={12} />}
                        </button>
                      </div>
                      <span className="muted" style={{ fontSize: "10px" }}>
                        {req.createdAt ? formatDate(req.createdAt.slice(0, 10)) : "Today"}
                      </span>
                    </td>

                    {/* Pet & Owner */}
                    <td>
                      <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                        <div style={{ display: "flex", alignItems: "center", gap: "4px", fontSize: "12px" }}>
                          <PawPrint size={12} style={{ color: "var(--primary)" }} />
                          <strong>{req.petName || req.petId}</strong>
                          <code style={{ fontSize: "10px", color: "var(--muted)" }}>({req.petId})</code>
                        </div>
                        <span className="muted" style={{ fontSize: "10px" }}>
                          Owner: <strong>{req.ownerName || req.ownerId}</strong> ({req.ownerId})
                        </span>
                      </div>
                    </td>

                    {/* Branch & GPS Coordinates */}
                    <td>
                      <div style={{ display: "flex", alignItems: "center", gap: "5px", fontSize: "11px" }}>
                        <MapPin size={13} style={{ color: "var(--muted)" }} />
                        <strong>{req.preferredBranch || "Central Clinic"}</strong>
                      </div>
                      {req.preferredClinicLocationLat && req.preferredClinicLocationLong ? (
                        <span style={{ fontSize: "9px", color: "var(--muted)", display: "flex", alignItems: "center", gap: "3px", marginTop: "2px" }}>
                          <Navigation size={9} /> GPS: {req.preferredClinicLocationLat.toFixed(2)}, {req.preferredClinicLocationLong.toFixed(2)}
                        </span>
                      ) : (
                        <span className="muted-line" style={{ fontSize: "10px" }}>
                          Preferred: {formatDate(req.preferredDate.slice(0, 10))}
                        </span>
                      )}
                    </td>

                    {/* Symptoms Snippet & Photo Indicator */}
                    <td style={{ maxWidth: "220px" }}>
                      <div
                        style={{
                          fontSize: "11px",
                          display: "-webkit-box",
                          WebkitLineClamp: 2,
                          WebkitBoxOrient: "vertical",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                          lineHeight: "1.35",
                        }}
                      >
                        {req.symptomsDescription}
                      </div>
                      {req.photoUrl && (
                        <span
                          style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "3px",
                            fontSize: "10px",
                            color: "var(--info)",
                            marginTop: "2px",
                          }}
                        >
                          <ImageIcon size={10} /> Photo attached
                        </span>
                      )}
                    </td>

                    {/* Budget Limit */}
                    <td>
                      <div style={{ display: "flex", alignItems: "center", gap: "4px" }}>
                        <Wallet size={12} style={{ color: "var(--muted)" }} />
                        <strong>{formatLkr(req.budgetLimit)}</strong>
                      </div>
                    </td>

                    {/* Workflow Status */}
                    <td>
                      <Badge tone={getStatusTone(req.status)}>{req.status}</Badge>
                      {req.statusNotes && (
                        <span
                          style={{
                            display: "block",
                            fontSize: "9px",
                            color: "var(--muted)",
                            maxWidth: "140px",
                            overflow: "hidden",
                            textOverflow: "ellipsis",
                            whiteSpace: "nowrap",
                            marginTop: "2px",
                          }}
                          title={req.statusNotes}
                        >
                          {req.statusNotes}
                        </span>
                      )}
                    </td>

                    {/* Actions */}
                    <td>
                      <div style={{ display: "flex", gap: "4px" }}>
                        <Button
                          variant="ghost"
                          onClick={() => {
                            setLookupId(req.id);
                            consultationService.getStatus(req.id).then(setLookupResult).catch(() => {});
                            window.scrollTo({ top: 0, behavior: "smooth" });
                          }}
                          style={{ fontSize: "10px", padding: "4px 8px" }}
                          icon={<Activity size={12} />}
                          title="Track in live status stepper"
                        >
                          Track
                        </Button>
                        <Button
                          variant="ghost"
                          onClick={() => handleViewAuditHistory(req.id)}
                          style={{ fontSize: "10px", padding: "4px 8px" }}
                          icon={<History size={12} />}
                          title="View audit history log"
                        >
                          Audit
                        </Button>
                        <Button
                          variant="secondary"
                          onClick={() => handleOpenStatusModal(req)}
                          style={{ fontSize: "10px", padding: "4px 8px" }}
                          icon={<Settings2 size={12} />}
                          title="Transition workflow status (Staff Action)"
                        >
                          Status
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* Audit Trail Timeline Modal */}
      {historyModalOpen && (
        <Modal
          title={`Audit Trail & History · ${historyRequestId}`}
          onClose={() => setHistoryModalOpen(false)}
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
            <div style={{ fontSize: "12px", color: "var(--muted)" }}>
              Chronological lifecycle transition events and audit trail for consultation request <code>{historyRequestId}</code>.
            </div>

            {historyLoading ? (
              <div className="empty-state" style={{ minHeight: "160px" }}>
                <Loader2 size={24} className="animate-spin" style={{ color: "var(--primary)" }} />
                <p style={{ marginTop: "10px", fontSize: "12px" }}>Loading audit records...</p>
              </div>
            ) : activeRequestHistory.length === 0 ? (
              <div className="empty-state" style={{ minHeight: "160px" }}>
                <History size={32} style={{ color: "var(--muted)", opacity: 0.5 }} />
                <p style={{ margin: "8px 0 0", fontSize: "12px" }}>No transition history recorded yet.</p>
              </div>
            ) : (
              <div className="timeline" style={{ maxHeight: "360px", overflowY: "auto" }}>
                {activeRequestHistory.map((item) => (
                  <div className="timeline-row" key={item.id || item.changedAt}>
                    <div className="timeline-node completed">
                      <Check size={11} />
                    </div>
                    <div className="timeline-content">
                      <div className="timeline-top">
                        <strong style={{ fontSize: "12px" }}>{item.status}</strong>
                        <Badge tone={getStatusTone(item.status)}>{item.status}</Badge>
                      </div>
                      <p style={{ fontSize: "11px", color: "var(--ink)", margin: "3px 0 2px" }}>
                        {item.comments || "Workflow state recorded"}
                      </p>
                      <small style={{ color: "var(--muted)", fontSize: "10px" }}>
                        {new Date(item.changedAt).toLocaleString()}
                      </small>
                    </div>
                  </div>
                ))}
              </div>
            )}

            <div style={{ display: "flex", justifyContent: "flex-end", borderTop: "1px solid var(--line)", paddingTop: "12px" }}>
              <Button variant="secondary" onClick={() => setHistoryModalOpen(false)}>
                Close
              </Button>
            </div>
          </div>
        </Modal>
      )}

      {/* Staff Status Transition Action Modal (PATCH /api/Consultations/{id}/status) */}
      {statusModalOpen && selectedReqForStatus && (
        <Modal
          title={`Update Workflow Status · ${selectedReqForStatus.id}`}
          onClose={() => setStatusModalOpen(false)}
        >
          <form onSubmit={handleExecuteStatusTransition}>
            {transitionError && (
              <div className="form-error" style={{ marginBottom: "14px" }}>
                <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                  <AlertCircle size={16} />
                  <span>{transitionError}</span>
                </div>
              </div>
            )}

            <div className="form-grid">
              <label style={{ gridColumn: "1 / -1" }}>
                Target Workflow Status *
                <select
                  value={newStatusEnum}
                  onChange={(e) => setNewStatusEnum(Number(e.target.value) as ConsultationStatus)}
                >
                  <option value={ConsultationStatus.Submitted}>1 - Submitted</option>
                  <option value={ConsultationStatus.Processing}>2 - Processing</option>
                  <option value={ConsultationStatus.PendingApproval}>3 - PendingApproval</option>
                  <option value={ConsultationStatus.Approved}>4 - Approved</option>
                  <option value={ConsultationStatus.Rejected}>5 - Rejected</option>
                  <option value={ConsultationStatus.RevisionRequired}>6 - RevisionRequired</option>
                  <option value={ConsultationStatus.AppointmentConfirmed}>7 - AppointmentConfirmed</option>
                </select>
              </label>

              <label style={{ gridColumn: "1 / -1" }}>
                Audit Transition Comments
                <textarea
                  rows={3}
                  placeholder="e.g. Clinical assessment conducted. Quotation drafted and forwarded for manager review."
                  value={statusComments}
                  onChange={(e) => setStatusComments(e.target.value)}
                />
              </label>
            </div>

            <div className="modal-actions">
              <Button variant="secondary" type="button" onClick={() => setStatusModalOpen(false)} disabled={transitioningStatus}>
                Cancel
              </Button>
              <Button type="submit" disabled={transitioningStatus} icon={transitioningStatus ? <Loader2 size={16} className="animate-spin" /> : <Settings2 size={16} />}>
                {transitioningStatus ? "Updating..." : "Confirm Status Transition"}
              </Button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
};
