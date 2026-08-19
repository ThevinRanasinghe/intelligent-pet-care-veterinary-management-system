import React, { useEffect, useState } from "react";
import { AlertCircle, CheckCircle2, Heart, Loader2, PawPrint, PlusCircle, User, Wand2 } from "lucide-react";
import { Modal } from "../../components/ui/Modal";
import { Button } from "../../components/ui/Button";
import {
  DEMO_OWNERS,
  generateGuid,
  isValidGuid,
  petService,
  type CreatePetDto,
  type Pet,
} from "../../services/api";

const SPECIES_OPTIONS = [
  "Dog",
  "Cat",
  "Bird",
  "Rabbit",
  "Guinea Pig",
  "Reptile",
  "Fish",
  "Other",
];

interface PetManagementModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: (createdPet: Pet) => void;
  initialOwnerId?: string;
}

export const PetManagementModal: React.FC<PetManagementModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
  initialOwnerId,
}) => {
  const [formData, setFormData] = useState<CreatePetDto>({
    ownerId: initialOwnerId || DEMO_OWNERS[0].id,
    name: "",
    species: "Dog",
    breed: "",
    age: 2,
    medicalHistorySummary: "",
  });

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      setError(null);
      setSuccessMsg(null);
      setFormData({
        ownerId: initialOwnerId || DEMO_OWNERS[0].id,
        name: "",
        species: "Dog",
        breed: "",
        age: 2,
        medicalHistorySummary: "",
      });
    }
  }, [isOpen, initialOwnerId]);

  if (!isOpen) return null;

  const handleGenerateOwnerGuid = () => {
    const guid = generateGuid();
    setFormData((prev) => ({ ...prev, ownerId: guid }));
  };

  const handleSelectDemoOwner = (ownerId: string) => {
    setFormData((prev) => ({ ...prev, ownerId }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccessMsg(null);

    // Validation
    if (!isValidGuid(formData.ownerId)) {
      setError("Please provide a valid Owner GUID (UUID format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).");
      return;
    }

    if (!formData.name.trim()) {
      setError("Please enter the pet's name.");
      return;
    }

    if (!formData.breed.trim()) {
      setError("Please enter the pet's breed.");
      return;
    }

    if (formData.age < 0) {
      setError("Pet age cannot be negative.");
      return;
    }

    setLoading(true);

    try {
      const created = await petService.createPet({
        ownerId: formData.ownerId.trim(),
        name: formData.name.trim(),
        species: formData.species.trim(),
        breed: formData.breed.trim(),
        age: Number(formData.age),
        medicalHistorySummary: formData.medicalHistorySummary?.trim() || null,
      });

      setSuccessMsg(`Pet "${created.name}" registered successfully!`);
      if (onSuccess) {
        onSuccess(created);
      }
      setTimeout(() => {
        onClose();
      }, 1000);
    } catch (err: any) {
      setError(err.message || "Failed to register pet.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal title="Register New Pet" onClose={onClose}>
      <form onSubmit={handleSubmit}>
        {/* Error Alert */}
        {error && (
          <div className="form-error" style={{ marginBottom: "14px" }}>
            <div style={{ display: "flex", gap: "8px", alignItems: "flex-start" }}>
              <AlertCircle size={16} style={{ flexShrink: 0, marginTop: "2px" }} />
              <div>
                <strong>Registration Error</strong>
                <p style={{ margin: "2px 0 0", fontSize: "11px" }}>{error}</p>
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
          {/* Owner ID (GUID) */}
          <div style={{ gridColumn: "1 / -1" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "4px" }}>
              <label htmlFor="pet-owner-id" style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                <User size={13} /> Owner GUID (UUID) *
              </label>
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
                Generate New GUID
              </button>
            </div>
            <input
              id="pet-owner-id"
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
                Invalid GUID format. Must be a valid UUID.
              </span>
            )}

            {/* Quick Demo Owner Presets */}
            <div style={{ display: "flex", gap: "6px", alignItems: "center", marginTop: "6px", flexWrap: "wrap" }}>
              <span style={{ fontSize: "10px", color: "var(--muted)" }}>Preset owners:</span>
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

          {/* Pet Name */}
          <label>
            Pet Name *
            <input
              type="text"
              required
              placeholder="e.g. Milo, Bella, Max"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            />
          </label>

          {/* Species */}
          <label>
            Species *
            <select
              value={formData.species}
              onChange={(e) => setFormData({ ...formData, species: e.target.value })}
            >
              {SPECIES_OPTIONS.map((species) => (
                <option key={species} value={species}>
                  {species}
                </option>
              ))}
            </select>
          </label>

          {/* Breed */}
          <label>
            Breed *
            <input
              type="text"
              required
              placeholder="e.g. Golden Retriever, Persian, Beagle"
              value={formData.breed}
              onChange={(e) => setFormData({ ...formData, breed: e.target.value })}
            />
          </label>

          {/* Age */}
          <label>
            Age (Years) *
            <input
              type="number"
              required
              min="0"
              max="50"
              value={formData.age}
              onChange={(e) => setFormData({ ...formData, age: Number(e.target.value) })}
            />
          </label>

          {/* Medical History Summary */}
          <div style={{ gridColumn: "1 / -1" }}>
            <label>
              Medical History / Allergies / Notes (Optional)
              <textarea
                rows={3}
                placeholder="e.g. Fully vaccinated (Rabies, DHPPi). Known allergy to penicillin. Prior surgery in 2024."
                value={formData.medicalHistorySummary || ""}
                onChange={(e) => setFormData({ ...formData, medicalHistorySummary: e.target.value })}
              />
            </label>
          </div>
        </div>

        {/* Modal Actions */}
        <div className="modal-actions">
          <Button variant="secondary" type="button" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button type="submit" disabled={loading} icon={loading ? <Loader2 size={16} className="animate-spin" /> : <PlusCircle size={16} />}>
            {loading ? "Registering..." : "Register Pet"}
          </Button>
        </div>
      </form>
    </Modal>
  );
};
