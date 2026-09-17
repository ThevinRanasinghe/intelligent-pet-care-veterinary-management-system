import { FormEvent, useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { X, PawPrint, CalendarDays, Clock3, MapPin } from "lucide-react";

import {
  petService,
  consultationService,
  Pet,
  ConsultationRequestApi,
} from "../../services/api";

/* ============================================================
   FORM TYPE
   ============================================================ */

type NewConsultationForm = {
  petId: string;
  symptoms: string;
  urgency: string;
  preferredDate: string;
  preferredTime: string;
  budget: string;
  clinic: string;
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
  symptoms: "",
  urgency: "Medium",
  preferredDate: "",
  preferredTime: "",
  budget: "",
  clinic: "",
  additionalNotes: "",
};

/* ============================================================
   COMPONENT
   ============================================================ */

export function NewConsultationModal({
  isOpen,
  onClose,
  onSubmit,
}: NewConsultationModalProps) {
  const [form, setForm] = useState<NewConsultationForm>(initialForm);

  const [pets, setPets] = useState<Pet[]>([]);

  const [loadingPets, setLoadingPets] = useState(false);

  const [submitting, setSubmitting] = useState(false);

  const [error, setError] = useState("");

  /* ============================================================
     LOAD REGISTERED PETS
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

        const data = await petService.getAllPets();

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

    loadPets();

    return () => {
      mounted = false;
    };
  }, [isOpen]);

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
  };

  /* ============================================================
     RESET FORM
     ============================================================ */

  const resetForm = () => {
    setForm(initialForm);
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

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (submitting) {
      return;
    }

    setError("");

    /* ----------------------------------------------------------
       Validate Pet
       ---------------------------------------------------------- */

    if (!form.petId) {
      setError("Please select a registered pet.");
      return;
    }

    const selectedPet = pets.find((pet) => pet.id === form.petId);

    if (!selectedPet) {
      setError("Selected pet could not be found.");
      return;
    }

    /* ----------------------------------------------------------
       Validate Symptoms
       ---------------------------------------------------------- */

    if (!form.symptoms.trim()) {
      setError("Please describe the pet's symptoms.");
      return;
    }

    /* ----------------------------------------------------------
       Validate Date
       ---------------------------------------------------------- */

    if (!form.preferredDate) {
      setError("Please select a preferred date.");
      return;
    }

    /* ----------------------------------------------------------
       Validate Time
       ---------------------------------------------------------- */

    if (!form.preferredTime) {
      setError("Please select a preferred time.");
      return;
    }

    /* ----------------------------------------------------------
       Validate Budget
       ---------------------------------------------------------- */

    if (form.budget.trim()) {
      const budget = Number(form.budget);

      if (Number.isNaN(budget) || budget < 0) {
        setError("Budget must be a valid positive amount.");
        return;
      }
    }

    /* ----------------------------------------------------------
       Validate Owner
       ---------------------------------------------------------- */

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
         STEP 2 - COMBINE DATE + TIME
         ======================================================== */

      const dateTimeString = `${form.preferredDate}T${form.preferredTime}:00`;

      const preferredDate = new Date(dateTimeString);

      if (Number.isNaN(preferredDate.getTime())) {
        setError("Please enter a valid date and time.");
        return;
      }

      /* ========================================================
         STEP 3 - CREATE CONSULTATION
         ======================================================== */

      const createdConsultation = await consultationService.createConsultation({
        petId: selectedPet.id,

        ownerId: selectedPet.ownerId,

        symptoms: form.symptoms.trim(),

        urgency: form.urgency,

        preferredDate: preferredDate.toISOString(),

        preferredTime: form.preferredTime ? `${form.preferredTime}:00` : null,

        budget: form.budget.trim() ? Number(form.budget) : null,

        symptomPhotoUrl: null,

        latitude: null,

        longitude: null,

        additionalNotes: form.additionalNotes.trim() || null,
      });

      /* ========================================================
         STEP 4 - SEND RESULT TO PARENT
         ======================================================== */

      onSubmit(createdConsultation);

      resetForm();

      onClose();
    } catch (err) {
      console.error("Failed to create consultation:", err);

      const message =
        err instanceof Error
          ? err.message
          : "Failed to create consultation request.";

      setError(message);
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
          {/* Header left */}

          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "12px",
            }}
          >
            {/* Icon */}

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

            {/* Text */}

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
                Submit a consultation request for a registered pet.
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

            <div
              style={{
                marginBottom: "20px",
              }}
            >
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "7px",
                  marginBottom: "12px",
                  fontSize: "12px",
                  fontWeight: 800,
                  color: "#242424",
                }}
              >
                <PawPrint size={16} />
                <span>Pet Information</span>
              </div>

              <label
                htmlFor="consultation-pet"
                style={{
                  display: "block",
                  marginBottom: "6px",
                  fontSize: "11px",
                  fontWeight: 700,
                  color: "#4b4b4b",
                }}
              >
                Registered Pet
                <span
                  style={{
                    color: "#c62828",
                    marginLeft: "3px",
                  }}
                >
                  *
                </span>
              </label>

              <div
                style={{
                  position: "relative",
                }}
              >
                <PawPrint
                  size={16}
                  style={{
                    position: "absolute",
                    left: "12px",
                    top: "50%",
                    transform: "translateY(-50%)",
                    color: "#8a8a8a",
                    pointerEvents: "none",
                  }}
                />

                <select
                  id="consultation-pet"
                  name="petId"
                  value={form.petId}
                  onChange={(event) =>
                    handleChange("petId", event.target.value)
                  }
                  disabled={loadingPets || submitting}
                  style={{
                    width: "100%",
                    height: "42px",
                    padding: "0 12px 0 38px",
                    border: "1px solid #d8d8d2",
                    borderRadius: "8px",
                    background: "#ffffff",
                    color: "#222222",
                    fontSize: "12px",
                    outline: "none",
                  }}
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
              </div>

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
                SYMPTOMS
                ================================================== */}

            <div
              style={{
                marginBottom: "20px",
              }}
            >
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

              <label
                htmlFor="consultation-symptoms"
                style={{
                  display: "block",
                  marginBottom: "6px",
                  fontSize: "11px",
                  fontWeight: 700,
                  color: "#4b4b4b",
                }}
              >
                Symptoms
                <span
                  style={{
                    color: "#c62828",
                    marginLeft: "3px",
                  }}
                >
                  *
                </span>
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
                style={{
                  width: "100%",
                  minHeight: "100px",
                  padding: "11px 12px",
                  resize: "vertical",
                  border: "1px solid #d8d8d2",
                  borderRadius: "8px",
                  background: "#ffffff",
                  color: "#222222",
                  fontSize: "12px",
                  lineHeight: 1.5,
                  outline: "none",
                }}
              />
            </div>

            {/* ==================================================
                URGENCY
                ================================================== */}

            <div
              style={{
                marginBottom: "20px",
              }}
            >
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "7px",
                  marginBottom: "12px",
                  fontSize: "12px",
                  fontWeight: 800,
                  color: "#242424",
                }}
              >
                <Clock3 size={16} />

                <span>Request Priority</span>
              </div>

              <label
                htmlFor="consultation-urgency"
                style={{
                  display: "block",
                  marginBottom: "6px",
                  fontSize: "11px",
                  fontWeight: 700,
                  color: "#4b4b4b",
                }}
              >
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
                style={{
                  width: "100%",
                  height: "42px",
                  padding: "0 12px",
                  border: "1px solid #d8d8d2",
                  borderRadius: "8px",
                  background: "#ffffff",
                  color: "#222222",
                  fontSize: "12px",
                  outline: "none",
                }}
              >
                <option value="Low">Low</option>

                <option value="Medium">Medium</option>

                <option value="High">High</option>

                <option value="Emergency">Emergency</option>
              </select>
            </div>

            {/* ==================================================
                DATE + TIME
                ================================================== */}

            <div
              style={{
                marginBottom: "20px",
              }}
            >
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "7px",
                  marginBottom: "12px",
                  fontSize: "12px",
                  fontWeight: 800,
                  color: "#242424",
                }}
              >
                <CalendarDays size={16} />

                <span>Preferred Schedule</span>
              </div>

              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "1fr 1fr",
                  gap: "14px",
                }}
              >
                {/* DATE */}

                <div>
                  <label
                    htmlFor="consultation-date"
                    style={{
                      display: "block",
                      marginBottom: "6px",
                      fontSize: "11px",
                      fontWeight: 700,
                      color: "#4b4b4b",
                    }}
                  >
                    Preferred Date
                    <span
                      style={{
                        color: "#c62828",
                        marginLeft: "3px",
                      }}
                    >
                      *
                    </span>
                  </label>

                  <div
                    style={{
                      position: "relative",
                    }}
                  >
                    <CalendarDays
                      size={16}
                      style={{
                        position: "absolute",
                        left: "12px",
                        top: "50%",
                        transform: "translateY(-50%)",
                        color: "#8a8a8a",
                        pointerEvents: "none",
                      }}
                    />

                    <input
                      id="consultation-date"
                      name="preferredDate"
                      type="date"
                      value={form.preferredDate}
                      onChange={(event) =>
                        handleChange("preferredDate", event.target.value)
                      }
                      disabled={submitting}
                      min={new Date().toISOString().split("T")[0]}
                      style={{
                        width: "100%",
                        height: "42px",
                        padding: "0 10px 0 38px",
                        border: "1px solid #d8d8d2",
                        borderRadius: "8px",
                        background: "#ffffff",
                        color: "#222222",
                        fontSize: "12px",
                        outline: "none",
                      }}
                    />
                  </div>
                </div>

                {/* TIME */}

                <div>
                  <label
                    htmlFor="consultation-time"
                    style={{
                      display: "block",
                      marginBottom: "6px",
                      fontSize: "11px",
                      fontWeight: 700,
                      color: "#4b4b4b",
                    }}
                  >
                    Preferred Time
                    <span
                      style={{
                        color: "#c62828",
                        marginLeft: "3px",
                      }}
                    >
                      *
                    </span>
                  </label>

                  <div
                    style={{
                      position: "relative",
                    }}
                  >
                    <Clock3
                      size={16}
                      style={{
                        position: "absolute",
                        left: "12px",
                        top: "50%",
                        transform: "translateY(-50%)",
                        color: "#8a8a8a",
                        pointerEvents: "none",
                      }}
                    />

                    <input
                      id="consultation-time"
                      name="preferredTime"
                      type="time"
                      value={form.preferredTime}
                      onChange={(event) =>
                        handleChange("preferredTime", event.target.value)
                      }
                      disabled={submitting}
                      style={{
                        width: "100%",
                        height: "42px",
                        padding: "0 10px 0 38px",
                        border: "1px solid #d8d8d2",
                        borderRadius: "8px",
                        background: "#ffffff",
                        color: "#222222",
                        fontSize: "12px",
                        outline: "none",
                      }}
                    />
                  </div>
                </div>
              </div>
            </div>

            {/* ==================================================
                BUDGET + CLINIC
                ================================================== */}

            <div
              style={{
                marginBottom: "20px",
              }}
            >
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "7px",
                  marginBottom: "12px",
                  fontSize: "12px",
                  fontWeight: 800,
                  color: "#242424",
                }}
              >
                <MapPin size={16} />

                <span>Clinic & Budget</span>
              </div>

              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "1fr 1fr",
                  gap: "14px",
                }}
              >
                {/* BUDGET */}

                <div>
                  <label
                    htmlFor="consultation-budget"
                    style={{
                      display: "block",
                      marginBottom: "6px",
                      fontSize: "11px",
                      fontWeight: 700,
                      color: "#4b4b4b",
                    }}
                  >
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
                    style={{
                      width: "100%",
                      height: "42px",
                      padding: "0 12px",
                      border: "1px solid #d8d8d2",
                      borderRadius: "8px",
                      background: "#ffffff",
                      color: "#222222",
                      fontSize: "12px",
                      outline: "none",
                    }}
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

                {/* CLINIC */}

                <div>
                  <label
                    htmlFor="consultation-clinic"
                    style={{
                      display: "block",
                      marginBottom: "6px",
                      fontSize: "11px",
                      fontWeight: 700,
                      color: "#4b4b4b",
                    }}
                  >
                    Preferred Clinic
                  </label>

                  <div
                    style={{
                      position: "relative",
                    }}
                  >
                    <MapPin
                      size={16}
                      style={{
                        position: "absolute",
                        left: "12px",
                        top: "50%",
                        transform: "translateY(-50%)",
                        color: "#8a8a8a",
                        pointerEvents: "none",
                      }}
                    />

                    <input
                      id="consultation-clinic"
                      name="clinic"
                      type="text"
                      value={form.clinic}
                      onChange={(event) =>
                        handleChange("clinic", event.target.value)
                      }
                      disabled={submitting}
                      placeholder="Enter preferred clinic"
                      style={{
                        width: "100%",
                        height: "42px",
                        padding: "0 12px 0 38px",
                        border: "1px solid #d8d8d2",
                        borderRadius: "8px",
                        background: "#ffffff",
                        color: "#222222",
                        fontSize: "12px",
                        outline: "none",
                      }}
                    />
                  </div>

                  <p
                    style={{
                      margin: "6px 0 0",
                      fontSize: "10px",
                      color: "#888888",
                    }}
                  >
                    Clinic preference is currently kept in the consultation
                    form.
                  </p>
                </div>
              </div>
            </div>

            {/* ==================================================
                ADDITIONAL NOTES
                ================================================== */}

            <div
              style={{
                marginBottom: "18px",
              }}
            >
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

              <label
                htmlFor="consultation-notes"
                style={{
                  display: "block",
                  marginBottom: "6px",
                  fontSize: "11px",
                  fontWeight: 700,
                  color: "#4b4b4b",
                }}
              >
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
                style={{
                  width: "100%",
                  minHeight: "80px",
                  padding: "11px 12px",
                  resize: "vertical",
                  border: "1px solid #d8d8d2",
                  borderRadius: "8px",
                  background: "#ffffff",
                  color: "#222222",
                  fontSize: "12px",
                  lineHeight: 1.5,
                  outline: "none",
                }}
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
              verified before the consultation request is created.
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
              disabled={submitting || loadingPets}
              style={{
                height: "38px",
                padding: "0 20px",
                border: "1px solid #d6aa00",
                borderRadius: "8px",
                background: "#f5c400",
                color: "#171717",
                fontSize: "11px",
                fontWeight: 800,
                cursor: submitting || loadingPets ? "not-allowed" : "pointer",
                opacity: submitting || loadingPets ? 0.55 : 1,
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
