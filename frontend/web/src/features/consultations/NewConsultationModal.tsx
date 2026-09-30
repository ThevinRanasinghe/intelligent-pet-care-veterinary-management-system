import { FormEvent, useEffect, useState } from "react";
import { createPortal } from "react-dom";
import {
  X,
  PawPrint,
  CalendarDays,
  Clock3,
  MapPin,
} from "lucide-react";

import {
  petService,
  ownerService,
  consultationService,
  Pet,
  ConsultationRequestApi,
} from "../../services/api";
import {
  lookupsService,
  DayAvailability,
  MonthAvailabilityDay,
  OrganizationLookup,
} from "../../services/lookupsService";
import { messageFrom } from "../../utils/errors";
import { ClinicMap } from "../shared/maps/ClinicMap";
import {
  BookingCalendar,
  currentMonth,
  VisibleMonth,
} from "../shared/booking/BookingCalendar";
import { SlotPicker } from "../shared/booking/SlotPicker";
import { useAuth } from "../auth/AuthContext";

/* ============================================================
   FORM TYPE
   ============================================================ */

type NewConsultationForm = {
  petId: string;
  organizationId: string;
  symptoms: string;
  urgency: string;
  budget: string;
  additionalNotes: string;
};

/* ============================================================
   PROPS
   ============================================================ */

type NewConsultationModalProps = {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (consultation: ConsultationRequestApi) => void;
};

/* ============================================================
   INITIAL FORM
   ============================================================ */

const initialForm: NewConsultationForm = {
  petId: "",
  organizationId: "",
  symptoms: "",
  urgency: "Medium",
  budget: "",
  additionalNotes: "",
};

const inputStyle = {
  width: "100%",
  height: "42px",
  padding: "0 12px",
  border: "1px solid #d8d8d2",
  borderRadius: "8px",
  background: "#ffffff",
  color: "#222222",
  fontSize: "12px",
  outline: "none",
} as const;

const textareaStyle = {
  width: "100%",
  padding: "11px 12px",
  resize: "vertical",
  border: "1px solid #d8d8d2",
  borderRadius: "8px",
  background: "#ffffff",
  color: "#222222",
  fontSize: "12px",
  lineHeight: 1.5,
  outline: "none",
} as const;

const labelStyle = {
  display: "block",
  marginBottom: "6px",
  fontSize: "11px",
  fontWeight: 700,
  color: "#4b4b4b",
} as const;

const sectionTitleStyle = {
  display: "flex",
  alignItems: "center",
  gap: "7px",
  marginBottom: "12px",
  fontSize: "12px",
  fontWeight: 800,
  color: "#242424",
} as const;

/* ============================================================
   COMPONENT
   ============================================================ */

export function NewConsultationModal({
  isOpen,
  onClose,
  onSubmit,
}: NewConsultationModalProps) {
  const { user } = useAuth();
  const [form, setForm] = useState<NewConsultationForm>(initialForm);

  const [pets, setPets] = useState<Pet[]>([]);
  const [organizations, setOrganizations] = useState<OrganizationLookup[]>([]);

  const [loadingPets, setLoadingPets] = useState(false);
  const [loadingOrganizations, setLoadingOrganizations] = useState(false);

  // Booking calendar state
  const [visibleMonth, setVisibleMonth] = useState<VisibleMonth>(currentMonth());
  const [monthDays, setMonthDays] = useState<MonthAvailabilityDay[] | null>(null);
  const [loadingMonth, setLoadingMonth] = useState(false);
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const [dayAvailability, setDayAvailability] = useState<DayAvailability | null>(null);
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null);

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  const selectedOrganization = organizations.find(
    (organization) => organization.id === form.organizationId,
  );

  /* ============================================================
     LOAD REGISTERED PETS + ACTIVE ORGANIZATIONS
     ============================================================ */

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    let mounted = true;

    const loadPets = async () => {
      try {
        setLoadingPets(true);
        setError("");

        const isPetOwner = user?.role === "PetOwner";
        let data: Pet[];

        if (isPetOwner) {
          const owners = await ownerService.getAllOwners();
          const owner = owners.find((candidate) =>
            candidate.email.trim().toLowerCase() === user?.email.trim().toLowerCase(),
          );
          data = owner ? await petService.getPetsByOwner(owner.id) : [];

          if (!owner && mounted) {
            setError("Your account is not linked to a pet-owner profile yet.");
          }
        } else {
          data = await petService.getAllPets();
        }

        if (mounted) {
          setPets(Array.isArray(data) ? data : []);
        }
      } catch (err) {
        console.error("Failed to load pets:", err);

        if (mounted) {
          setPets([]);
          setError("Unable to load registered pets.");
        }
      } finally {
        if (mounted) {
          setLoadingPets(false);
        }
      }
    };

    const loadOrganizations = async () => {
      try {
        setLoadingOrganizations(true);
        const data = await lookupsService.getActiveOrganizations();
        if (mounted) {
          setOrganizations(Array.isArray(data) ? data : []);
        }
      } catch (err) {
        console.error("Failed to load organizations:", err);
        if (mounted) {
          setOrganizations([]);
          setError("Unable to load available clinics.");
        }
      } finally {
        if (mounted) {
          setLoadingOrganizations(false);
        }
      }
    };

    loadPets();
    loadOrganizations();

    return () => {
      mounted = false;
    };
  }, [isOpen, user]);

  /* ============================================================
     MONTH AVAILABILITY (per organization)
     ============================================================ */

  useEffect(() => {
    if (!isOpen || !form.organizationId) {
      setMonthDays(null);
      return;
    }

    let mounted = true;
    setLoadingMonth(true);

    lookupsService
      .getMonthAvailability(form.organizationId, visibleMonth.year, visibleMonth.month)
      .then((days) => {
        if (mounted) setMonthDays(days);
      })
      .catch((err) => {
        console.error("Failed to load month availability:", err);
        if (mounted) setMonthDays(null);
      })
      .finally(() => {
        if (mounted) setLoadingMonth(false);
      });

    return () => {
      mounted = false;
    };
  }, [isOpen, form.organizationId, visibleMonth]);

  /* ============================================================
     DAY AVAILABILITY (slots for the selected date)
     ============================================================ */

  useEffect(() => {
    if (!isOpen || !form.organizationId || !selectedDate) {
      setDayAvailability(null);
      return;
    }

    let mounted = true;
    setLoadingSlots(true);

    lookupsService
      .getAvailability(form.organizationId, selectedDate)
      .then((availability) => {
        if (mounted) setDayAvailability(availability);
      })
      .catch((err) => {
        console.error("Failed to load day availability:", err);
        if (mounted) setDayAvailability(null);
      })
      .finally(() => {
        if (mounted) setLoadingSlots(false);
      });

    return () => {
      mounted = false;
    };
  }, [isOpen, form.organizationId, selectedDate]);

  /* ============================================================
     ESC KEY
     ============================================================ */

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !submitting) {
        onClose();
      }
    };

    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen, submitting, onClose]);

  /* ============================================================
     HANDLE FIELD CHANGE
     ============================================================ */

  const handleChange = (field: keyof NewConsultationForm, value: string) => {
    setForm((previous) => ({
      ...previous,
      [field]: value,
    }));

    if (error) {
      setError("");
    }

    // A different clinic means a different availability map.
    if (field === "organizationId") {
      setSelectedDate(null);
      setSelectedSlot(null);
      setDayAvailability(null);
      setMonthDays(null);
    }
  };

  const handleSelectDate = (iso: string) => {
    setSelectedDate(iso);
    setSelectedSlot(null);
    if (error) setError("");
  };

  const handleSelectSlot = (start: string) => {
    setSelectedSlot(start);
    if (error) setError("");
  };

  /* ============================================================
     RESET FORM
     ============================================================ */

  const resetForm = () => {
    setForm(initialForm);
    setSelectedDate(null);
    setSelectedSlot(null);
    setDayAvailability(null);
    setMonthDays(null);
    setVisibleMonth(currentMonth());
    setError("");
  };

  /* ============================================================
     CLOSE MODAL
     ============================================================ */

  const handleClose = () => {
    if (submitting) {
      return;
    }

    resetForm();
    onClose();
  };

  /* ============================================================
     SUBMIT CONSULTATION
     ============================================================ */

  const canSubmit =
    !!form.petId && !!form.organizationId && !!selectedDate && !!selectedSlot;

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (submitting) {
      return;
    }

    setError("");

    if (!form.petId) {
      setError("Please select a registered pet.");
      return;
    }

    const selectedPet = pets.find((pet) => pet.id === form.petId);

    if (!selectedPet) {
      setError("Selected pet could not be found.");
      return;
    }

    if (!form.organizationId) {
      setError("Please select a clinic.");
      return;
    }

    if (!selectedDate) {
      setError("Please pick a date for the consultation.");
      return;
    }

    if (!selectedSlot) {
      setError("Please pick a time slot for the consultation.");
      return;
    }

    if (!form.symptoms.trim()) {
      setError("Please describe the pet's symptoms.");
      return;
    }

    if (form.budget.trim()) {
      const budget = Number(form.budget);

      if (Number.isNaN(budget) || budget < 0) {
        setError("Budget must be a valid positive amount.");
        return;
      }
    }

    if (!selectedPet.ownerId) {
      setError("This pet does not have a valid owner.");
      return;
    }

    try {
      setSubmitting(true);

      /* ========================================================
         STEP 1 - OWNERSHIP VALIDATION
         ======================================================== */

      const ownershipResult = await consultationService.validateOwnership(
        selectedPet.id,
        selectedPet.ownerId,
      );

      if (!ownershipResult.isValid) {
        setError(ownershipResult.message || "Pet ownership validation failed.");
        return;
      }

      /* ========================================================
         STEP 2 - CREATE CONSULTATION
         The end time is never sent — the server books exactly one
         hour from the selected slot start.
         ======================================================== */

      const createdConsultation = await consultationService.createConsultation({
        petId: selectedPet.id,

        ownerId: selectedPet.ownerId,

        organizationId: form.organizationId,

        symptoms: form.symptoms.trim(),

        urgency: form.urgency,

        preferredDate: selectedDate,

        preferredTime: `${selectedSlot}:00`,

        budget: form.budget.trim() ? Number(form.budget) : null,

        symptomPhotoUrl: null,

        additionalNotes: form.additionalNotes.trim() || null,
      });

      /* ========================================================
         STEP 3 - SEND RESULT TO PARENT
         ======================================================== */

      onSubmit(createdConsultation);

      resetForm();

      onClose();
    } catch (err) {
      console.error("Failed to create consultation:", err);

      // messageFrom surfaces ProblemDetails.detail — a 409 slot conflict
      // shows "This appointment slot is no longer available. …"
      setError(messageFrom(err) || "Failed to create consultation request.");
    } finally {
      setSubmitting(false);
    }
  };

  /* ============================================================
     DO NOT RENDER WHEN CLOSED
     ============================================================ */

  if (!isOpen) {
    return null;
  }

  /* ============================================================
     MODAL CONTENT
     ============================================================ */

  const modalContent = (
    <div
      onMouseDown={(event) => {
        /*
         * Close only when clicking the dark background.
         */
        if (event.target === event.currentTarget) {
          handleClose();
        }
      }}
      style={{
        position: "fixed",
        inset: 0,
        width: "100vw",
        height: "100vh",
        zIndex: 99999,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "24px",
        background: "rgba(0, 0, 0, 0.55)",
        backdropFilter: "blur(4px)",
        WebkitBackdropFilter: "blur(4px)",
      }}
    >
      {/* ======================================================
          MODAL CARD
          ====================================================== */}

      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="new-consultation-title"
        onMouseDown={(event) => {
          event.stopPropagation();
        }}
        style={{
          width: "min(760px, 100%)",
          maxHeight: "calc(100vh - 48px)",
          display: "flex",
          flexDirection: "column",
          overflow: "hidden",
          background: "#ffffff",
          border: "1px solid #e4e4dc",
          borderRadius: "16px",
          boxShadow: "0 25px 80px rgba(0, 0, 0, 0.30)",
        }}
      >
        {/* ====================================================
            HEADER
            ==================================================== */}

        <div
          style={{
            display: "flex",
            alignItems: "flex-start",
            justifyContent: "space-between",
            gap: "16px",
            padding: "20px 24px",
            borderBottom: "1px solid #e8e8e2",
            flexShrink: 0,
          }}
        >
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "12px",
            }}
          >
            <div
              style={{
                width: "44px",
                height: "44px",
                display: "grid",
                placeItems: "center",
                flexShrink: 0,
                borderRadius: "12px",
                background: "#fff4bf",
                color: "#9a7200",
              }}
            >
              <PawPrint size={23} />
            </div>

            <div>
              <div
                style={{
                  marginBottom: "3px",
                  fontSize: "9px",
                  fontWeight: 800,
                  letterSpacing: "0.12em",
                  color: "#9a7200",
                }}
              >
                CONSULTATION REQUEST
              </div>

              <h2
                id="new-consultation-title"
                style={{
                  margin: 0,
                  fontSize: "21px",
                  lineHeight: 1.2,
                  fontWeight: 800,
                  color: "#181818",
                }}
              >
                New Consultation
              </h2>

              <p
                style={{
                  margin: "5px 0 0",
                  fontSize: "12px",
                  color: "#777777",
                }}
              >
                Pick a clinic, a date and a one-hour slot.
              </p>
            </div>
          </div>

          {/* Close */}

          <button
            type="button"
            onClick={handleClose}
            disabled={submitting}
            aria-label="Close consultation form"
            style={{
              width: "34px",
              height: "34px",
              display: "grid",
              placeItems: "center",
              flexShrink: 0,
              border: "1px solid #d8d8d2",
              borderRadius: "8px",
              background: "#ffffff",
              color: "#555555",
              cursor: submitting ? "not-allowed" : "pointer",
              opacity: submitting ? 0.5 : 1,
            }}
          >
            <X size={19} />
          </button>
        </div>

        {/* ====================================================
            FORM
            ==================================================== */}

        <form
          onSubmit={handleSubmit}
          style={{
            display: "flex",
            flexDirection: "column",
            minHeight: 0,
          }}
        >
          {/* ==================================================
              BODY
              ================================================== */}

          <div
            style={{
              overflowY: "auto",
              padding: "22px 24px",
            }}
          >
            {/* ERROR */}

            {error && (
              <div
                role="alert"
                style={{
                  marginBottom: "18px",
                  padding: "12px 14px",
                  border: "1px solid #f2b8b5",
                  borderRadius: "9px",
                  background: "#fff1f0",
                  color: "#b42318",
                  fontSize: "12px",
                  lineHeight: 1.5,
                }}
              >
                {error}
              </div>
            )}

            {/* ==================================================
                PET INFORMATION
                ================================================== */}

            <div style={{ marginBottom: "20px" }}>
              <div style={sectionTitleStyle}>
                <PawPrint size={16} />
                <span>Pet Information</span>
              </div>

              <label htmlFor="consultation-pet" style={labelStyle}>
                Registered Pet
                <span style={{ color: "#c62828", marginLeft: "3px" }}>*</span>
              </label>

              <select
                id="consultation-pet"
                name="petId"
                aria-label="Registered Pet"
                value={form.petId}
                onChange={(event) => handleChange("petId", event.target.value)}
                disabled={loadingPets || submitting}
                style={inputStyle}
              >
                <option value="">
                  {loadingPets
                    ? "Loading registered pets..."
                    : "Select a registered pet"}
                </option>

                {pets.map((pet) => (
                  <option key={pet.id} value={pet.id}>
                    {pet.name}
                    {" — "}
                    {pet.species}
                    {pet.breed ? ` — ${pet.breed}` : ""}
                  </option>
                ))}
              </select>

              <p
                style={{
                  margin: "6px 0 0",
                  fontSize: "10px",
                  color: "#888888",
                }}
              >
                Only pets registered in the system can be selected.
              </p>
            </div>

            {/* ==================================================
                CLINIC (ORGANIZATION)
                ================================================== */}

            <div style={{ marginBottom: "20px" }}>
              <div style={sectionTitleStyle}>
                <MapPin size={16} />
                <span>Select Clinic</span>
              </div>

              {!loadingOrganizations && organizations.length > 0 && (
                <div style={{ marginBottom: "12px" }}>
                  <ClinicMap
                    clinics={organizations}
                    selectedId={form.organizationId}
                    onSelect={(clinic) =>
                      handleChange("organizationId", clinic.id)
                    }
                  />
                </div>
              )}

              {selectedOrganization && (
                <div
                  data-testid="selected-clinic-card"
                  style={{
                    marginBottom: "12px",
                    padding: "10px 12px",
                    border: "1px solid #eadf9c",
                    borderRadius: "9px",
                    background: "#fffbea",
                    fontSize: "12px",
                  }}
                >
                  <div style={{ fontWeight: 800, color: "#242424" }}>
                    {selectedOrganization.name}
                  </div>
                  {selectedOrganization.address && (
                    <div style={{ marginTop: "3px", color: "#666666" }}>
                      {selectedOrganization.address}
                    </div>
                  )}
                  <div style={{ marginTop: "3px", color: "#9a7200", fontSize: "10px" }}>
                    Organization ID: {selectedOrganization.id}
                  </div>
                </div>
              )}

              <label htmlFor="consultation-organization" style={labelStyle}>
                Clinic / Organization
                <span style={{ color: "#c62828", marginLeft: "3px" }}>*</span>
              </label>

              <select
                id="consultation-organization"
                name="organizationId"
                aria-label="Clinic / Organization"
                value={form.organizationId}
                onChange={(event) =>
                  handleChange("organizationId", event.target.value)
                }
                disabled={loadingOrganizations || submitting}
                style={inputStyle}
              >
                <option value="">
                  {loadingOrganizations
                    ? "Loading clinics..."
                    : "Select a clinic"}
                </option>

                {organizations.map((organization) => (
                  <option key={organization.id} value={organization.id}>
                    {organization.name}
                    {organization.city ? ` — ${organization.city}` : ""}
                  </option>
                ))}
              </select>
            </div>

            {/* ==================================================
                DATE (MONTH CALENDAR)
                ================================================== */}

            <div style={{ marginBottom: "20px" }}>
              <div style={sectionTitleStyle}>
                <CalendarDays size={16} />
                <span>Consultation Date</span>
              </div>

              {!form.organizationId ? (
                <div className="booking-slots-hint">
                  Select a clinic first to see available dates.
                </div>
              ) : (
                <BookingCalendar
                  visibleMonth={visibleMonth}
                  days={monthDays}
                  loading={loadingMonth}
                  selectedDate={selectedDate}
                  onSelectDate={handleSelectDate}
                  onMonthChange={setVisibleMonth}
                />
              )}
            </div>

            {/* ==================================================
                TIME (FIXED ONE-HOUR SLOTS)
                ================================================== */}

            <div style={{ marginBottom: "20px" }}>
              <div style={sectionTitleStyle}>
                <Clock3 size={16} />
                <span>Time Slot (one hour)</span>
              </div>

              <SlotPicker
                slots={dayAvailability?.slots ?? null}
                selectedStart={selectedSlot}
                onSelect={handleSelectSlot}
                loading={loadingSlots}
                emptyHint={
                  !form.organizationId
                    ? "Select a clinic and a date first."
                    : !selectedDate
                      ? "Select a date to see the available time slots."
                      : "No availability information for this day."
                }
              />
            </div>

            {/* ==================================================
                SYMPTOMS
                ================================================== */}

            <div style={{ marginBottom: "20px" }}>
              <div
                style={{
                  marginBottom: "12px",
                  fontSize: "12px",
                  fontWeight: 800,
                  color: "#242424",
                }}
              >
                Symptoms & Problem
              </div>

              <label htmlFor="consultation-symptoms" style={labelStyle}>
                Symptoms
                <span style={{ color: "#c62828", marginLeft: "3px" }}>*</span>
              </label>

              <textarea
                id="consultation-symptoms"
                name="symptoms"
                value={form.symptoms}
                onChange={(event) =>
                  handleChange("symptoms", event.target.value)
                }
                disabled={submitting}
                rows={4}
                placeholder="Describe the symptoms or health problem..."
                style={{ ...textareaStyle, minHeight: "100px" }}
              />
            </div>

            {/* ==================================================
                URGENCY + BUDGET
                ================================================== */}

            <div style={{ marginBottom: "20px" }}>
              <div style={sectionTitleStyle}>
                <Clock3 size={16} />
                <span>Request Priority & Budget</span>
              </div>

              <div
                style={{
                  display: "grid",
                  gridTemplateColumns:
                    "repeat(auto-fit, minmax(min(200px, 100%), 1fr))",
                  gap: "14px",
                }}
              >
                <div>
                  <label htmlFor="consultation-urgency" style={labelStyle}>
                    Urgency
                  </label>

                  <select
                    id="consultation-urgency"
                    name="urgency"
                    value={form.urgency}
                    onChange={(event) =>
                      handleChange("urgency", event.target.value)
                    }
                    disabled={submitting}
                    style={inputStyle}
                  >
                    <option value="Low">Low</option>
                    <option value="Medium">Medium</option>
                    <option value="High">High</option>
                    <option value="Emergency">Emergency</option>
                  </select>
                </div>

                <div>
                  <label htmlFor="consultation-budget" style={labelStyle}>
                    Budget Limit
                  </label>

                  <input
                    id="consultation-budget"
                    name="budget"
                    type="number"
                    min="0"
                    step="0.01"
                    value={form.budget}
                    onChange={(event) =>
                      handleChange("budget", event.target.value)
                    }
                    disabled={submitting}
                    placeholder="e.g. 15000"
                    style={inputStyle}
                  />

                  <p
                    style={{
                      margin: "6px 0 0",
                      fontSize: "10px",
                      color: "#888888",
                    }}
                  >
                    Optional maximum budget in LKR.
                  </p>
                </div>
              </div>
            </div>

            {/* ==================================================
                ADDITIONAL NOTES
                ================================================== */}

            <div style={{ marginBottom: "18px" }}>
              <div
                style={{
                  marginBottom: "12px",
                  fontSize: "12px",
                  fontWeight: 800,
                  color: "#242424",
                }}
              >
                Additional Information
              </div>

              <label htmlFor="consultation-notes" style={labelStyle}>
                Additional Notes
              </label>

              <textarea
                id="consultation-notes"
                name="additionalNotes"
                value={form.additionalNotes}
                onChange={(event) =>
                  handleChange("additionalNotes", event.target.value)
                }
                disabled={submitting}
                rows={3}
                placeholder="Add any other useful information..."
                style={{ ...textareaStyle, minHeight: "80px" }}
              />
            </div>

            {/* ==================================================
                BACKEND INFORMATION
                ================================================== */}

            <div
              style={{
                padding: "12px 14px",
                border: "1px solid #eadf9c",
                borderRadius: "9px",
                background: "#fffbea",
                color: "#735900",
                fontSize: "11px",
                lineHeight: 1.6,
              }}
            >
              <strong>Request validation:</strong> Pet ownership will be
              verified, and the chosen one-hour slot is re-checked by the
              server before the consultation request is created.
            </div>
          </div>

          {/* ====================================================
              FOOTER
              ==================================================== */}

          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "flex-end",
              gap: "10px",
              padding: "14px 24px",
              borderTop: "1px solid #e8e8e2",
              background: "#fafaf7",
              flexShrink: 0,
            }}
          >
            {/* Cancel */}

            <button
              type="button"
              onClick={handleClose}
              disabled={submitting}
              style={{
                height: "38px",
                padding: "0 18px",
                border: "1px solid #d5d5cf",
                borderRadius: "8px",
                background: "#ffffff",
                color: "#444444",
                fontSize: "11px",
                fontWeight: 700,
                cursor: submitting ? "not-allowed" : "pointer",
                opacity: submitting ? 0.5 : 1,
              }}
            >
              Cancel
            </button>

            {/* Create */}

            <button
              type="submit"
              disabled={submitting || loadingPets || !canSubmit}
              style={{
                height: "38px",
                padding: "0 20px",
                border: "1px solid #d6aa00",
                borderRadius: "8px",
                background: "#f5c400",
                color: "#171717",
                fontSize: "11px",
                fontWeight: 800,
                cursor:
                  submitting || loadingPets || !canSubmit
                    ? "not-allowed"
                    : "pointer",
                opacity: submitting || loadingPets || !canSubmit ? 0.55 : 1,
              }}
            >
              {submitting ? "Creating..." : "Create Consultation"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );

  /* ============================================================
     RENDER THROUGH BODY PORTAL

     This prevents the modal from being affected by the page
     layout, overflow, stacking context, or sidebar.
     ============================================================ */

  return createPortal(modalContent, document.body);
}

export default NewConsultationModal;
