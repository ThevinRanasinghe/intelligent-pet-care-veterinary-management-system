import React, { useState } from "react";
import {
  Activity,
  AlertCircle,
  Calendar,
  Check,
  CheckCircle2,
  Clock3,
  Copy,
  ExternalLink,
  Filter,
  Image as ImageIcon,
  Loader2,
  MapPin,
  PawPrint,
  RefreshCw,
  Search,
  ShieldAlert,
  Sparkles,
  Wallet,
} from "lucide-react";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import {
  consultationService,
  isValidGuid,
  type ConsultationRequest,
  type ConsultationStatusResponse,
} from "../../services/api";
import { formatDate, formatLkr } from "../../utils/format";

interface ConsultationStatusTrackerProps {
  consultations: ConsultationRequest[];
  loading?: boolean;
  onRefresh: () => void;
  onNewConsultation: () => void;
}

export const ConsultationStatusTracker: React.FC<
  ConsultationStatusTrackerProps
> = ({ consultations, loading = false, onRefresh, onNewConsultation }) => {
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("All");
  const [copiedId, setCopiedId] = useState<string | null>(null);

  // Quick lookup state
  const [lookupId, setLookupId] = useState("");
  const [lookupLoading, setLookupLoading] = useState(false);
  const [lookupResult, setLookupResult] =
    useState<ConsultationStatusResponse | null>(null);
  const [lookupError, setLookupError] = useState<string | null>(null);

  const copyToClipboard = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 1800);
  };

  const handleLookupStatus = async (e: React.FormEvent) => {
    e.preventDefault();
    setLookupError(null);
    setLookupResult(null);

    const trimmed = lookupId.trim();
    if (!trimmed) {
      setLookupError("Please enter a Consultation GUID.");
      return;
    }

    if (!isValidGuid(trimmed)) {
      setLookupError(
        "Invalid GUID format (must be standard UUID 8-4-4-4-12 format)."
      );
      return;
    }

    setLookupLoading(true);
    try {
      const res = await consultationService.getStatus(trimmed);
      setLookupResult(res);
    } catch (err: any) {
      setLookupError(err.message || "Consultation request not found.");
    } finally {
      setLookupLoading(false);
    }
  };

  const getStatusTone = (
    status: string
  ): "warning" | "success" | "danger" | "info" | "neutral" => {
    switch (status?.toLowerCase()) {
      case "pending":
        return "warning";
      case "pendingmanagerapproval":
      case "planning":
        return "info";
      case "approved":
      case "confirmed":
      case "completed":
        return "success";
      case "rejected":
      case "cancelled":
        return "danger";
      default:
        return "neutral";
    }
  };

  const filteredConsultations = consultations.filter((c) => {
    const matchesSearch =
      c.id.toLowerCase().includes(searchQuery.toLowerCase()) ||
      c.petId.toLowerCase().includes(searchQuery.toLowerCase()) ||
      c.ownerId.toLowerCase().includes(searchQuery.toLowerCase()) ||
      c.preferredBranch.toLowerCase().includes(searchQuery.toLowerCase()) ||
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
            <div className="eyebrow">Endpoint 4 · GET /api/Consultations/&#123;id&#125;/status</div>
            <h3>Quick Consultation Status Lookup</h3>
          </div>
          <Activity size={18} style={{ color: "var(--primary)" }} />
        </div>
        <p style={{ margin: "0 0 14px", fontSize: "12px", color: "var(--muted)" }}>
          Check real-time status and lifecycle updates for any consultation request GUID directly against the .NET API.
        </p>

        <form onSubmit={handleLookupStatus} style={{ display: "flex", gap: "10px", flexWrap: "wrap" }}>
          <div className="search-input" style={{ flex: 1, minWidth: "260px" }}>
            <Search size={16} />
            <input
              placeholder="Paste Consultation GUID (e.g. 7c9e6679-7425-40de-944b-e07fc1f90ae7)..."
              value={lookupId}
              onChange={(e) => setLookupId(e.target.value)}
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

        {/* Lookup Result Box */}
        {lookupResult && (
          <div
            style={{
              marginTop: "14px",
              padding: "14px",
              borderRadius: "12px",
              background: "var(--surface-soft)",
              border: "1px solid var(--line)",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              flexWrap: "wrap",
              gap: "12px",
            }}
          >
            <div>
              <span style={{ fontSize: "10px", textTransform: "uppercase", color: "var(--muted)", fontWeight: 800 }}>
                Consultation ID
              </span>
              <strong style={{ display: "block", fontSize: "13px", color: "var(--ink)", fontFamily: "monospace" }}>
                {lookupResult.consultationId}
              </strong>
              {lookupResult.updatedAt && (
                <small style={{ color: "var(--muted)", fontSize: "10px" }}>
                  Last updated: {new Date(lookupResult.updatedAt).toLocaleString()}
                </small>
              )}
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
              <span style={{ fontSize: "12px", color: "var(--muted)" }}>Current Status:</span>
              <Badge tone={getStatusTone(lookupResult.status)}>{lookupResult.status}</Badge>
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

      {/* Consultation Requests History List */}
      <Card>
        <div className="card-header">
          <div>
            <div className="eyebrow">Request Queue</div>
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

        {/* Filters */}
        <div className="filter-bar">
          <div className="search-input">
            <Search size={16} />
            <input
              placeholder="Search by symptoms, branch, or GUID..."
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
              <option value="Pending">Pending</option>
              <option value="PendingManagerApproval">Pending Approval</option>
              <option value="Approved">Approved</option>
              <option value="Rejected">Rejected</option>
            </select>
          </div>
          <div className="filter-summary">
            {filteredConsultations.length} request{filteredConsultations.length === 1 ? "" : "s"}
          </div>
        </div>

        {/* Table or Empty State */}
        {loading ? (
          <div className="empty-state" style={{ minHeight: "220px" }}>
            <Activity size={32} className="animate-pulse" style={{ color: "var(--primary)" }} />
            <p style={{ marginTop: "12px" }}>Fetching consultation requests...</p>
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
                ? "Submit a consultation request for a registered pet to get started."
                : "Try adjusting your search query or filter selection."}
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
                  <th>Pet & Owner</th>
                  <th>Branch & Date</th>
                  <th>Symptoms</th>
                  <th>Budget Limit</th>
                  <th>Status</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {filteredConsultations.map((req) => (
                  <tr key={req.id}>
                    <td>
                      <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                        <code style={{ fontSize: "11px", fontWeight: 700, color: "var(--ink)" }}>
                          {req.id.slice(0, 8)}...
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
                          title="Copy Request GUID"
                        >
                          {copiedId === req.id ? <Check size={13} /> : <Copy size={13} />}
                        </button>
                      </div>
                      <span className="muted" style={{ fontSize: "10px" }}>
                        {req.createdAt ? formatDate(req.createdAt.slice(0, 10)) : "Today"}
                      </span>
                    </td>
                    <td>
                      <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                        <div style={{ display: "flex", alignItems: "center", gap: "4px", fontSize: "11px" }}>
                          <PawPrint size={12} style={{ color: "var(--primary)" }} />
                          <span>Pet: <code>{req.petId.slice(0, 8)}...</code></span>
                        </div>
                        <span className="muted" style={{ fontSize: "10px" }}>
                          Owner: <code>{req.ownerId.slice(0, 8)}...</code>
                        </span>
                      </div>
                    </td>
                    <td>
                      <div style={{ display: "flex", alignItems: "center", gap: "5px", fontSize: "11px" }}>
                        <MapPin size={13} style={{ color: "var(--muted)" }} />
                        <strong>{req.preferredBranch}</strong>
                      </div>
                      <span className="muted-line" style={{ fontSize: "10px", marginTop: "2px" }}>
                        {formatDate(req.preferredDate.slice(0, 10))}
                      </span>
                    </td>
                    <td style={{ maxWidth: "240px" }}>
                      <div
                        style={{
                          fontSize: "11px",
                          display: "-webkit-box",
                          WebkitLineClamp: 2,
                          WebkitBoxOrient: "vertical",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
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
                    <td>
                      <div style={{ display: "flex", alignItems: "center", gap: "4px" }}>
                        <Wallet size={12} style={{ color: "var(--muted)" }} />
                        <strong>{formatLkr(req.budgetLimit)}</strong>
                      </div>
                    </td>
                    <td>
                      <Badge tone={getStatusTone(req.status)}>{req.status}</Badge>
                    </td>
                    <td>
                      <Button
                        variant="ghost"
                        onClick={() => {
                          setLookupId(req.id);
                          consultationService.getStatus(req.id).then(setLookupResult).catch(() => {});
                          window.scrollTo({ top: 0, behavior: "smooth" });
                        }}
                        style={{ fontSize: "10px", padding: "4px 8px" }}
                        icon={<Activity size={12} />}
                      >
                        Track
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
};
