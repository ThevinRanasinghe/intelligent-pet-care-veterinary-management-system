import React, { useEffect, useState } from "react";
import {
  Activity,
  AlertCircle,
  Calendar,
  CheckCircle2,
  Clock,
  FileText,
  HeartPulse,
  Loader2,
  Plus,
  PlusCircle,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
  Stethoscope,
  Syringe,
  User,
} from "lucide-react";
import { Modal } from "../../components/ui/Modal";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import {
  petService,
  type Pet,
  type PetMedicalHistoryDto,
  type MedicalRecordDto,
  type VaccinationRecordDto,
  type CreateMedicalRecordDto,
  type CreateVaccinationRecordDto,
} from "../../services/api";
import { formatDate } from "../../utils/format";

interface PetHistoryModalProps {
  isOpen: boolean;
  onClose: () => void;
  pet: Pet | null;
  onRecordAdded?: () => void;
}

export const PetHistoryModal: React.FC<PetHistoryModalProps> = ({
  isOpen,
  onClose,
  pet,
  onRecordAdded,
}) => {
  const [activeTab, setActiveTab] = useState<"medical" | "vaccines" | "staff-add">("medical");
  const [history, setHistory] = useState<PetMedicalHistoryDto | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Staff add record form state
  const [recordType, setRecordType] = useState<"medical" | "vaccine">("medical");
  const [medDiagnosis, setMedDiagnosis] = useState("");
  const [medTreatment, setMedTreatment] = useState("");
  const [medVet, setMedVet] = useState("Dr. Emily Hayes, DVM");
  const [medNotes, setMedNotes] = useState("");

  const [vacName, setVacName] = useState("Rabies Booster");
  const [vacAdminDate, setVacAdminDate] = useState(new Date().toISOString().split("T")[0]);
  const [vacDueDate, setVacDueDate] = useState(
    new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString().split("T")[0]
  );
  const [vacVet, setVacVet] = useState("Dr. Emily Hayes, DVM");
  const [vacBatch, setVacBatch] = useState("VAC-2026-09A");

  const [savingRecord, setSavingRecord] = useState(false);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const fetchHistory = async () => {
    if (!pet) return;
    setLoading(true);
    setError(null);
    try {
      const data = await petService.getPetMedicalHistory(pet.id);
      setHistory(data);
    } catch (err: any) {
      setError(err.message || "Failed to load clinical history.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen && pet) {
      setError(null);
      setActionSuccess(null);
      setActiveTab("medical");
      fetchHistory();
    }
  }, [isOpen, pet]);

  if (!isOpen || !pet) return null;

  const handleAddMedicalRecord = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!medDiagnosis.trim() || !medTreatment.trim()) {
      setError("Diagnosis and Treatment are required.");
      return;
    }

    setSavingRecord(true);
    setError(null);
    setActionSuccess(null);

    try {
      const dto: CreateMedicalRecordDto = {
        diagnosis: medDiagnosis.trim(),
        treatment: medTreatment.trim(),
        veterinarianName: medVet.trim() || null,
        clinicalNotes: medNotes.trim() || null,
        recordDate: new Date().toISOString(),
      };

      await petService.addMedicalRecord(pet.id, dto);
      setActionSuccess("Medical record added successfully!");
      setMedDiagnosis("");
      setMedTreatment("");
      setMedNotes("");
      await fetchHistory();
      if (onRecordAdded) onRecordAdded();
      setTimeout(() => {
        setActiveTab("medical");
        setActionSuccess(null);
      }, 1000);
    } catch (err: any) {
      setError(err.message || "Failed to add medical record.");
    } finally {
      setSavingRecord(false);
    }
  };

  const handleAddVaccinationRecord = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!vacName.trim()) {
      setError("Vaccine name is required.");
      return;
    }

    setSavingRecord(true);
    setError(null);
    setActionSuccess(null);

    try {
      const dto: CreateVaccinationRecordDto = {
        vaccineName: vacName.trim(),
        dateAdministered: new Date(vacAdminDate).toISOString(),
        nextDueDate: vacDueDate ? new Date(vacDueDate).toISOString() : null,
        veterinarianName: vacVet.trim() || null,
        batchNumber: vacBatch.trim() || null,
      };

      await petService.addVaccinationRecord(pet.id, dto);
      setActionSuccess("Vaccination record added successfully!");
      await fetchHistory();
      if (onRecordAdded) onRecordAdded();
      setTimeout(() => {
        setActiveTab("vaccines");
        setActionSuccess(null);
      }, 1000);
    } catch (err: any) {
      setError(err.message || "Failed to add vaccination record.");
    } finally {
      setSavingRecord(false);
    }
  };

  const getVaccineStatusTone = (dueDateStr?: string | null): { label: string; tone: "success" | "warning" | "danger" } => {
    if (!dueDateStr) return { label: "Administered", tone: "success" };
    const due = new Date(dueDateStr);
    const now = new Date();
    const diffDays = Math.round((due.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));

    if (diffDays < 0) {
      return { label: `Overdue (${Math.abs(diffDays)}d)`, tone: "danger" };
    }
    if (diffDays <= 30) {
      return { label: `Due in ${diffDays}d`, tone: "warning" };
    }
    return { label: "Up to Date", tone: "success" };
  };

  return (
    <Modal
      title={`Medical & Vaccination Records · ${pet.name}`}
      onClose={onClose}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        {/* Pet Summary Banner */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            padding: "12px 14px",
            background: "#f7faf8",
            border: "1px solid var(--line)",
            borderRadius: "12px",
            flexWrap: "wrap",
            gap: "10px",
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
            {pet.photoUrl ? (
              <img
                src={pet.photoUrl}
                alt={pet.name}
                style={{ width: "42px", height: "42px", borderRadius: "10px", objectFit: "cover" }}
              />
            ) : (
              <div
                style={{
                  width: "42px",
                  height: "42px",
                  borderRadius: "10px",
                  background: "var(--primary-soft)",
                  color: "var(--primary)",
                  display: "grid",
                  placeItems: "center",
                }}
              >
                <Stethoscope size={20} />
              </div>
            )}
            <div>
              <strong style={{ fontSize: "14px", color: "var(--ink)" }}>{pet.name}</strong>
              <div style={{ fontSize: "11px", color: "var(--muted)", display: "flex", gap: "6px" }}>
                <span>{pet.species} ({pet.breed})</span>
                <span>·</span>
                <span>{pet.age} yr{pet.age === 1 ? "" : "s"} old</span>
                <span>·</span>
                <span>Owner: <code>{pet.ownerId}</code></span>
              </div>
            </div>
          </div>
          <Button
            variant="ghost"
            onClick={fetchHistory}
            disabled={loading}
            icon={<RefreshCw size={13} className={loading ? "animate-spin" : ""} />}
            title="Refresh history"
          >
            Refresh
          </Button>
        </div>

        {/* Tab Navigation */}
        <div
          style={{
            display: "flex",
            gap: "6px",
            borderBottom: "1px solid var(--line)",
            paddingBottom: "2px",
          }}
        >
          <button
            type="button"
            onClick={() => setActiveTab("medical")}
            style={{
              padding: "8px 14px",
              border: "none",
              borderBottom: activeTab === "medical" ? "3px solid var(--primary)" : "3px solid transparent",
              background: "none",
              fontSize: "12px",
              fontWeight: 700,
              color: activeTab === "medical" ? "var(--primary-deep)" : "var(--muted)",
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: "6px",
            }}
          >
            <Stethoscope size={15} />
            Clinical Diagnoses ({history?.medicalRecords.length ?? 0})
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("vaccines")}
            style={{
              padding: "8px 14px",
              border: "none",
              borderBottom: activeTab === "vaccines" ? "3px solid var(--primary)" : "3px solid transparent",
              background: "none",
              fontSize: "12px",
              fontWeight: 700,
              color: activeTab === "vaccines" ? "var(--primary-deep)" : "var(--muted)",
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: "6px",
            }}
          >
            <Syringe size={15} />
            Vaccination Log ({history?.vaccinationRecords.length ?? 0})
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("staff-add")}
            style={{
              padding: "8px 14px",
              border: "none",
              borderBottom: activeTab === "staff-add" ? "3px solid var(--primary)" : "3px solid transparent",
              background: "none",
              fontSize: "12px",
              fontWeight: 700,
              color: activeTab === "staff-add" ? "var(--primary-deep)" : "var(--muted)",
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: "6px",
              marginLeft: "auto",
            }}
          >
            <PlusCircle size={15} />
            Add Clinical Entry
          </button>
        </div>

        {/* Error / Success Banners */}
        {error && (
          <div className="form-error">
            <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
              <AlertCircle size={16} />
              <span>{error}</span>
            </div>
          </div>
        )}

        {actionSuccess && (
          <div
            style={{
              padding: "10px 12px",
              borderRadius: "10px",
              background: "var(--success-soft)",
              color: "#2d7e5b",
              border: "1px solid #d6ecdf",
              display: "flex",
              gap: "8px",
              alignItems: "center",
              fontSize: "11px",
            }}
          >
            <CheckCircle2 size={16} />
            <span>{actionSuccess}</span>
          </div>
        )}

        {/* Tab 1: Medical Records (UC-08) */}
        {activeTab === "medical" && (
          <div>
            {loading ? (
              <div className="empty-state" style={{ minHeight: "180px" }}>
                <Loader2 size={24} className="animate-spin" style={{ color: "var(--primary)" }} />
                <p style={{ marginTop: "10px", fontSize: "12px" }}>Loading medical records...</p>
              </div>
            ) : !history || history.medicalRecords.length === 0 ? (
              <div className="empty-state" style={{ minHeight: "180px" }}>
                <Stethoscope size={32} style={{ color: "var(--muted)", opacity: 0.5 }} />
                <h4 style={{ margin: "8px 0 3px", fontSize: "13px" }}>No medical diagnoses recorded</h4>
                <p style={{ margin: 0, fontSize: "11px", color: "var(--muted)" }}>
                  This pet has a clean bill of health with no active clinical diagnoses on record.
                </p>
                <Button
                  variant="secondary"
                  onClick={() => {
                    setRecordType("medical");
                    setActiveTab("staff-add");
                  }}
                  style={{ marginTop: "12px", fontSize: "11px" }}
                  icon={<Plus size={13} />}
                >
                  Add Medical Record
                </Button>
              </div>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: "10px", maxHeight: "380px", overflowY: "auto" }}>
                {history.medicalRecords.map((med) => (
                  <div
                    key={med.id}
                    style={{
                      border: "1px solid var(--line)",
                      borderRadius: "12px",
                      padding: "12px",
                      background: "#fff",
                      display: "flex",
                      flexDirection: "column",
                      gap: "6px",
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                      <div>
                        <span style={{ fontSize: "9px", fontWeight: 800, color: "var(--primary)", letterSpacing: "0.05em" }}>
                          {med.id} · {med.recordDate ? formatDate(med.recordDate.slice(0, 10)) : "Undated"}
                        </span>
                        <strong style={{ display: "block", fontSize: "13px", color: "var(--ink)", marginTop: "2px" }}>
                          {med.diagnosis}
                        </strong>
                      </div>
                      <Badge tone="info">
                        <Stethoscope size={11} style={{ marginRight: "4px" }} />
                        {med.veterinarianName || "Attending Vet"}
                      </Badge>
                    </div>
                    <div style={{ fontSize: "11px", color: "#45534f", marginTop: "2px" }}>
                      <strong>Treatment: </strong> {med.treatment}
                    </div>
                    {med.clinicalNotes && (
                      <div
                        style={{
                          fontSize: "10px",
                          color: "var(--muted)",
                          background: "#f9fbfa",
                          padding: "6px 8px",
                          borderRadius: "6px",
                          border: "1px solid #e8efec",
                          marginTop: "4px",
                        }}
                      >
                        <strong>Notes: </strong> {med.clinicalNotes}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Tab 2: Vaccination Log (UC-08) */}
        {activeTab === "vaccines" && (
          <div>
            {loading ? (
              <div className="empty-state" style={{ minHeight: "180px" }}>
                <Loader2 size={24} className="animate-spin" style={{ color: "var(--primary)" }} />
                <p style={{ marginTop: "10px", fontSize: "12px" }}>Loading vaccination log...</p>
              </div>
            ) : !history || history.vaccinationRecords.length === 0 ? (
              <div className="empty-state" style={{ minHeight: "180px" }}>
                <Syringe size={32} style={{ color: "var(--muted)", opacity: 0.5 }} />
                <h4 style={{ margin: "8px 0 3px", fontSize: "13px" }}>No vaccinations recorded</h4>
                <p style={{ margin: 0, fontSize: "11px", color: "var(--muted)" }}>
                  No vaccination administrations logged for this pet yet.
                </p>
                <Button
                  variant="secondary"
                  onClick={() => {
                    setRecordType("vaccine");
                    setActiveTab("staff-add");
                  }}
                  style={{ marginTop: "12px", fontSize: "11px" }}
                  icon={<Plus size={13} />}
                >
                  Log Vaccine Dose
                </Button>
              </div>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: "10px", maxHeight: "380px", overflowY: "auto" }}>
                {history.vaccinationRecords.map((vac) => {
                  const statusInfo = getVaccineStatusTone(vac.nextDueDate);
                  return (
                    <div
                      key={vac.id}
                      style={{
                        border: "1px solid var(--line)",
                        borderRadius: "12px",
                        padding: "12px",
                        background: "#fff",
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        gap: "10px",
                        flexWrap: "wrap",
                      }}
                    >
                      <div style={{ display: "flex", alignItems: "flex-start", gap: "10px" }}>
                        <div
                          style={{
                            width: "32px",
                            height: "32px",
                            borderRadius: "8px",
                            background: "var(--success-soft)",
                            color: "var(--success)",
                            display: "grid",
                            placeItems: "center",
                            flexShrink: 0,
                          }}
                        >
                          <Syringe size={16} />
                        </div>
                        <div>
                          <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                            <strong style={{ fontSize: "13px", color: "var(--ink)" }}>{vac.vaccineName}</strong>
                            <code style={{ fontSize: "10px", color: "var(--muted)" }}>{vac.id}</code>
                          </div>
                          <div style={{ fontSize: "10px", color: "var(--muted)", marginTop: "2px" }}>
                            Administered: {formatDate(vac.dateAdministered.slice(0, 10))} · Vet: {vac.veterinarianName || "Clinic Staff"}
                            {vac.batchNumber && <span> · Batch: {vac.batchNumber}</span>}
                          </div>
                        </div>
                      </div>
                      <div style={{ textAlign: "right" }}>
                        <Badge tone={statusInfo.tone}>{statusInfo.label}</Badge>
                        {vac.nextDueDate && (
                          <span style={{ display: "block", fontSize: "10px", color: "var(--muted)", marginTop: "3px" }}>
                            Next Due: {formatDate(vac.nextDueDate.slice(0, 10))}
                          </span>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}

        {/* Tab 3: Staff Record Entry (Authorized Veterinary Staff) */}
        {activeTab === "staff-add" && (
          <div style={{ background: "#fbfdfc", padding: "14px", borderRadius: "12px", border: "1px solid var(--line)" }}>
            <div style={{ display: "flex", gap: "8px", marginBottom: "12px" }}>
              <button
                type="button"
                onClick={() => setRecordType("medical")}
                style={{
                  padding: "6px 12px",
                  borderRadius: "8px",
                  fontSize: "11px",
                  border: "1px solid var(--line)",
                  background: recordType === "medical" ? "var(--primary-soft)" : "#fff",
                  color: recordType === "medical" ? "var(--primary-deep)" : "var(--ink)",
                  fontWeight: recordType === "medical" ? 700 : 500,
                  cursor: "pointer",
                }}
              >
                Clinical Medical Record
              </button>
              <button
                type="button"
                onClick={() => setRecordType("vaccine")}
                style={{
                  padding: "6px 12px",
                  borderRadius: "8px",
                  fontSize: "11px",
                  border: "1px solid var(--line)",
                  background: recordType === "vaccine" ? "var(--primary-soft)" : "#fff",
                  color: recordType === "vaccine" ? "var(--primary-deep)" : "var(--ink)",
                  fontWeight: recordType === "vaccine" ? 700 : 500,
                  cursor: "pointer",
                }}
              >
                Vaccination Record
              </button>
            </div>

            {recordType === "medical" ? (
              <form onSubmit={handleAddMedicalRecord} className="form-grid">
                <label style={{ gridColumn: "1 / -1" }}>
                  Diagnosis *
                  <input
                    type="text"
                    required
                    placeholder="e.g. Acute Gastroenteritis, Otitis Externa"
                    value={medDiagnosis}
                    onChange={(e) => setMedDiagnosis(e.target.value)}
                  />
                </label>
                <label style={{ gridColumn: "1 / -1" }}>
                  Treatment Protocol *
                  <textarea
                    rows={2}
                    required
                    placeholder="e.g. Administered sub-Q fluids. Prescribed oral probiotics and gastroprotectant for 5 days."
                    value={medTreatment}
                    onChange={(e) => setMedTreatment(e.target.value)}
                  />
                </label>
                <label>
                  Attending Veterinarian
                  <input
                    type="text"
                    value={medVet}
                    onChange={(e) => setMedVet(e.target.value)}
                  />
                </label>
                <label>
                  Clinical Observations (Optional)
                  <input
                    type="text"
                    placeholder="e.g. Temperature 38.6C, stable vitals"
                    value={medNotes}
                    onChange={(e) => setMedNotes(e.target.value)}
                  />
                </label>
                <div style={{ gridColumn: "1 / -1", display: "flex", justifyContent: "flex-end", marginTop: "10px" }}>
                  <Button type="submit" disabled={savingRecord} icon={<PlusCircle size={15} />}>
                    {savingRecord ? "Saving Record..." : "Append Medical Record"}
                  </Button>
                </div>
              </form>
            ) : (
              <form onSubmit={handleAddVaccinationRecord} className="form-grid">
                <label>
                  Vaccine Name *
                  <input
                    type="text"
                    required
                    placeholder="e.g. DHPP, Rabies, FVRCP"
                    value={vacName}
                    onChange={(e) => setVacName(e.target.value)}
                  />
                </label>
                <label>
                  Batch / Lot Number
                  <input
                    type="text"
                    placeholder="e.g. LOT-2026-X88"
                    value={vacBatch}
                    onChange={(e) => setVacBatch(e.target.value)}
                  />
                </label>
                <label>
                  Date Administered *
                  <input
                    type="date"
                    required
                    value={vacAdminDate}
                    onChange={(e) => setVacAdminDate(e.target.value)}
                  />
                </label>
                <label>
                  Next Due Date
                  <input
                    type="date"
                    value={vacDueDate}
                    onChange={(e) => setVacDueDate(e.target.value)}
                  />
                </label>
                <label style={{ gridColumn: "1 / -1" }}>
                  Attending Veterinarian
                  <input
                    type="text"
                    value={vacVet}
                    onChange={(e) => setVacVet(e.target.value)}
                  />
                </label>
                <div style={{ gridColumn: "1 / -1", display: "flex", justifyContent: "flex-end", marginTop: "10px" }}>
                  <Button type="submit" disabled={savingRecord} icon={<PlusCircle size={15} />}>
                    {savingRecord ? "Saving Vaccine..." : "Log Vaccine Dose"}
                  </Button>
                </div>
              </form>
            )}
          </div>
        )}

        {/* Footer */}
        <div style={{ display: "flex", justifyContent: "flex-end", borderTop: "1px solid var(--line)", paddingTop: "12px" }}>
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
        </div>
      </div>
    </Modal>
  );
};
