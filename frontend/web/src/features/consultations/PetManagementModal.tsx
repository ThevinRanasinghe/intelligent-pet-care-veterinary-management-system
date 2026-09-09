import React, { useEffect, useState } from "react";
import {
  AlertCircle,
  Calendar,
  CheckCircle2,
  Edit3,
  Image as ImageIcon,
  Info,
  Loader2,
  Lock,
  PawPrint,
  PlusCircle,
  User,
  Wand2,
} from "lucide-react";
import { Modal } from "../../components/ui/Modal";
import { Button } from "../../components/ui/Button";
import {
  DEMO_OWNERS,
  generateShortId,
  isValidOwnerId,
  petService,
  type CreatePetDto,
  type UpdatePetProfileDto,
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

const PRESET_PHOTOS: Record<string, string> = {
  Dog: "https://images.unsplash.com/photo-1552053831-71594a27632d?w=300&q=80",
  Cat: "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?w=300&q=80",
  Bird: "https://images.unsplash.com/photo-1552728089-57bdde30beb3?w=300&q=80",
  Rabbit: "https://images.unsplash.com/photo-1585110396000-c9ffd4e4b308?w=300&q=80",
};

interface PetManagementModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: (pet: Pet) => void;
  initialOwnerId?: string;
  petToEdit?: Pet | null;
}

export const PetManagementModal: React.FC<PetManagementModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
  initialOwnerId,
  petToEdit,
}) => {
  const isEditMode = Boolean(petToEdit);

    const [formData, setFormData] = useState({
      ownerId: initialOwnerId || DEMO_OWNERS[0].id,
      ownerFullName: "",
      ownerEmail: "",
      name: "",
      species: "Dog",
      breed: "",
      dateOfBirth: "",
      age: 2,
      notes: "",
      photoUrl: "",
    });

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      setError(null);
      setSuccessMsg(null);
      if (petToEdit) {
        setFormData({
          ownerId: petToEdit.ownerId,
          ownerFullName: "",
          ownerEmail: "",
          name: petToEdit.name,
          species: petToEdit.species,
          breed: petToEdit.breed,
          dateOfBirth: petToEdit.dateOfBirth
            ? petToEdit.dateOfBirth.slice(0, 10)
            : "",
          age: petToEdit.age,
          notes: petToEdit.notes || "",
          photoUrl: petToEdit.photoUrl || "",
        });
      } else {
        setFormData({
          ownerId: initialOwnerId || DEMO_OWNERS[0].id,
          ownerFullName: "",
          ownerEmail: "",
          name: "",
          species: "Dog",
          breed: "",
          dateOfBirth: "",
          age: 2,
          notes: "",
          photoUrl: PRESET_PHOTOS["Dog"] || "",
        });
      }
    }
  }, [isOpen, initialOwnerId, petToEdit]);

  if (!isOpen) return null;

  // DOB & Age calculation handler
  const handleDobChange = (dobStr: string) => {
    if (!dobStr) {
      setFormData((prev) => ({ ...prev, dateOfBirth: "" }));
      return;
    }

    const birthDate = new Date(dobStr);
    const today = new Date();

    if (birthDate > today) {
      setError("Date of Birth cannot be in the future.");
      setFormData((prev) => ({ ...prev, dateOfBirth: dobStr }));
      return;
    }

    setError(null);

    // Calculate age in years
    let calculatedAge = today.getFullYear() - birthDate.getFullYear();
    const monthDiff = today.getMonth() - birthDate.getMonth();
    if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birthDate.getDate())) {
      calculatedAge--;
    }
    const safeAge = Math.max(0, calculatedAge);

    setFormData((prev) => ({
      ...prev,
      dateOfBirth: dobStr,
      age: safeAge,
    }));
  };

  const handleAgeChange = (newAge: number) => {
    if (newAge < 0) return;
    setFormData((prev) => ({
      ...prev,
      age: newAge,
    }));
  };

  const handleSpeciesChange = (newSpecies: string) => {
    setFormData((prev) => ({
      ...prev,
      species: newSpecies,
      photoUrl: prev.photoUrl === PRESET_PHOTOS[prev.species] || !prev.photoUrl
        ? PRESET_PHOTOS[newSpecies] || prev.photoUrl
        : prev.photoUrl,
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccessMsg(null);

    // Validations
    if (!isValidOwnerId(formData.ownerId) && !(formData.ownerFullName && formData.ownerEmail)) {
      setError("Please provide a valid Owner ID or Owner Name and Email.");
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

    if (formData.dateOfBirth) {
      const dob = new Date(formData.dateOfBirth);
      if (dob > new Date()) {
        setError("Date of Birth cannot be in the future.");
        return;
      }
    }

    setLoading(true);

    try {
      if (isEditMode && petToEdit) {
        // UC-06 & UC-07: Update Pet Profile (Strictly non-clinical fields)
        const updateDto: UpdatePetProfileDto = {
          name: formData.name.trim(),
          species: formData.species.trim(),
          breed: formData.breed.trim(),
          dateOfBirth: formData.dateOfBirth ? new Date(formData.dateOfBirth).toISOString() : null,
          age: Number(formData.age),
          notes: formData.notes.trim() || null,
          photoUrl: formData.photoUrl.trim() || null,
        };

        const updated = await petService.updatePetProfile(petToEdit.id, updateDto, formData.ownerId);
        setSuccessMsg(`Pet profile for "${updated.name}" (${updated.id}) updated successfully!`);
        if (onSuccess) onSuccess(updated);
      } else {
        // UC-05: Register Pet
        const createDto: CreatePetDto = {
          ownerId: formData.ownerId.trim(),
          owner: {
            fullName: formData.ownerFullName.trim(),
            email: formData.ownerEmail.trim(),
            phoneNumber: "",
            address: null,
          },
          name: formData.name.trim(),
          species: formData.species.trim(),
          breed: formData.breed.trim(),
          dateOfBirth: formData.dateOfBirth ? new Date(formData.dateOfBirth).toISOString() : null,
          age: Number(formData.age),
          notes: formData.notes.trim() || null,
          photoUrl: formData.photoUrl.trim() || null,
        };

        const created = await petService.createPet(createDto);
        setSuccessMsg(`Pet "${created.name}" registered successfully with ID ${created.id}!`);
        if (onSuccess) onSuccess(created);
      }

      setTimeout(() => {
        onClose();
      }, 1100);
    } catch (err: any) {
      setError(err.message || "Failed to save pet profile.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={isEditMode ? `Edit Patient Profile · ${petToEdit?.name}` : "Register New Patient"}
      onClose={onClose}
    >
      <form onSubmit={handleSubmit}>
        {/* Protected Clinical Records Notice in Edit Mode */}
        {isEditMode && (
          <div
            style={{
              marginBottom: "14px",
              padding: "10px 12px",
              borderRadius: "10px",
              background: "#f0f8ff",
              border: "1px solid #cce4ff",
              color: "#1e429f",
              display: "flex",
              alignItems: "flex-start",
              gap: "8px",
              fontSize: "11px",
            }}
          >
            <Lock size={15} style={{ flexShrink: 0, marginTop: "2px", color: "#1a56db" }} />
            <div>
              <strong>Protected Clinical Record</strong>
              <p style={{ margin: "2px 0 0", lineHeight: 1.4, fontSize: "10px" }}>
                Only general profile fields (Name, Species, Breed, Age/DOB, Notes, Photo) are editable.
                Diagnostic records, medical charts, and vaccination logs remain strictly immutable here and can only be altered by authorized veterinary personnel.
              </p>
            </div>
          </div>
        )}

        {/* Error Alert */}
        {error && (
          <div className="form-error" style={{ marginBottom: "14px" }}>
            <div style={{ display: "flex", gap: "8px", alignItems: "flex-start" }}>
              <AlertCircle size={16} style={{ flexShrink: 0, marginTop: "2px" }} />
              <div>
                <strong>Form Validation Notice</strong>
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
          {/* Owner ID (e.g. OWN-2001) */}
          <div style={{ gridColumn: "1 / -1" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "4px" }}>
              <label htmlFor="pet-owner-id" style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                <User size={13} /> Owner ID *
              </label>
              {!isEditMode && (
                <button
                  type="button"
                  onClick={() => setFormData((prev) => ({ ...prev, ownerId: generateShortId("OWN") }))}
                  style={{
                    fontSize: "10px",
                    background: "none",
                    border: "none",
                    color: "var(--primary)",
                    cursor: "pointer",
                    textDecoration: "underline",
                  }}
                >
                  Generate ID
                </button>
              )}
            </div>
            <input
              id="pet-owner-id"
              type="text"
              required
              disabled={isEditMode}
              placeholder="e.g. OWN-2001"
              value={formData.ownerId}
              onChange={(e) => setFormData({ ...formData, ownerId: e.target.value.toUpperCase() })}
              style={{
                width: "100%",
                borderColor: formData.ownerId && !isValidOwnerId(formData.ownerId) ? "var(--danger)" : undefined,
                background: isEditMode ? "#f3f7f5" : undefined,
              }}
            />
            {formData.ownerId && !isValidOwnerId(formData.ownerId) && (
              <span style={{ color: "var(--danger)", fontSize: "10px", display: "block", marginTop: "2px" }}>
                Invalid Owner ID format (e.g. OWN-2001).
              </span>
            )}

            {/* Owner Full Name */}
            <label>
              Owner Full Name *
              <input
                type="text"
                required={!isValidOwnerId(formData.ownerId)}
                placeholder="e.g. John Doe"
                value={formData.ownerFullName}
                onChange={(e) => setFormData({ ...formData, ownerFullName: e.target.value })}
              />
            </label>

            {/* Owner Email / Contact */}
            <label>
              Owner Email *
              <input
                type="email"
                required={!isValidOwnerId(formData.ownerId)}
                placeholder="john@example.com"
                value={formData.ownerEmail}
                onChange={(e) => setFormData({ ...formData, ownerEmail: e.target.value })}
              />
            </label>

            {/* Quick Demo Owner Presets */}
            {!isEditMode && (
              <div style={{ display: "flex", gap: "6px", alignItems: "center", marginTop: "6px", flexWrap: "wrap" }}>
                <span style={{ fontSize: "10px", color: "var(--muted)" }}>Client presets:</span>
                {DEMO_OWNERS.map((owner) => (
                  <button
                    key={owner.id}
                    type="button"
                    onClick={() => setFormData((prev) => ({ ...prev, ownerId: owner.id }))}
                    style={{
                      fontSize: "10px",
                      padding: "2px 8px",
                      borderRadius: "6px",
                      border: "1px solid var(--line)",
                      background: formData.ownerId.toUpperCase() === owner.id.toUpperCase() ? "var(--primary-soft)" : "#fff",
                      color: formData.ownerId.toUpperCase() === owner.id.toUpperCase() ? "var(--primary-deep)" : "var(--ink)",
                      cursor: "pointer",
                      fontWeight: formData.ownerId.toUpperCase() === owner.id.toUpperCase() ? 700 : 500,
                    }}
                  >
                    {owner.fullName} ({owner.id})
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Pet Name */}
          <label>
            Pet Name *
            <input
              type="text"
              required
              placeholder="e.g. Buddy, Milo, Luna"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            />
          </label>

          {/* Species */}
          <label>
            Species *
            <select
              value={formData.species}
              onChange={(e) => handleSpeciesChange(e.target.value)}
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
              placeholder="e.g. Golden Retriever, British Shorthair"
              value={formData.breed}
              onChange={(e) => setFormData({ ...formData, breed: e.target.value })}
            />
          </label>

          {/* Date of Birth with Automatic Age Calculation */}
          <label>
            Date of Birth (DOB)
            <input
              type="date"
              max={new Date().toISOString().split("T")[0]}
              value={formData.dateOfBirth}
              onChange={(e) => handleDobChange(e.target.value)}
            />
          </label>

          {/* Age (Years) */}
          <label>
            Age (Years) *
            <input
              type="number"
              required
              min="0"
              max="50"
              value={formData.age}
              onChange={(e) => handleAgeChange(Number(e.target.value))}
            />
          </label>

          {/* Photo URL & Quick Presets */}
          <div style={{ gridColumn: "1 / -1" }}>
            <label>
              Photo URL (Optional)
              <input
                type="url"
                placeholder="https://images.unsplash.com/photo-..."
                value={formData.photoUrl}
                onChange={(e) => setFormData({ ...formData, photoUrl: e.target.value })}
              />
            </label>
            {formData.photoUrl && (
              <div
                style={{
                  marginTop: "8px",
                  display: "flex",
                  alignItems: "center",
                  gap: "10px",
                  padding: "8px",
                  background: "#f9fbfa",
                  borderRadius: "8px",
                  border: "1px solid var(--line)",
                }}
              >
                <img
                  src={formData.photoUrl}
                  alt="Pet Preview"
                  onError={(e) => ((e.target as HTMLElement).style.display = "none")}
                  style={{ width: "42px", height: "42px", borderRadius: "8px", objectFit: "cover" }}
                />
                <span style={{ fontSize: "11px", color: "var(--muted)" }}>Photo preview attached</span>
              </div>
            )}
          </div>

          {/* Notes / Behavioral / Allergy Summary */}
          <div style={{ gridColumn: "1 / -1" }}>
            <label>
              Notes & Allergies (Optional)
              <textarea
                rows={3}
                placeholder="e.g. Allergic to chicken-based dry food. Friendly temperament. Indoor pet."
                value={formData.notes}
                onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
              />
            </label>
          </div>
        </div>

        {/* Modal Actions */}
        <div className="modal-actions">
          <Button variant="secondary" type="button" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button
            type="submit"
            disabled={loading}
            icon={loading ? <Loader2 size={16} className="animate-spin" /> : isEditMode ? <Edit3 size={16} /> : <PlusCircle size={16} />}
          >
            {loading ? "Saving..." : isEditMode ? "Update Profile" : "Register Pet"}
          </Button>
        </div>
      </form>
    </Modal>
  );
};
