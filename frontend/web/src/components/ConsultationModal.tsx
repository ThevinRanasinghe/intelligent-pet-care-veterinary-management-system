import React, { useEffect, useState } from "react";
import {
  AlertCircle,
  Calendar,
  Check,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clock,
  Compass,
  FileText,
  Image as ImageIcon,
  Loader2,
  MapPin,
  Navigation,
  PawPrint,
  ShieldCheck,
  Sparkles,
  UploadCloud,
  User,
  Wallet,
  X,
} from "lucide-react";
import { Modal } from "./ui/Modal";
import { Button } from "./ui/Button";
import { Badge } from "./ui/Badge";
import {
  CLINIC_BRANCHES,
  DEMO_BRANCHES,
  DEMO_OWNERS,
  calculateDistanceKm,
  consultationService,
  findNearestClinic,
  generateShortId,
  isValidOwnerId,
  isValidPetId,
  type ConsultationRequest,
  type ClinicBranch,
  type Pet,
} from "../services/api";
import { formatDate, formatLkr } from "../utils/format";

interface ConsultationModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: (request: ConsultationRequest) => void;
  initialOwnerId?: string;
  initialPetId?: string;
  availablePets?: Pet[];
}

const SAMPLE_SYMPTOM_PHOTOS = [
  { label: "Ear Irritation", url: "https://images.unsplash.com/photo-1543466835-00a7907e9de1?w=400&q=80" },
  { label: "Skin Rash", url: "https://images.unsplash.com/photo-1583511655857-d19b40a7a54e?w=400&q=80" },
  { label: "Eye Discharge", url: "https://images.unsplash.com/photo-1537151625747-768eb6cf92b2?w=400&q=80" },
];

const TIME_SLOTS = [
  { id: "morning", label: "Morning Shift (09:00 - 12:00)", timeString: "09:30:00" },
  { id: "afternoon", label: "Afternoon Shift (13:00 - 16:00)", timeString: "14:00:00" },
  { id: "evening", label: "Evening Shift (17:00 - 20:00)", timeString: "17:30:00" },
];

export const ConsultationModal: React.FC<ConsultationModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
  initialOwnerId,
  initialPetId,
  availablePets = [],
}) => {
  const tomorrow = new Date(Date.now() + 86400000).toISOString().split("T")[0];

  // Wizard state: 1 to 5
  const [currentStep, setCurrentStep] = useState<number>(1);

  // Form payload state
  const [ownerId, setOwnerId] = useState<string>(initialOwnerId || DEMO_OWNERS[0].id);
  const [petId, setPetId] = useState<string>(initialPetId || "");
  const [manualPetId, setManualPetId] = useState<boolean>(false);
  const [ownershipVerified, setOwnershipVerified] = useState<boolean | null>(null);
  const [verifyingOwnership, setVerifyingOwnership] = useState<boolean>(false);

  const [symptomsDescription, setSymptomsDescription] = useState<string>("");
  const [photoUrl, setPhotoUrl] = useState<string>("");

  const [preferredDate, setPreferredDate] = useState<string>(tomorrow);
  const [selectedTimeSlot, setSelectedTimeSlot] = useState<string>(TIME_SLOTS[0].id);

  const [preferredBranch, setPreferredBranch] = useState<string>(DEMO_BRANCHES[0]);
  const [latitude, setLatitude] = useState<number>(6.9044);
  const [longitude, setLongitude] = useState<number>(79.8528);
  const [gpsDetecting, setGpsDetecting] = useState<boolean>(false);
  const [nearestClinicInfo, setNearestClinicInfo] = useState<(ClinicBranch & { distanceKm: number }) | null>(null);

  const [budgetLimit, setBudgetLimit] = useState<number>(5000);

  // UI status state
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Sync initial props
  useEffect(() => {
    if (isOpen) {
      setCurrentStep(1);
      setError(null);
      setSuccessMsg(null);
      const activeOwner = initialOwnerId || DEMO_OWNERS[0].id;
      setOwnerId(activeOwner);

      // Find matching pets for this owner
      const matching = availablePets.filter(
        (p) => p.ownerId.toLowerCase() === activeOwner.toLowerCase()
      );
      if (initialPetId) {
        setPetId(initialPetId);
      } else if (matching.length > 0) {
        setPetId(matching[0].id);
      } else if (availablePets.length > 0) {
        setPetId(availablePets[0].id);
      } else {
        setPetId("PET-1001");
      }

      // Default GPS coords to Colombo 03 Central Clinic
      setLatitude(6.9044);
      setLongitude(79.8528);
      const initialNearest = findNearestClinic(6.9044, 79.8528);
      setNearestClinicInfo(initialNearest);
    }
  }, [isOpen, initialOwnerId, initialPetId, availablePets]);

  // Ownership verification check
  const verifyOwnership = async (candidatePetId: string, candidateOwnerId: string) => {
    if (!isValidPetId(candidatePetId) || !isValidOwnerId(candidateOwnerId)) {
      setOwnershipVerified(null);
      return;
    }

    setVerifyingOwnership(true);
    try {
      const res = await consultationService.validateOwnership(candidatePetId, candidateOwnerId);
      setOwnershipVerified(res.isValid);
      if (!res.isValid) {
        setError(`Pet '${candidatePetId}' does not belong to owner '${candidateOwnerId}'.`);
      } else {
        setError(null);
      }
    } catch {
      // In offline / mock mode fallback to local array
      const localPet = availablePets.find((p) => p.id === candidatePetId);
      if (localPet) {
        setOwnershipVerified(localPet.ownerId.toUpperCase() === candidateOwnerId.toUpperCase());
      } else {
        setOwnershipVerified(true);
      }
    } finally {
      setVerifyingOwnership(false);
    }
  };

  useEffect(() => {
    if (petId && ownerId) {
      verifyOwnership(petId, ownerId);
    }
  }, [petId, ownerId]);

  if (!isOpen) return null;

  // Filtered pets for active owner
  const ownerPets = availablePets.filter(
    (p) => p.ownerId.toLowerCase() === ownerId.toLowerCase()
  );
  const selectedPet = availablePets.find((p) => p.id === petId);

  // GPS Geolocation Detector
  const handleDetectGPS = () => {
    setGpsDetecting(true);
    setError(null);

    if ("geolocation" in navigator) {
      navigator.geolocation.getCurrentPosition(
        (pos) => {
          const lat = Math.round(pos.coords.latitude * 10000) / 10000;
          const lng = Math.round(pos.coords.longitude * 10000) / 10000;
          setLatitude(lat);
          setLongitude(lng);
          const nearest = findNearestClinic(lat, lng);
          setNearestClinicInfo(nearest);
          setPreferredBranch(nearest.name);
          setGpsDetecting(false);
        },
        () => {
          // Fallback location near clinic network (Colombo)
          const simulatedLat = 6.9044;
          const simulatedLng = 79.8528;
          setLatitude(simulatedLat);
          setLongitude(simulatedLng);
          const nearest = findNearestClinic(simulatedLat, simulatedLng);
          setNearestClinicInfo(nearest);
          setPreferredBranch(nearest.name);
          setGpsDetecting(false);
        },
        { timeout: 5000 }
      );
    } else {
      setGpsDetecting(false);
      setError("Geolocation is not supported by your browser. Default coordinates applied.");
    }
  };

  // Image file uploader
  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (file.size > 5 * 1024 * 1024) {
        setError("Image file size should be less than 5MB.");
        return;
      }
      const reader = new FileReader();
      reader.onload = (uploadEvent) => {
        const result = uploadEvent.target?.result as string;
        setPhotoUrl(result);
        setError(null);
      };
      reader.readAsDataURL(file);
    }
  };

  // Step Validation & Navigation
  const validateCurrentStep = (): boolean => {
    setError(null);

    if (currentStep === 1) {
      if (!isValidOwnerId(ownerId)) {
        setError("Please enter a valid Owner Short ID (e.g. OWN-2001).");
        return false;
      }
      if (!isValidPetId(petId)) {
        setError("Please select or enter a valid Pet Short ID (e.g. PET-1001).");
        return false;
      }
      if (ownershipVerified === false) {
        setError(`Pet ${petId} does not belong to owner ${ownerId}. Please select a verified pet.`);
        return false;
      }
      return true;
    }

    if (currentStep === 2) {
      if (!symptomsDescription.trim() || symptomsDescription.trim().length < 5) {
        setError("Please provide a symptom description with at least 5 characters.");
        return false;
      }
      return true;
    }

    if (currentStep === 3) {
      if (!preferredDate) {
        setError("Please pick a preferred appointment date.");
        return false;
      }
      return true;
    }

    if (currentStep === 4) {
      if (latitude < -90 || latitude > 90) {
        setError("Latitude must be between -90 and 90 degrees.");
        return false;
      }
      if (longitude < -180 || longitude > 180) {
        setError("Longitude must be between -180 and 180 degrees.");
        return false;
      }
      return true;
    }

    if (currentStep === 5) {
      if (budgetLimit <= 0) {
        setError("Budget limit must be greater than 0 LKR.");
        return false;
      }
      return true;
    }

    return true;
  };

  const nextStep = () => {
    if (validateCurrentStep()) {
      setCurrentStep((prev) => Math.min(5, prev + 1));
    }
  };

  const prevStep = () => {
    setError(null);
    setCurrentStep((prev) => Math.max(1, prev - 1));
  };

  // Submission handler
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validateCurrentStep()) return;

    setLoading(true);
    setError(null);
    setSuccessMsg(null);

    try {
      const selectedSlot = TIME_SLOTS.find((s) => s.id === selectedTimeSlot) || TIME_SLOTS[0];
      const combinedDateTime = new Date(`${preferredDate}T${selectedSlot.timeString}`).toISOString();

      const payload = {
        ownerId: ownerId.trim(),
        petId: petId.trim(),
        symptomsDescription: symptomsDescription.trim(),
        photoUrl: photoUrl.trim() || null,
        preferredDate: combinedDateTime,
        budgetLimit: Number(budgetLimit),
        preferredClinicLocationLat: latitude,
        preferredClinicLocationLong: longitude,
        preferredBranch,
      };

      const response = await consultationService.createConsultation(payload);
      setSuccessMsg(`Consultation Request '${response.id}' submitted successfully with status: ${response.status}!`);
      if (onSuccess) onSuccess(response);

      setTimeout(() => {
        onClose();
      }, 1200);
    } catch (err: any) {
      setError(err.message || "Failed to submit consultation request.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal title="New Consultation Request" onClose={onClose}>
      {/* Wizard Progress Bar */}
      <div style={{ marginBottom: "18px" }}>
        <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "8px" }}>
          {[
            { step: 1, label: "Select Patient" },
            { step: 2, label: "Symptoms" },
            { step: 3, label: "Schedule" },
            { step: 4, label: "Location" },
            { step: 5, label: "Confirm" },
          ].map((item) => (
            <div
              key={item.step}
              style={{
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                flex: 1,
                cursor: item.step < currentStep ? "pointer" : "default",
              }}
              onClick={() => {
                if (item.step < currentStep) setCurrentStep(item.step);
              }}
            >
              <div
                style={{
                  width: "26px",
                  height: "26px",
                  borderRadius: "50%",
                  display: "grid",
                  placeItems: "center",
                  fontSize: "11px",
                  fontWeight: 800,
                  background:
                    item.step === currentStep
                      ? "var(--primary)"
                      : item.step < currentStep
                      ? "var(--primary-soft)"
                      : "#edf2f0",
                  color:
                    item.step === currentStep
                      ? "#fff"
                      : item.step < currentStep
                      ? "var(--primary-deep)"
                      : "#85938f",
                  border: item.step === currentStep ? "2px solid var(--primary-deep)" : "none",
                  transition: "all 0.15s ease",
                }}
              >
                {item.step < currentStep ? <Check size={14} /> : item.step}
              </div>
              <span
                style={{
                  fontSize: "9px",
                  marginTop: "4px",
                  fontWeight: item.step === currentStep ? 700 : 500,
                  color: item.step === currentStep ? "var(--ink)" : "var(--muted)",
                  textAlign: "center",
                }}
              >
                {item.label}
              </span>
            </div>
          ))}
        </div>
        <div style={{ height: "4px", background: "#e2ebe7", borderRadius: "99px", overflow: "hidden" }}>
          <div
            style={{
              height: "100%",
              width: `${(currentStep / 5) * 100}%`,
              background: "var(--primary)",
              transition: "width 0.2s ease",
            }}
          />
        </div>
      </div>

      {/* Error Alert */}
      {error && (
        <div className="form-error" style={{ marginBottom: "14px" }}>
          <div style={{ display: "flex", gap: "8px", alignItems: "flex-start" }}>
            <AlertCircle size={16} style={{ flexShrink: 0, marginTop: "2px" }} />
            <div>
              <strong>Notice</strong>
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

      <form onSubmit={handleSubmit}>
        {/* STEP 1: PATIENT SELECTION */}
        {currentStep === 1 && (
          <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
            <div>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "4px" }}>
                <label style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                  <User size={13} /> Owner Short ID (e.g. OWN-2001) *
                </label>
                <button
                  type="button"
                  onClick={() => setOwnerId(generateShortId("OWN"))}
                  style={{ fontSize: "10px", background: "none", border: "none", color: "var(--primary)", cursor: "pointer", textDecoration: "underline" }}
                >
                  Generate ID
                </button>
              </div>
              <input
                type="text"
                required
                placeholder="e.g. OWN-2001"
                value={ownerId}
                onChange={(e) => setOwnerId(e.target.value.toUpperCase())}
                style={{ width: "100%" }}
              />
              <div style={{ display: "flex", gap: "6px", alignItems: "center", marginTop: "6px", flexWrap: "wrap" }}>
                <span style={{ fontSize: "10px", color: "var(--muted)" }}>Preset Owners:</span>
                {DEMO_OWNERS.map((owner) => (
                  <button
                    key={owner.id}
                    type="button"
                    onClick={() => setOwnerId(owner.id)}
                    style={{
                      fontSize: "10px",
                      padding: "2px 8px",
                      borderRadius: "6px",
                      border: "1px solid var(--line)",
                      background: ownerId.toUpperCase() === owner.id.toUpperCase() ? "var(--primary-soft)" : "#fff",
                      color: ownerId.toUpperCase() === owner.id.toUpperCase() ? "var(--primary-deep)" : "var(--ink)",
                      cursor: "pointer",
                      fontWeight: ownerId.toUpperCase() === owner.id.toUpperCase() ? 700 : 500,
                    }}
                  >
                    {owner.fullName} ({owner.id})
                  </button>
                ))}
              </div>
            </div>

            {/* Pet Selection */}
            <div>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "4px" }}>
                <label style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                  <PawPrint size={13} /> Select Patient Pet *
                </label>
                <button
                  type="button"
                  onClick={() => setManualPetId(!manualPetId)}
                  style={{ fontSize: "10px", background: "none", border: "none", color: "var(--primary)", cursor: "pointer", textDecoration: "underline" }}
                >
                  {manualPetId ? "Select from owner pets" : "Enter manual Pet ID"}
                </button>
              </div>

              {!manualPetId && ownerPets.length > 0 ? (
                <select
                  value={petId}
                  onChange={(e) => setPetId(e.target.value)}
                  style={{ width: "100%" }}
                >
                  {ownerPets.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} ({p.species} · {p.breed}) — {p.id}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  type="text"
                  required
                  placeholder="e.g. PET-1001"
                  value={petId}
                  onChange={(e) => setPetId(e.target.value.toUpperCase())}
                  style={{ width: "100%" }}
                />
              )}
            </div>

            {/* Ownership Pre-Validation Badge & Selected Pet Card */}
            <div
              style={{
                padding: "12px",
                borderRadius: "12px",
                background: ownershipVerified ? "#f3faf6" : "#fffbf5",
                border: `1px solid ${ownershipVerified ? "#d1ecd9" : "#fbe3c2"}`,
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                gap: "10px",
                flexWrap: "wrap",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                <div
                  style={{
                    width: "36px",
                    height: "36px",
                    borderRadius: "10px",
                    background: ownershipVerified ? "var(--success-soft)" : "var(--warning-soft)",
                    color: ownershipVerified ? "var(--success)" : "var(--warning)",
                    display: "grid",
                    placeItems: "center",
                    flexShrink: 0,
                  }}
                >
                  {verifyingOwnership ? <Loader2 size={16} className="animate-spin" /> : ownershipVerified ? <ShieldCheck size={18} /> : <AlertCircle size={18} />}
                </div>
                <div>
                  <strong style={{ fontSize: "12px", color: "var(--ink)" }}>
                    {selectedPet ? selectedPet.name : petId || "Pet Pending"}
                  </strong>
                  <div style={{ fontSize: "10px", color: "var(--muted)" }}>
                    {selectedPet ? `${selectedPet.species} (${selectedPet.breed}) · ${selectedPet.age} yrs` : "Short ID Verification"}
                  </div>
                </div>
              </div>

              <div>
                {verifyingOwnership ? (
                  <Badge tone="neutral">Validating ownership...</Badge>
                ) : ownershipVerified ? (
                  <Badge tone="success">
                    <ShieldCheck size={12} style={{ marginRight: "4px" }} /> Verified Patient
                  </Badge>
                ) : (
                  <Badge tone="warning">Ownership Check Pending</Badge>
                )}
              </div>
            </div>
          </div>
        )}

        {/* STEP 2: SYMPTOMS & PHOTO ATTACHMENT */}
        {currentStep === 2 && (
          <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
            <div>
              <label>
                Clinical Symptoms Description *
                <textarea
                  rows={4}
                  required
                  placeholder="Describe the pet's current symptoms, onset timeline, behavioral changes, appetite, energy levels, etc."
                  value={symptomsDescription}
                  onChange={(e) => setSymptomsDescription(e.target.value)}
                  style={{ marginTop: "6px" }}
                />
              </label>
              <div style={{ textAlign: "right", fontSize: "10px", color: "var(--muted)", marginTop: "2px" }}>
                {symptomsDescription.length} characters (min 5)
              </div>
            </div>

            {/* Photo Attachment & Upload Picker */}
            <div>
              <label style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "6px" }}>
                <ImageIcon size={14} /> Symptom Photo Attachment (Optional)
              </label>

              {/* Upload or URL input */}
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "10px", marginBottom: "10px" }}>
                <label
                  style={{
                    border: "1px dashed var(--line)",
                    borderRadius: "10px",
                    padding: "12px",
                    textAlign: "center",
                    cursor: "pointer",
                    background: "#fbfdfc",
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    gap: "4px",
                  }}
                >
                  <UploadCloud size={20} style={{ color: "var(--primary)" }} />
                  <span style={{ fontSize: "11px", fontWeight: 700, color: "var(--ink)" }}>Upload Device Photo</span>
                  <small style={{ fontSize: "9px", color: "var(--muted)" }}>PNG, JPG up to 5MB</small>
                  <input
                    type="file"
                    accept="image/*"
                    onChange={handleFileUpload}
                    style={{ display: "none" }}
                  />
                </label>

                <div>
                  <input
                    type="url"
                    placeholder="Or paste direct image URL..."
                    value={photoUrl.startsWith("data:") ? "" : photoUrl}
                    onChange={(e) => setPhotoUrl(e.target.value)}
                    style={{ height: "42px", marginTop: 0 }}
                  />
                  <div style={{ display: "flex", gap: "4px", marginTop: "6px", flexWrap: "wrap" }}>
                    <span style={{ fontSize: "9px", color: "var(--muted)" }}>Samples:</span>
                    {SAMPLE_SYMPTOM_PHOTOS.map((sample) => (
                      <button
                        key={sample.label}
                        type="button"
                        onClick={() => setPhotoUrl(sample.url)}
                        style={{
                          fontSize: "9px",
                          padding: "2px 6px",
                          borderRadius: "4px",
                          border: "1px solid var(--line)",
                          background: "#fff",
                          cursor: "pointer",
                        }}
                      >
                        {sample.label}
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* Live Preview Box */}
              {photoUrl && (
                <div
                  style={{
                    padding: "10px",
                    borderRadius: "10px",
                    background: "#f7faf8",
                    border: "1px solid var(--line)",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                    <img
                      src={photoUrl}
                      alt="Symptom preview"
                      onError={(e) => ((e.target as HTMLElement).style.display = "none")}
                      style={{ width: "50px", height: "50px", borderRadius: "8px", objectFit: "cover" }}
                    />
                    <div>
                      <strong style={{ fontSize: "11px", color: "var(--ink)", display: "block" }}>
                        Symptom Photo Attached
                      </strong>
                      <span style={{ fontSize: "9px", color: "var(--muted)" }}>
                        {photoUrl.startsWith("data:") ? "Local upload file attached" : photoUrl.slice(0, 45) + "..."}
                      </span>
                    </div>
                  </div>
                  <Button
                    variant="ghost"
                    onClick={() => setPhotoUrl("")}
                    icon={<X size={14} />}
                    title="Remove Photo"
                  />
                </div>
              )}
            </div>
          </div>
        )}

        {/* STEP 3: PREFERRED DATE & TIME SLOT */}
        {currentStep === 3 && (
          <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
            <div>
              <label style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "4px" }}>
                <Calendar size={13} /> Preferred Appointment Date *
              </label>
              <input
                type="date"
                required
                min={new Date().toISOString().split("T")[0]}
                value={preferredDate}
                onChange={(e) => setPreferredDate(e.target.value)}
                style={{ width: "100%" }}
              />
            </div>

            <div>
              <label style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "6px" }}>
                <Clock size={13} /> Preferred Time Shift Slot *
              </label>
              <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                {TIME_SLOTS.map((slot) => (
                  <label
                    key={slot.id}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "10px",
                      padding: "10px 12px",
                      borderRadius: "10px",
                      border: `1px solid ${selectedTimeSlot === slot.id ? "var(--primary)" : "var(--line)"}`,
                      background: selectedTimeSlot === slot.id ? "var(--primary-soft)" : "#fff",
                      cursor: "pointer",
                    }}
                  >
                    <input
                      type="radio"
                      name="timeSlot"
                      value={slot.id}
                      checked={selectedTimeSlot === slot.id}
                      onChange={() => setSelectedTimeSlot(slot.id)}
                      style={{ margin: 0, width: "auto" }}
                    />
                    <div>
                      <strong style={{ fontSize: "12px", color: "var(--ink)", display: "block" }}>{slot.label}</strong>
                      <span style={{ fontSize: "10px", color: "var(--muted)" }}>Scheduled window starting {slot.timeString.slice(0, 5)}</span>
                    </div>
                  </label>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* STEP 4: LOCATION & NEAREST CLINIC MATCHING */}
        {currentStep === 4 && (
          <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
            {/* GPS Detection Bar */}
            <div
              style={{
                padding: "12px",
                borderRadius: "12px",
                background: "#f0f8ff",
                border: "1px solid #cce4ff",
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                flexWrap: "wrap",
                gap: "10px",
              }}
            >
              <div>
                <strong style={{ fontSize: "12px", color: "#1e429f", display: "flex", alignItems: "center", gap: "6px" }}>
                  <Navigation size={14} /> Nearest Clinic Matcher
                </strong>
                <p style={{ margin: "2px 0 0", fontSize: "10px", color: "#375796" }}>
                  Auto-detect current GPS coordinates to calculate proximity to all clinic facilities.
                </p>
              </div>
              <Button
                type="button"
                variant="secondary"
                onClick={handleDetectGPS}
                disabled={gpsDetecting}
                icon={gpsDetecting ? <Loader2 size={13} className="animate-spin" /> : <Compass size={13} />}
                style={{ fontSize: "11px", minHeight: "32px" }}
              >
                {gpsDetecting ? "Locating..." : "Detect GPS"}
              </Button>
            </div>

            {/* Nearest Clinic Highlight */}
            {nearestClinicInfo && (
              <div
                style={{
                  padding: "10px 12px",
                  borderRadius: "10px",
                  background: "var(--success-soft)",
                  border: "1px solid #d6ecdf",
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                }}
              >
                <div>
                  <span style={{ fontSize: "9px", textTransform: "uppercase", fontWeight: 800, color: "var(--success)" }}>
                    Recommended Nearest Branch
                  </span>
                  <strong style={{ display: "block", fontSize: "13px", color: "var(--ink)" }}>
                    {nearestClinicInfo.name}
                  </strong>
                  <span style={{ fontSize: "10px", color: "var(--muted)" }}>{nearestClinicInfo.address}</span>
                </div>
                <Badge tone="success">{nearestClinicInfo.distanceKm} km away</Badge>
              </div>
            )}

            {/* Preferred Branch Selector */}
            <div>
              <label style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "4px" }}>
                <MapPin size={13} /> Preferred Branch *
              </label>
              <select
                value={preferredBranch}
                onChange={(e) => {
                  const bName = e.target.value;
                  setPreferredBranch(bName);
                  const br = CLINIC_BRANCHES.find((b) => b.name === bName);
                  if (br) {
                    setLatitude(br.lat);
                    setLongitude(br.lng);
                  }
                }}
                style={{ width: "100%" }}
              >
                {CLINIC_BRANCHES.map((branch) => {
                  const dist = calculateDistanceKm(latitude, longitude, branch.lat, branch.lng);
                  return (
                    <option key={branch.name} value={branch.name}>
                      {branch.name} ({branch.address}) ~ {dist} km
                    </option>
                  );
                })}
              </select>
            </div>

            {/* Lat/Long Input Fields */}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "10px" }}>
              <label>
                Latitude (-90 to 90)
                <input
                  type="number"
                  step="0.0001"
                  value={latitude}
                  onChange={(e) => {
                    const val = Number(e.target.value);
                    setLatitude(val);
                    setNearestClinicInfo(findNearestClinic(val, longitude));
                  }}
                />
              </label>
              <label>
                Longitude (-180 to 180)
                <input
                  type="number"
                  step="0.0001"
                  value={longitude}
                  onChange={(e) => {
                    const val = Number(e.target.value);
                    setLongitude(val);
                    setNearestClinicInfo(findNearestClinic(latitude, val));
                  }}
                />
              </label>
            </div>
          </div>
        )}

        {/* STEP 5: BUDGET & CONFIRMATION */}
        {currentStep === 5 && (
          <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
            {/* Budget Input */}
            <div>
              <label style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "4px" }}>
                <Wallet size={13} /> Budget Limit (LKR) *
              </label>
              <input
                type="number"
                required
                min="500"
                step="500"
                value={budgetLimit}
                onChange={(e) => setBudgetLimit(Number(e.target.value))}
                style={{ width: "100%" }}
              />
              <span style={{ fontSize: "10px", color: "var(--muted)", marginTop: "2px", display: "block" }}>
                Used by veterinarians and managers to tailor medication and diagnostic proposals.
              </span>
            </div>

            {/* Summary Card */}
            <div
              style={{
                padding: "14px",
                borderRadius: "14px",
                background: "#fbfdfc",
                border: "1px solid var(--line)",
                display: "flex",
                flexDirection: "column",
                gap: "10px",
              }}
            >
              <div style={{ fontSize: "11px", fontWeight: 800, textTransform: "uppercase", color: "var(--muted)" }}>
                Consultation Request Summary
              </div>

              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "10px", fontSize: "11px" }}>
                <div>
                  <span style={{ color: "var(--muted)", display: "block", fontSize: "9px" }}>PATIENT PET</span>
                  <strong>{selectedPet?.name || petId}</strong> ({petId})
                </div>
                <div>
                  <span style={{ color: "var(--muted)", display: "block", fontSize: "9px" }}>OWNER</span>
                  <strong>{ownerId}</strong>
                </div>
                <div>
                  <span style={{ color: "var(--muted)", display: "block", fontSize: "9px" }}>BRANCH</span>
                  <strong>{preferredBranch}</strong>
                </div>
                <div>
                  <span style={{ color: "var(--muted)", display: "block", fontSize: "9px" }}>DATE & SHIFT</span>
                  <strong>{preferredDate}</strong> ({selectedTimeSlot})
                </div>
                <div>
                  <span style={{ color: "var(--muted)", display: "block", fontSize: "9px" }}>BUDGET LIMIT</span>
                  <strong style={{ color: "var(--primary-deep)" }}>{formatLkr(budgetLimit)}</strong>
                </div>
                <div>
                  <span style={{ color: "var(--muted)", display: "block", fontSize: "9px" }}>PHOTO ATTACHED</span>
                  <strong>{photoUrl ? "Yes (Preview verified)" : "None"}</strong>
                </div>
              </div>

              <div style={{ borderTop: "1px solid var(--line)", paddingTop: "8px", fontSize: "11px" }}>
                <span style={{ color: "var(--muted)", display: "block", fontSize: "9px" }}>SYMPTOMS NOTE</span>
                <p style={{ margin: "2px 0 0", color: "#364541", lineHeight: 1.4 }}>
                  {symptomsDescription}
                </p>
              </div>
            </div>
          </div>
        )}

        {/* Wizard Control Actions */}
        <div className="modal-actions" style={{ display: "flex", justifyContent: "space-between", marginTop: "20px" }}>
          <div>
            {currentStep > 1 && (
              <Button variant="secondary" type="button" onClick={prevStep} disabled={loading} icon={<ChevronLeft size={16} />}>
                Previous
              </Button>
            )}
          </div>

          <div style={{ display: "flex", gap: "8px" }}>
            <Button variant="secondary" type="button" onClick={onClose} disabled={loading}>
              Cancel
            </Button>
            {currentStep < 5 ? (
              <Button type="button" onClick={nextStep} icon={<ChevronRight size={16} />}>
                Next Step
              </Button>
            ) : (
              <Button
                type="submit"
                disabled={loading}
                icon={loading ? <Loader2 size={16} className="animate-spin" /> : <Sparkles size={16} />}
              >
                {loading ? "Submitting..." : "Submit Consultation Request"}
              </Button>
            )}
          </div>
        </div>
      </form>
    </Modal>
  );
};
