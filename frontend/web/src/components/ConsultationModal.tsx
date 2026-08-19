import React, { useEffect, useState } from "react";
import {
  AlertCircle,
  Calendar,
  CheckCircle2,
  Image as ImageIcon,
  Loader2,
  PawPrint,
  Sparkles,
  User,
  Wand2,
} from "lucide-react";
import { Modal } from "./ui/Modal";
import { Button } from "./ui/Button";
import { Badge } from "./ui/Badge";
import {
  consultationService,
  DEMO_BRANCHES,
  DEMO_OWNERS,
  generateGuid,
  isValidGuid,
  type ConsultationRequest,
  type Pet,
} from "../services/api";

interface ConsultationModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: (request: ConsultationRequest) => void;
  initialOwnerId?: string;
  initialPetId?: string;
  availablePets?: Pet[];
}

export const ConsultationModal: React.FC<ConsultationModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
  initialOwnerId,
  initialPetId,
  availablePets = [],
}) => {
  const tomorrowStr = new Date(Date.now() + 86400000).toISOString().split("T")[0];

  const [formData, setFormData] = useState({
    petId: initialPetId || "",
    ownerId: initialOwnerId || DEMO_OWNERS[0].id,
    symptomsDescription: "",
    photoUrl: "",
    preferredBranch: DEMO_BRANCHES[0],
    preferredDate: tomorrowStr,
    budgetLimit: 5000,
  });

  const [manualPetInput, setManualPetInput] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Sync initial props when modal opens
  useEffect(() => {
    if (isOpen) {
      setError(null);
      setSuccessMsg(null);
      const ownerId = initialOwnerId || formData.ownerId || DEMO_OWNERS[0].id;
      let petId = initialPetId || "";
      if (!petId && availablePets.length > 0) {
        petId = availablePets[0].id;
      }
      setFormData((prev) => ({
        ...prev,
        ownerId,
        petId,
        preferredDate: prev.preferredDate || tomorrowStr,
      }));
      setManualPetInput(availablePets.length === 0 && !initialPetId);
    }
  }, [isOpen, initialOwnerId, initialPetId, availablePets]);

  if (!isOpen) return null;

  const ownerPets = availablePets.filter((p) => p.ownerId.toLowerCase() === formData.ownerId.toLowerCase());

  const handleSelectDemoOwner = (ownerId: string) => {
    setFormData((prev) => {
      const matchingPets = availablePets.filter((p) => p.ownerId.toLowerCase() === ownerId.toLowerCase());
      return {
        ...prev,
        ownerId,
        petId: matchingPets.length > 0 ? matchingPets[0].id : prev.petId,
      };
    });
  };

  const handleGenerateOwnerGuid = () => {
    const newGuid = generateGuid();
    setFormData((prev) => ({ ...prev, ownerId: newGuid }));
  };

  const handleGeneratePetGuid = () => {
    const newGuid = generateGuid();
    setFormData((prev) => ({ ...prev, petId: newGuid }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccessMsg(null);

    // Client-side GUID Validations
    if (!isValidGuid(formData.ownerId)) {
      setError("Please provide a valid Owner GUID (UUID format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).");
      return;
    }

    if (!isValidGuid(formData.petId)) {
      setError("Please provide a valid Pet GUID (UUID format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).");
      return;
    }

    if (!formData.symptomsDescription.trim()) {
      setError("Please provide a description of the pet's symptoms.");
      return;
    }

    if (formData.budgetLimit <= 0) {
      setError("Budget limit must be greater than 0 LKR.");
      return;
    }

    setLoading(true);

    try {
      // Ensure ISO 8601 DateTime for ASP.NET backend
      const isoDate = new Date(formData.preferredDate).toISOString();
      const response = await consultationService.createConsultation({
        ownerId: formData.ownerId.trim(),
        petId: formData.petId.trim(),
        symptomsDescription: formData.symptomsDescription.trim(),
        photoUrl: formData.photoUrl.trim() ? formData.photoUrl.trim() : null,
        preferredBranch: formData.preferredBranch,
        preferredDate: isoDate,
        budgetLimit: Number(formData.budgetLimit),
      });

      setSuccessMsg("Consultation Request created successfully! Status is Pending.");
      if (onSuccess) {
        onSuccess(response);
      }
      setTimeout(() => {
        onClose();
      }, 1200);
    } catch (err: any) {
      const msg = err.message || "Failed to submit request.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const isOwnershipError = error && error.toLowerCase().includes("pet ownership validation failed");

  return (
    <Modal title="New Consultation Request" onClose={onClose}>
      <form onSubmit={handleSubmit}>
        {/* Error Alert */}
        {error && (
          <div className="form-error" style={{ marginBottom: "14px" }}>
            <div style={{ display: "flex", gap: "8px", alignItems: "flex-start" }}>
              <AlertCircle size={16} style={{ flexShrink: 0, marginTop: "2px" }} />
              <div>
                <strong>{isOwnershipError ? "Pet Ownership Mismatch" : "Submission Error"}</strong>
                <p style={{ margin: "2px 0 0", fontSize: "11px" }}>{error}</p>
                {isOwnershipError && (
                  <p style={{ margin: "4px 0 0", fontSize: "10px", opacity: 0.9 }}>
                    Tip: The backend enforces that the Pet must already be registered and owned by this Owner ID. Register this pet first or select a valid pet from this owner.
                  </p>
                )}
              </div>
            </div>
          </div>
        )}

        {/* Success Alert */}
        {successMsg && (
          <div
            style={{
              marginBottom: "14px",
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
            <span>{successMsg}</span>
          </div>
        )}

        <div className="form-grid">
          {/* Owner ID Input */}
          <div style={{ gridColumn: "1 / -1" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "4px" }}>
              <label htmlFor="owner-id" style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                <User size={13} /> Owner GUID (UUID) *
              </label>
              <div style={{ display: "flex", gap: "6px" }}>
                <button
                  type="button"
                  onClick={handleGenerateOwnerGuid}
                  style={{
                    fontSize: "10px",
                    background: "none",
                    border: "none",
                    color: "var(--primary)",
                    cursor: "pointer",
                    textDecoration: "underline",
                  }}
                >
                  Generate GUID
                </button>
              </div>
            </div>
            <input
              id="owner-id"
              type="text"
              required
              placeholder="e.g. e2b8d000-0000-0000-0000-000000000001"
              value={formData.ownerId}
              onChange={(e) => setFormData({ ...formData, ownerId: e.target.value })}
              style={{
                width: "100%",
                borderColor: formData.ownerId && !isValidGuid(formData.ownerId) ? "var(--danger)" : undefined,
              }}
            />
            {formData.ownerId && !isValidGuid(formData.ownerId) && (
              <span style={{ color: "var(--danger)", fontSize: "10px", display: "block", marginTop: "2px" }}>
                Invalid GUID format. Must be 8-4-4-4-12 hex characters.
              </span>
            )}

            {/* Quick Demo Owner Presets */}
            <div style={{ display: "flex", gap: "6px", alignItems: "center", marginTop: "6px", flexWrap: "wrap" }}>
              <span style={{ fontSize: "10px", color: "var(--muted)" }}>Quick select:</span>
              {DEMO_OWNERS.map((owner) => (
                <button
                  key={owner.id}
                  type="button"
                  onClick={() => handleSelectDemoOwner(owner.id)}
                  style={{
                    fontSize: "10px",
                    padding: "2px 8px",
                    borderRadius: "6px",
                    border: "1px solid var(--line)",
                    background: formData.ownerId.toLowerCase() === owner.id.toLowerCase() ? "var(--primary-soft)" : "#fff",
                    color: formData.ownerId.toLowerCase() === owner.id.toLowerCase() ? "var(--primary-deep)" : "var(--ink)",
                    cursor: "pointer",
                  }}
                >
                  {owner.name}
                </button>
              ))}
            </div>
          </div>

          {/* Pet ID Input */}
          <div style={{ gridColumn: "1 / -1" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "4px" }}>
              <label htmlFor="pet-id" style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                <PawPrint size={13} /> Pet Selection / GUID *
              </label>
              {ownerPets.length > 0 && (
                <button
                  type="button"
                  onClick={() => setManualPetInput(!manualPetInput)}
                  style={{
                    fontSize: "10px",
                    background: "none",
                    border: "none",
                    color: "var(--primary)",
                    cursor: "pointer",
                    textDecoration: "underline",
                  }}
                >
                  {manualPetInput ? "Select from owner pets" : "Enter manual GUID"}
                </button>
              )}
            </div>

            {!manualPetInput && ownerPets.length > 0 ? (
              <select
                id="pet-id"
                value={formData.petId}
                onChange={(e) => setFormData({ ...formData, petId: e.target.value })}
                style={{ width: "100%" }}
              >
                {ownerPets.map((pet) => (
                  <option key={pet.id} value={pet.id}>
                    {pet.name} ({pet.species} - {pet.breed}, {pet.age} yrs) · {pet.id.slice(0, 8)}...
                  </option>
                ))}
              </select>
            ) : (
              <div>
                <input
                  id="pet-id"
                  type="text"
                  required
                  placeholder="e.g. 7c9e6679-7425-40de-944b-e07fc1f90ae7"
                  value={formData.petId}
                  onChange={(e) => setFormData({ ...formData, petId: e.target.value })}
                  style={{
                    width: "100%",
                    borderColor: formData.petId && !isValidGuid(formData.petId) ? "var(--danger)" : undefined,
                  }}
                />
                <div style={{ display: "flex", justifyContent: "space-between", marginTop: "2px" }}>
                  {formData.petId && !isValidGuid(formData.petId) ? (
                    <span style={{ color: "var(--danger)", fontSize: "10px" }}>
                      Invalid GUID format.
                    </span>
                  ) : <span />}
                  <button
                    type="button"
                    onClick={handleGeneratePetGuid}
                    style={{
                      fontSize: "10px",
                      background: "none",
                      border: "none",
                      color: "var(--primary)",
                      cursor: "pointer",
                      textDecoration: "underline",
                    }}
                  >
                    Generate Pet GUID
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* Preferred Branch */}
          <label>
            Preferred Branch *
            <select
              value={formData.preferredBranch}
              onChange={(e) => setFormData({ ...formData, preferredBranch: e.target.value })}
            >
              {DEMO_BRANCHES.map((branch) => (
                <option key={branch} value={branch}>
                  {branch}
                </option>
              ))}
            </select>
          </label>

          {/* Preferred Date */}
          <label>
            Preferred Date *
            <input
              type="date"
              required
              min={new Date().toISOString().split("T")[0]}
              value={formData.preferredDate}
              onChange={(e) => setFormData({ ...formData, preferredDate: e.target.value })}
            />
          </label>

          {/* Budget Limit */}
          <label>
            Budget Limit (LKR) *
            <input
              type="number"
              required
              min="500"
              step="500"
              value={formData.budgetLimit}
              onChange={(e) => setFormData({ ...formData, budgetLimit: Number(e.target.value) })}
            />
          </label>

          {/* Photo URL */}
          <label>
            Photo URL (Optional)
            <input
              type="url"
              placeholder="https://images.unsplash.com/..."
              value={formData.photoUrl}
              onChange={(e) => setFormData({ ...formData, photoUrl: e.target.value })}
            />
          </label>

          {/* Symptoms Description */}
          <div style={{ gridColumn: "1 / -1" }}>
            <label>
              Symptoms Description *
              <textarea
                required
                rows={3}
                placeholder="Describe the pet's condition, onset of symptoms, behavior changes, dietary intake, etc."
                value={formData.symptomsDescription}
                onChange={(e) => setFormData({ ...formData, symptomsDescription: e.target.value })}
              />
            </label>
          </div>
        </div>

        {/* Optional Photo Thumbnail Preview */}
        {formData.photoUrl && (
          <div
            style={{
              marginTop: "10px",
              padding: "8px",
              borderRadius: "8px",
              background: "#f9fbfa",
              border: "1px solid var(--line)",
              display: "flex",
              alignItems: "center",
              gap: "10px",
            }}
          >
            <img
              src={formData.photoUrl}
              alt="Pet Preview"
              onError={(e) => ((e.target as HTMLElement).style.display = "none")}
              style={{
                width: "48px",
                height: "48px",
                borderRadius: "6px",
                objectFit: "cover",
              }}
            />
            <div style={{ fontSize: "11px", color: "var(--muted)" }}>
              <span>Photo Attached</span>
              <small style={{ display: "block", wordBreak: "break-all" }}>{formData.photoUrl}</small>
            </div>
          </div>
        )}

        {/* Modal Actions */}
        <div className="modal-actions">
          <Button variant="secondary" type="button" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button type="submit" disabled={loading} icon={loading ? <Loader2 size={16} className="animate-spin" /> : <Sparkles size={16} />}>
            {loading ? "Submitting..." : "Submit Consultation"}
          </Button>
        </div>
      </form>
    </Modal>
  );
};

