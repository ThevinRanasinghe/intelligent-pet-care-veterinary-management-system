import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import {
  CalendarClock,
  ClipboardCheck,
  FileText,
  PawPrint,
  TrendingUp,
  UsersRound,
  Plus,
  Sparkles,
  ArrowRight,
  Activity,
  Heart,
} from "lucide-react";
import { Card } from "../components/ui/Card";
import { Badge } from "../components/ui/Badge";
import { Button } from "../components/ui/Button";
import {
  approvalProposals,
  appointmentSlots,
  quotations,
} from "../services/mockData";
import { formatLkr } from "../utils/format";
import { ConsultationModal } from "../components/ConsultationModal";
import { PetManagementModal } from "../features/consultations/PetManagementModal";
import { petService, consultationService, type Pet, type ConsultationRequest } from "../services/api";

export function DashboardPage() {
  const [isConsultationModalOpen, setIsConsultationModalOpen] = useState(false);
  const [isPetModalOpen, setIsPetModalOpen] = useState(false);
  const [recentPets, setRecentPets] = useState<Pet[]>([]);
  const [recentConsultations, setRecentConsultations] = useState<ConsultationRequest[]>([]);

  useEffect(() => {
    petService.getAllPets().then(setRecentPets).catch(() => {});
    consultationService.getAllConsultations().then(setRecentConsultations).catch(() => {});
  }, []);

  const confirmed = appointmentSlots.filter(
    (item) => item.status === "Confirmed",
  ).length;
  const pending = approvalProposals.filter(
    (item) => item.status === "Pending",
  ).length;
  const approvedQuotes = quotations.filter((item) =>
    ["Approved", "Finalised"].includes(item.status),
  );
  const expectedRevenue = approvedQuotes.reduce(
    (sum, q) =>
      sum +
      q.items.reduce(
        (inner, line) => inner + line.quantity * line.unitPrice,
        0,
      ),
    0,
  );

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <div className="eyebrow">Clinic overview</div>
          <h2>Good evening, Clinic Manager</h2>
          <p>
            Monitor today's operations, register pet patients, and move approval-ready cases forward.
          </p>
        </div>
        <div className="heading-actions" style={{ display: "flex", alignItems: "center", gap: "10px", flexWrap: "wrap" }}>
          <Button
            variant="secondary"
            icon={<PawPrint size={15} />}
            onClick={() => setIsPetModalOpen(true)}
          >
            Register Pet
          </Button>
          <Button
            variant="primary"
            icon={<Sparkles size={15} />}
            onClick={() => setIsConsultationModalOpen(true)}
          >
            New Consultation
          </Button>
          <Badge tone="success">System healthy</Badge>
          <span className="heading-date">19 Aug 2026</span>
        </div>
      </div>

      <div className="stat-grid">
        {[
          {
            label: "Appointments today",
            value: confirmed.toString(),
            trend: "+2",
            icon: CalendarClock,
          },
          {
            label: "Pending approvals",
            value: pending.toString(),
            trend: "Needs review",
            icon: ClipboardCheck,
          },
          {
            label: "Approved quotes",
            value: approvedQuotes.length.toString(),
            trend: formatLkr(expectedRevenue),
            icon: FileText,
          },
          {
            label: "Active vets",
            value: "4",
            trend: "All branches",
            icon: UsersRound,
          },
        ].map(({ label, value, trend, icon: Icon }) => (
          <Card key={label} className="stat-card">
            <div className="stat-top">
              <div className="stat-icon">
                <Icon size={18} />
              </div>
              <span>{trend}</span>
            </div>
            <strong>{value}</strong>
            <p>{label}</p>
          </Card>
        ))}
      </div>

      {/* Component 1: Pet & Consultation Quick Intake Card */}
      <Card style={{ marginBottom: "18px" }}>
        <div className="card-header">
          <div>
            <div className="eyebrow">Component 1 · Intake & Patient Flow</div>
            <h3>Pet & Consultation Intake Management</h3>
          </div>
          <Link to="/consultations" style={{ display: "flex", alignItems: "center", gap: "4px", color: "var(--primary)", fontWeight: 700, fontSize: "12px" }}>
            Open Intake Center <ArrowRight size={14} />
          </Link>
        </div>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
            gap: "14px",
            marginTop: "10px",
          }}
        >
          {/* Quick Pet Registration Action Card */}
          <div
            style={{
              padding: "16px",
              borderRadius: "14px",
              background: "#fbfdfc",
              border: "1px solid var(--line)",
              display: "flex",
              flexDirection: "column",
              justifyContent: "space-between",
              gap: "12px",
            }}
          >
            <div>
              <div style={{ display: "flex", alignItems: "center", gap: "8px", marginBottom: "8px" }}>
                <div
                  style={{
                    width: "32px",
                    height: "32px",
                    borderRadius: "8px",
                    background: "var(--primary-soft)",
                    color: "var(--primary)",
                    display: "grid",
                    placeItems: "center",
                  }}
                >
                  <PawPrint size={17} />
                </div>
                <strong style={{ fontSize: "13px" }}>Patient Registration</strong>
              </div>
              <p style={{ margin: 0, fontSize: "11px", color: "var(--muted)", lineHeight: 1.45 }}>
                Register pets with species, breed, age/DOB, and Owner Short ID verification (e.g. OWN-2001).
              </p>
            </div>
            <Button
              variant="secondary"
              onClick={() => setIsPetModalOpen(true)}
              icon={<Plus size={14} />}
              style={{ width: "100%", justifyContent: "center", fontSize: "11px" }}
            >
              Register New Pet
            </Button>
          </div>

          {/* Quick Consultation Request Action Card */}
          <div
            style={{
              padding: "16px",
              borderRadius: "14px",
              background: "#fbfdfc",
              border: "1px solid var(--line)",
              display: "flex",
              flexDirection: "column",
              justifyContent: "space-between",
              gap: "12px",
            }}
          >
            <div>
              <div style={{ display: "flex", alignItems: "center", gap: "8px", marginBottom: "8px" }}>
                <div
                  style={{
                    width: "32px",
                    height: "32px",
                    borderRadius: "8px",
                    background: "var(--info-soft)",
                    color: "var(--info)",
                    display: "grid",
                    placeItems: "center",
                  }}
                >
                  <Activity size={17} />
                </div>
                <strong style={{ fontSize: "13px" }}>Consultation Request</strong>
              </div>
              <p style={{ margin: 0, fontSize: "11px", color: "var(--muted)", lineHeight: 1.45 }}>
                Submit consultation requests with ownership validation, symptoms, branch, and budget limit.
              </p>
            </div>
            <Button
              variant="primary"
              onClick={() => setIsConsultationModalOpen(true)}
              icon={<Sparkles size={14} />}
              style={{ width: "100%", justifyContent: "center", fontSize: "11px" }}
            >
              Submit Consultation
            </Button>
          </div>
        </div>
      </Card>

      <div className="content-grid dashboard-grid">
        <Card>
          <div className="card-header">
            <div>
              <div className="eyebrow">Approval queue</div>
              <h3>Requests needing attention</h3>
            </div>
            <Link to="/approvals" style={{ fontSize: "12px", color: "var(--primary)", fontWeight: 700 }}>
              View all
            </Link>
          </div>
          <div className="queue-list">
            {approvalProposals.map((proposal) => (
              <div className="queue-item" key={proposal.id}>
                <div>
                  <div className="queue-title">
                    <span>{proposal.petName}</span>
                    <Badge
                      tone={
                        proposal.urgency === "Urgent" ? "danger" : "warning"
                      }
                    >
                      {proposal.urgency}
                    </Badge>
                  </div>
                  <p>
                    {proposal.ownerName} · {proposal.proposedVet}
                  </p>
                </div>
                <strong>{formatLkr(proposal.quotationTotal)}</strong>
              </div>
            ))}
          </div>
        </Card>

        <Card>
          <div className="card-header">
            <div>
              <div className="eyebrow">Today</div>
              <h3>Clinic pulse</h3>
            </div>
            <TrendingUp size={18} />
          </div>
          <div className="pulse-row">
            <div className="pulse-number">84%</div>
            <div>
              <strong>On-time capacity</strong>
              <p>Available slots are healthy across active branches.</p>
            </div>
          </div>
          <div className="mini-bar">
            <span style={{ width: "84%" }} />
          </div>
          <div className="pulse-note">
            <PawPrint size={16} />
            <span>
              Appointment conflicts are checked before a slot is saved.
            </span>
          </div>
        </Card>
      </div>

      {/* Modals */}
      <PetManagementModal
        isOpen={isPetModalOpen}
        onClose={() => setIsPetModalOpen(false)}
        onSuccess={(pet) => setRecentPets((prev) => [pet, ...prev])}
      />

      <ConsultationModal
        isOpen={isConsultationModalOpen}
        onClose={() => setIsConsultationModalOpen(false)}
        onSuccess={(c) => setRecentConsultations((prev) => [c, ...prev])}
        availablePets={recentPets}
      />
    </div>
  );
}

