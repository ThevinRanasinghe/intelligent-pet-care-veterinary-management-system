import { useEffect, useMemo, useState } from "react";
import {
  Search,
  Plus,
  PawPrint,
  CalendarDays,
  Clock3,
  MapPin,
  ChevronRight,
  SlidersHorizontal,
  X,
} from "lucide-react";

import { NewConsultationModal } from "./NewConsultationModal";

import {
  consultationService,
  petService,
  ownerService,
  ConsultationRequestApi,
  Pet,
  PetOwner,
} from "../../services/api";

/* ========================================================================== */
/* Types                                                                      */
/* ========================================================================== */

type ConsultationStatus =
  | "Pending"
  | "Draft"
  | "Submitted"
  | "Processing"
  | "Approved"
  | "Rejected"
  | "Cancelled";

type Urgency = "Low" | "Medium" | "High" | "Emergency";

type ConsultationRequest = {
  id: string;
  petId: string;
  ownerId: string;
  petName: string;
  species: string;
  breed: string;
  ownerName: string;
  symptoms: string;
  urgency: Urgency;
  preferredDate: string;
  preferredTime: string;
  clinic: string;
  status: ConsultationStatus;
};

/* ========================================================================== */
/* Helper Functions                                                           */
/* ========================================================================== */

function formatTime(time?: string | null): string {
  if (!time) return "—";

  const [hoursString, minutes] = time.split(":");
  const hours = Number(hoursString);

  if (Number.isNaN(hours)) {
    return time;
  }

  const suffix = hours >= 12 ? "PM" : "AM";
  const displayHour = hours % 12 || 12;

  return `${displayHour}:${minutes ?? "00"} ${suffix}`;
}

function formatDate(date?: string | null): string {
  if (!date) return "—";

  const parsedDate = new Date(date.includes("T") ? date : `${date}T00:00:00`);

  if (Number.isNaN(parsedDate.getTime())) {
    return date;
  }

  return parsedDate.toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

function formatDateTime(date?: string | null): string {
  if (!date) return "—";

  const parsedDate = new Date(date);

  if (Number.isNaN(parsedDate.getTime())) {
    return date;
  }

  return parsedDate.toLocaleString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

function getStatusClass(status: string): string {
  switch (status) {
    case "Approved":
      return "badge badge-success";

    case "Processing":
    case "Submitted":
      return "badge badge-info";

    case "Rejected":
      return "badge badge-danger";

    case "Cancelled":
      return "badge badge-neutral";

    case "Draft":
    case "Pending":
    default:
      return "badge badge-warning";
  }
}

function getUrgencyClass(urgency: string): string {
  switch (urgency) {
    case "Emergency":
      return "consultation-urgency emergency";

    case "High":
      return "consultation-urgency high";

    case "Medium":
      return "consultation-urgency medium";

    case "Low":
    default:
      return "consultation-urgency low";
  }
}

/* ========================================================================== */
/* Component                                                                  */
/* ========================================================================== */

export function ConsultationRequestsPage() {
  const [search, setSearch] = useState("");

  const [statusFilter, setStatusFilter] = useState<string>("All");

  /* ------------------------------------------------------------------------ */
  /* New Consultation Modal                                                   */
  /* ------------------------------------------------------------------------ */

  const [isNewConsultationOpen, setIsNewConsultationOpen] = useState(false);

  /* ------------------------------------------------------------------------ */
  /* Consultation List                                                        */
  /* ------------------------------------------------------------------------ */

  const [requests, setRequests] = useState<ConsultationRequest[]>([]);

  const [successNotice, setSuccessNotice] = useState("");

  const [errorNotice, setErrorNotice] = useState("");

  const [isLoading, setIsLoading] = useState(true);

  /* ------------------------------------------------------------------------ */
  /* View Details Modal                                                       */
  /* ------------------------------------------------------------------------ */

  const [selectedConsultation, setSelectedConsultation] =
    useState<ConsultationRequestApi | null>(null);

  const [isDetailsOpen, setIsDetailsOpen] = useState(false);

  const [isLoadingDetails, setIsLoadingDetails] = useState(false);

  const [detailsError, setDetailsError] = useState("");

  /* ------------------------------------------------------------------------ */
  /* Submit Consultation                                                      */
  /* ------------------------------------------------------------------------ */

  const [isSubmittingConsultation, setIsSubmittingConsultation] =
    useState(false);

  const [submitError, setSubmitError] = useState("");

  /* ======================================================================== */
  /* Load Consultations                                                       */
  /* ======================================================================== */

  const loadConsultations = async () => {
    try {
      setIsLoading(true);
      setErrorNotice("");

      const [apiConsultations, petsData, ownersData] = await Promise.all([
        consultationService.getAllConsultations(),

        petService.getAllPets().catch(() => [] as Pet[]),

        ownerService.getAllOwners().catch(() => [] as PetOwner[]),
      ]);

      if (!Array.isArray(apiConsultations)) {
        setRequests([]);
        return;
      }

      /* -------------------------------------------------------------------- */
      /* Pet Lookup                                                            */
      /* -------------------------------------------------------------------- */

      const petMap = new Map<string, Pet>();

      petsData.forEach((pet) => {
        petMap.set(pet.id, pet);
      });

      /* -------------------------------------------------------------------- */
      /* Owner Lookup                                                          */
      /* -------------------------------------------------------------------- */

      const ownerMap = new Map<string, PetOwner>();

      ownersData.forEach((owner) => {
        ownerMap.set(owner.id, owner);
      });

      /* -------------------------------------------------------------------- */
      /* Convert API data                                                      */
      /* -------------------------------------------------------------------- */

      const mapped: ConsultationRequest[] = apiConsultations.map((item) => {
        const pet = petMap.get(item.petId);

        const owner = ownerMap.get(item.ownerId);

        return {
          id: item.id,

          petId: item.petId,

          ownerId: item.ownerId,

          petName: item.petName || pet?.name || "Registered Pet",

          species: pet?.species || "Pet",

          breed: pet?.breed || "Standard",

          ownerName: owner?.fullName || `Owner ${item.ownerId}`,

          symptoms: item.symptoms || "No symptoms provided",

          urgency: (item.urgency as Urgency) || "Medium",

          preferredDate: item.preferredDate
            ? item.preferredDate.substring(0, 10)
            : "",

          preferredTime: formatTime(item.preferredTime),

          clinic: "Happy Paws Veterinary Hospital",

          status: (item.status as ConsultationStatus) || "Draft",
        };
      });

      setRequests(mapped);
    } catch (error) {
      console.error("Error loading consultations from backend:", error);

      setRequests([]);

      setErrorNotice("Unable to load consultation requests from the backend.");
    } finally {
      setIsLoading(false);
    }
  };

  /* ======================================================================== */
  /* Initial Load                                                             */
  /* ======================================================================== */

  useEffect(() => {
    loadConsultations();
  }, []);

  /* ======================================================================== */
  /* Search + Filter                                                          */
  /* ======================================================================== */

  const filteredRequests = useMemo(() => {
    const searchValue = search.trim().toLowerCase();

    return requests.filter((request) => {
      const matchesSearch =
        !searchValue ||
        request.id.toLowerCase().includes(searchValue) ||
        request.petName.toLowerCase().includes(searchValue) ||
        request.ownerName.toLowerCase().includes(searchValue) ||
        request.species.toLowerCase().includes(searchValue) ||
        request.breed.toLowerCase().includes(searchValue) ||
        request.symptoms.toLowerCase().includes(searchValue);

      let matchesStatus = true;

      if (statusFilter === "Pending") {
        matchesStatus =
          request.status === "Pending" || request.status === "Draft";
      } else if (statusFilter === "Submitted") {
        matchesStatus =
          request.status === "Submitted" || request.status === "Processing";
      } else if (statusFilter !== "All") {
        matchesStatus = request.status === statusFilter;
      }

      return matchesSearch && matchesStatus;
    });
  }, [requests, search, statusFilter]);

  /* ======================================================================== */
  /* Statistics                                                               */
  /* ======================================================================== */

  const totalRequests = requests.length;

  const pendingRequests = requests.filter(
    (request) => request.status === "Pending" || request.status === "Draft",
  ).length;

  const processingRequests = requests.filter(
    (request) =>
      request.status === "Processing" || request.status === "Submitted",
  ).length;

  const approvedRequests = requests.filter(
    (request) => request.status === "Approved",
  ).length;

  /* ======================================================================== */
  /* New Consultation Success                                                 */
  /* ======================================================================== */

  const handleNewConsultationSuccess = async (
    newApiConsultation: ConsultationRequestApi,
  ) => {
    setIsNewConsultationOpen(false);

    setErrorNotice("");

    setSuccessNotice(
      `Consultation request created successfully. Backend ID: ${newApiConsultation.id}`,
    );

    await loadConsultations();

    window.setTimeout(() => {
      setSuccessNotice("");
    }, 4000);
  };

  /* ======================================================================== */
  /* VIEW DETAILS                                                             */
  /* ======================================================================== */

  const handleViewDetails = async (consultationId: string) => {
    try {
      setDetailsError("");
      setSubmitError("");

      setSelectedConsultation(null);

      setIsDetailsOpen(true);

      setIsLoadingDetails(true);

      const details =
        await consultationService.getConsultationById(consultationId);

      setSelectedConsultation(details);
    } catch (error) {
      console.error("Error loading consultation details:", error);

      setDetailsError(
        error instanceof Error
          ? error.message
          : "Unable to load consultation details.",
      );
    } finally {
      setIsLoadingDetails(false);
    }
  };

  /* ======================================================================== */
  /* SUBMIT CONSULTATION                                                      */
  /* ======================================================================== */

  const handleSubmitConsultation = async () => {
    if (!selectedConsultation) {
      return;
    }

    if (selectedConsultation.status !== "Draft") {
      return;
    }

    try {
      setIsSubmittingConsultation(true);

      setSubmitError("");

      setDetailsError("");

      const updatedConsultation = await consultationService.submitConsultation(
        selectedConsultation.id,
      );

      /* -------------------------------------------------------------- */
      /* Update currently opened details                                 */
      /* -------------------------------------------------------------- */

      setSelectedConsultation(updatedConsultation);

      /* -------------------------------------------------------------- */
      /* Refresh consultation list                                      */
      /* -------------------------------------------------------------- */

      await loadConsultations();

      /* -------------------------------------------------------------- */
      /* Success message                                                 */
      /* -------------------------------------------------------------- */

      setSuccessNotice(
        `Consultation ${updatedConsultation.id} submitted successfully.`,
      );

      window.setTimeout(() => {
        setSuccessNotice("");
      }, 4000);
    } catch (error) {
      console.error("Error submitting consultation:", error);

      setSubmitError(
        error instanceof Error
          ? error.message
          : "Unable to submit the consultation request.",
      );
    } finally {
      setIsSubmittingConsultation(false);
    }
  };

  /* ======================================================================== */
  /* CLOSE DETAILS                                                            */
  /* ======================================================================== */

  const handleCloseDetails = () => {
    if (isLoadingDetails || isSubmittingConsultation) {
      return;
    }

    setIsDetailsOpen(false);

    setSelectedConsultation(null);

    setDetailsError("");

    setSubmitError("");
  };

  /* ======================================================================== */
  /* Render                                                                   */
  /* ======================================================================== */

  return (
    <div className="page-wrap consultation-page">
      {/* ==================================================================== */}
      {/* PAGE HEADER                                                           */}
      {/* ==================================================================== */}

      <div className="page-heading consultation-heading">
        <div>
          <div className="eyebrow">PET CARE MANAGEMENT</div>

          <h2>Consultation Requests</h2>

          <p>
            Manage pet consultation requests and track their progress in
            real-time.
          </p>
        </div>

        {/* ------------------------------------------------------------------ */}
        {/* NEW CONSULTATION BUTTON                                             */}
        {/* ------------------------------------------------------------------ */}

        <div className="heading-actions">
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              setSuccessNotice("");
              setErrorNotice("");

              setIsNewConsultationOpen(true);
            }}
          >
            <Plus size={16} />
            New Consultation
          </button>
        </div>
      </div>

      {/* ==================================================================== */}
      {/* SUCCESS NOTICE                                                        */}
      {/* ==================================================================== */}

      {successNotice && <div className="notice">{successNotice}</div>}

      {/* ==================================================================== */}
      {/* ERROR NOTICE                                                          */}
      {/* ==================================================================== */}

      {errorNotice && <div className="notice">{errorNotice}</div>}

      {/* ==================================================================== */}
      {/* STATISTICS                                                            */}
      {/* ==================================================================== */}

      <div className="stat-grid consultation-stats">
        {/* Total */}

        <div className="card stat-card">
          <div className="stat-top">
            <span>Total Requests</span>

            <div className="stat-icon">
              <PawPrint size={18} />
            </div>
          </div>

          <strong>{totalRequests}</strong>
        </div>

        {/* Pending */}

        <div className="card stat-card">
          <div className="stat-top">
            <span>Pending</span>

            <div className="stat-icon consultation-warning-icon">
              <Clock3 size={18} />
            </div>
          </div>

          <strong>{pendingRequests}</strong>
        </div>

        {/* Processing */}

        <div className="card stat-card">
          <div className="stat-top">
            <span>Processing</span>

            <div className="stat-icon">
              <CalendarDays size={18} />
            </div>
          </div>

          <strong>{processingRequests}</strong>
        </div>

        {/* Approved */}

        <div className="card stat-card">
          <div className="stat-top">
            <span>Approved</span>

            <div className="stat-icon">
              <PawPrint size={18} />
            </div>
          </div>

          <strong>{approvedRequests}</strong>
        </div>
      </div>

      {/* ==================================================================== */}
      {/* MAIN CONSULTATION PANEL                                               */}
      {/* ==================================================================== */}

      <div className="card consultation-panel">
        {/* ------------------------------------------------------------------ */}
        {/* FILTERS                                                             */}
        {/* ------------------------------------------------------------------ */}

        <div className="filter-bar consultation-filter-bar">
          {/* Search */}

          <div className="search-input">
            <Search size={16} />

            <input
              type="text"
              placeholder="Search by pet, owner, symptoms, ID..."
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>

          {/* Status */}

          <div className="select-input consultation-select">
            <SlidersHorizontal size={16} />

            <select
              value={statusFilter}
              onChange={(event) => setStatusFilter(event.target.value)}
            >
              <option value="All">All Statuses</option>

              <option value="Pending">Pending / Draft</option>

              <option value="Submitted">Submitted / Processing</option>

              <option value="Approved">Approved</option>

              <option value="Rejected">Rejected</option>

              <option value="Cancelled">Cancelled</option>
            </select>
          </div>
        </div>

        {/* ------------------------------------------------------------------ */}
        {/* REQUEST COUNT                                                       */}
        {/* ------------------------------------------------------------------ */}

        <div className="consultation-panel-header">
          <span className="eyebrow">REQUESTS</span>

          <h3>
            {filteredRequests.length} consultation
            {filteredRequests.length !== 1 ? "s" : ""}
          </h3>

          <p>Showing consultation requests matching active filters</p>
        </div>

        {/* ------------------------------------------------------------------ */}
        {/* REQUEST LIST                                                        */}
        {/* ------------------------------------------------------------------ */}

        <div className="consultation-request-list">
          {isLoading ? (
            <div className="empty-state">
              <div className="empty-state-icon">
                <PawPrint size={24} />
              </div>

              <h3>Loading consultation requests...</h3>

              <p>Please wait while we load data from the backend.</p>
            </div>
          ) : filteredRequests.length === 0 ? (
            <div className="empty-state">
              <div className="empty-state-icon">
                <PawPrint size={24} />
              </div>

              <h3>No consultation requests found</h3>

              <p>Try changing your search or status filter.</p>
            </div>
          ) : (
            filteredRequests.map((request) => (
              <div key={request.id} className="consultation-request-card">
                {/* Pet Icon */}

                <div className="consultation-pet-icon">
                  <PawPrint size={22} />
                </div>

                {/* Main Content */}

                <div className="consultation-request-main">
                  <div className="consultation-request-info">
                    <div className="consultation-title-row">
                      <div>
                        <h4>{request.petName}</h4>

                        <p>
                          {request.species}

                          {" · "}

                          {request.breed}
                        </p>
                      </div>

                      <div>
                        <span className={getUrgencyClass(request.urgency)}>
                          {request.urgency}
                        </span>

                        <span className={getStatusClass(request.status)}>
                          {request.status}
                        </span>
                      </div>
                    </div>

                    <p className="consultation-owner">
                      Owner: <strong>{request.ownerName}</strong>
                    </p>

                    <p className="consultation-symptoms">{request.symptoms}</p>

                    <div className="consultation-meta">
                      <span>
                        <CalendarDays size={13} />

                        {formatDate(request.preferredDate)}
                      </span>

                      <span>
                        <Clock3 size={13} />

                        {request.preferredTime}
                      </span>

                      <span className="consultation-clinic">
                        <MapPin size={13} />

                        {request.clinic}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Actions */}

                <div className="consultation-request-actions">
                  <span className="consultation-request-id">{request.id}</span>

                  <button
                    type="button"
                    className="btn btn-secondary consultation-view-btn"
                    onClick={() => handleViewDetails(request.id)}
                  >
                    View Details
                    <ChevronRight size={14} />
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      </div>

      {/* ==================================================================== */}
      {/* INFORMATION STRIP                                                     */}
      {/* ==================================================================== */}

      <div className="info-strip">
        <PawPrint size={18} />

        <div>
          <strong>Consultation workflow</strong>

          <span>
            New requests are validated for pet ownership before they are saved
            into PostgreSQL and move through processing and approval.
          </span>
        </div>
      </div>

      {/* ==================================================================== */}
      {/* NEW CONSULTATION MODAL                                                */}
      {/* ==================================================================== */}

      <NewConsultationModal
        isOpen={isNewConsultationOpen}
        onClose={() => {
          setIsNewConsultationOpen(false);
        }}
        onSubmit={handleNewConsultationSuccess}
      />

      {/* ==================================================================== */}
      {/* VIEW DETAILS MODAL                                                    */}
      {/* ==================================================================== */}

      {isDetailsOpen && (
        <div
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              handleCloseDetails();
            }
          }}
          style={{
            position: "fixed",
            inset: 0,
            width: "100vw",
            height: "100vh",
            zIndex: 99998,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            padding: "24px",
            background: "rgba(0, 0, 0, 0.55)",
            backdropFilter: "blur(4px)",
            WebkitBackdropFilter: "blur(4px)",
          }}
        >
          {/* ================================================================ */}
          {/* DETAILS CARD                                                     */}
          {/* ================================================================ */}

          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="consultation-details-title"
            onMouseDown={(event) => {
              event.stopPropagation();
            }}
            style={{
              width: "min(700px, 100%)",
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
            {/* ============================================================ */}
            {/* HEADER                                                        */}
            {/* ============================================================ */}

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
                    CONSULTATION DETAILS
                  </div>

                  <h2
                    id="consultation-details-title"
                    style={{
                      margin: 0,
                      fontSize: "21px",
                      lineHeight: 1.2,
                      fontWeight: 800,
                      color: "#181818",
                    }}
                  >
                    Consultation Request
                  </h2>

                  {selectedConsultation && (
                    <p
                      style={{
                        margin: "5px 0 0",
                        fontSize: "11px",
                        color: "#777777",
                      }}
                    >
                      ID: {selectedConsultation.id}
                    </p>
                  )}
                </div>
              </div>

              <button
                type="button"
                onClick={handleCloseDetails}
                disabled={isLoadingDetails || isSubmittingConsultation}
                aria-label="Close consultation details"
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
                  cursor:
                    isLoadingDetails || isSubmittingConsultation
                      ? "not-allowed"
                      : "pointer",
                  opacity:
                    isLoadingDetails || isSubmittingConsultation ? 0.5 : 1,
                }}
              >
                <X size={19} />
              </button>
            </div>

            {/* ============================================================ */}
            {/* BODY                                                          */}
            {/* ============================================================ */}

            <div
              style={{
                overflowY: "auto",
                padding: "22px 24px",
              }}
            >
              {/* Loading */}

              {isLoadingDetails && (
                <div
                  style={{
                    padding: "45px 20px",
                    textAlign: "center",
                  }}
                >
                  <PawPrint
                    size={28}
                    style={{
                      marginBottom: "10px",
                    }}
                  />

                  <h3
                    style={{
                      margin: "0 0 6px",
                      fontSize: "15px",
                    }}
                  >
                    Loading consultation details...
                  </h3>

                  <p
                    style={{
                      margin: 0,
                      fontSize: "12px",
                      color: "#777777",
                    }}
                  >
                    Please wait while we get the latest information from the
                    backend.
                  </p>
                </div>
              )}

              {/* Error */}

              {!isLoadingDetails && detailsError && (
                <div
                  style={{
                    padding: "14px",
                    border: "1px solid #f2b8b5",
                    borderRadius: "9px",
                    background: "#fff1f0",
                    color: "#b42318",
                    fontSize: "12px",
                    lineHeight: 1.5,
                  }}
                >
                  {detailsError}
                </div>
              )}

              {/* Details */}

              {!isLoadingDetails && !detailsError && selectedConsultation && (
                <div>
                  {/* ==================================================== */}
                  {/* STATUS + URGENCY                                     */}
                  {/* ==================================================== */}

                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      gap: "12px",
                      marginBottom: "20px",
                      padding: "14px",
                      border: "1px solid #e8e8e2",
                      borderRadius: "10px",
                      background: "#fafaf7",
                    }}
                  >
                    <div>
                      <div
                        style={{
                          marginBottom: "5px",
                          fontSize: "10px",
                          fontWeight: 700,
                          color: "#777777",
                        }}
                      >
                        REQUEST STATUS
                      </div>

                      <span
                        className={getStatusClass(selectedConsultation.status)}
                      >
                        {selectedConsultation.status}
                      </span>
                    </div>

                    <div>
                      <div
                        style={{
                          marginBottom: "5px",
                          fontSize: "10px",
                          fontWeight: 700,
                          color: "#777777",
                        }}
                      >
                        URGENCY
                      </div>

                      <span
                        className={getUrgencyClass(
                          selectedConsultation.urgency,
                        )}
                      >
                        {selectedConsultation.urgency}
                      </span>
                    </div>
                  </div>

                  {/* ==================================================== */}
                  {/* PET + OWNER                                            */}
                  {/* ==================================================== */}

                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns: "1fr 1fr",
                      gap: "14px",
                      marginBottom: "18px",
                    }}
                  >
                    <div
                      style={{
                        padding: "14px",
                        border: "1px solid #e8e8e2",
                        borderRadius: "10px",
                      }}
                    >
                      <div
                        style={{
                          marginBottom: "6px",
                          fontSize: "10px",
                          fontWeight: 700,
                          color: "#777777",
                        }}
                      >
                        PET ID
                      </div>

                      <strong
                        style={{
                          fontSize: "13px",
                        }}
                      >
                        {selectedConsultation.petId}
                      </strong>
                    </div>

                    <div
                      style={{
                        padding: "14px",
                        border: "1px solid #e8e8e2",
                        borderRadius: "10px",
                      }}
                    >
                      <div
                        style={{
                          marginBottom: "6px",
                          fontSize: "10px",
                          fontWeight: 700,
                          color: "#777777",
                        }}
                      >
                        OWNER ID
                      </div>

                      <strong
                        style={{
                          fontSize: "13px",
                        }}
                      >
                        {selectedConsultation.ownerId}
                      </strong>
                    </div>
                  </div>

                  {/* ==================================================== */}
                  {/* PET NAME                                               */}
                  {/* ==================================================== */}

                  {selectedConsultation.petName && (
                    <div
                      style={{
                        marginBottom: "18px",
                        padding: "14px",
                        border: "1px solid #e8e8e2",
                        borderRadius: "10px",
                      }}
                    >
                      <div
                        style={{
                          marginBottom: "6px",
                          fontSize: "10px",
                          fontWeight: 700,
                          color: "#777777",
                        }}
                      >
                        PET
                      </div>

                      <strong
                        style={{
                          fontSize: "15px",
                        }}
                      >
                        {selectedConsultation.petName}
                      </strong>
                    </div>
                  )}

                  {/* ==================================================== */}
                  {/* SYMPTOMS                                               */}
                  {/* ==================================================== */}

                  <div
                    style={{
                      marginBottom: "18px",
                    }}
                  >
                    <div
                      style={{
                        marginBottom: "7px",
                        fontSize: "11px",
                        fontWeight: 800,
                        color: "#333333",
                      }}
                    >
                      Symptoms
                    </div>

                    <div
                      style={{
                        padding: "13px 14px",
                        border: "1px solid #e8e8e2",
                        borderRadius: "9px",
                        background: "#fafaf7",
                        fontSize: "12px",
                        lineHeight: 1.6,
                        color: "#444444",
                        whiteSpace: "pre-wrap",
                      }}
                    >
                      {selectedConsultation.symptoms || "No symptoms provided."}
                    </div>
                  </div>

                  {/* ==================================================== */}
                  {/* DATE / TIME / BUDGET                                  */}
                  {/* ==================================================== */}

                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns: "1fr 1fr",
                      gap: "14px",
                      marginBottom: "18px",
                    }}
                  >
                    {/* Date */}

                    <div
                      style={{
                        padding: "14px",
                        border: "1px solid #e8e8e2",
                        borderRadius: "10px",
                      }}
                    >
                      <div
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: "6px",
                          marginBottom: "7px",
                          fontSize: "10px",
                          fontWeight: 700,
                          color: "#777777",
                        }}
                      >
                        <CalendarDays size={14} />
                        Preferred Date
                      </div>

                      <strong
                        style={{
                          fontSize: "12px",
                        }}
                      >
                        {formatDate(selectedConsultation.preferredDate)}
                      </strong>
                    </div>

                    {/* Time */}

                    <div
                      style={{
                        padding: "14px",
                        border: "1px solid #e8e8e2",
                        borderRadius: "10px",
                      }}
                    >
                      <div
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: "6px",
                          marginBottom: "7px",
                          fontSize: "10px",
                          fontWeight: 700,
                          color: "#777777",
                        }}
                      >
                        <Clock3 size={14} />
                        Preferred Time
                      </div>

                      <strong
                        style={{
                          fontSize: "12px",
                        }}
                      >
                        {formatTime(selectedConsultation.preferredTime)}
                      </strong>
                    </div>

                    {/* Budget */}

                    <div
                      style={{
                        padding: "14px",
                        border: "1px solid #e8e8e2",
                        borderRadius: "10px",
                      }}
                    >
                      <div
                        style={{
                          marginBottom: "7px",
                          fontSize: "10px",
                          fontWeight: 700,
                          color: "#777777",
                        }}
                      >
                        Budget Limit
                      </div>

                      <strong
                        style={{
                          fontSize: "12px",
                        }}
                      >
                        {selectedConsultation.budget != null
                          ? `LKR ${selectedConsultation.budget.toLocaleString()}`
                          : "Not specified"}
                      </strong>
                    </div>

                    {/* Clinic */}

                    <div
                      style={{
                        padding: "14px",
                        border: "1px solid #e8e8e2",
                        borderRadius: "10px",
                      }}
                    >
                      <div
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: "6px",
                          marginBottom: "7px",
                          fontSize: "10px",
                          fontWeight: 700,
                          color: "#777777",
                        }}
                      >
                        <MapPin size={14} />
                        Clinic
                      </div>

                      <strong
                        style={{
                          fontSize: "12px",
                        }}
                      >
                        Happy Paws Veterinary Hospital
                      </strong>
                    </div>
                  </div>

                  {/* ==================================================== */}
                  {/* ADDITIONAL NOTES                                      */}
                  {/* ==================================================== */}

                  <div
                    style={{
                      marginBottom: "18px",
                    }}
                  >
                    <div
                      style={{
                        marginBottom: "7px",
                        fontSize: "11px",
                        fontWeight: 800,
                        color: "#333333",
                      }}
                    >
                      Additional Notes
                    </div>

                    <div
                      style={{
                        padding: "13px 14px",
                        border: "1px solid #e8e8e2",
                        borderRadius: "9px",
                        background: "#fafaf7",
                        fontSize: "12px",
                        lineHeight: 1.6,
                        color: "#444444",
                        whiteSpace: "pre-wrap",
                      }}
                    >
                      {selectedConsultation.additionalNotes ||
                        "No additional notes."}
                    </div>
                  </div>

                  {/* ==================================================== */}
                  {/* CREATED / UPDATED                                     */}
                  {/* ==================================================== */}

                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns: "1fr 1fr",
                      gap: "14px",
                    }}
                  >
                    <div
                      style={{
                        padding: "12px 14px",
                        border: "1px solid #e8e8e2",
                        borderRadius: "9px",
                      }}
                    >
                      <div
                        style={{
                          marginBottom: "5px",
                          fontSize: "9px",
                          fontWeight: 700,
                          color: "#888888",
                        }}
                      >
                        CREATED
                      </div>

                      <span
                        style={{
                          fontSize: "11px",
                          color: "#444444",
                        }}
                      >
                        {formatDateTime(selectedConsultation.createdAt)}
                      </span>
                    </div>

                    <div
                      style={{
                        padding: "12px 14px",
                        border: "1px solid #e8e8e2",
                        borderRadius: "9px",
                      }}
                    >
                      <div
                        style={{
                          marginBottom: "5px",
                          fontSize: "9px",
                          fontWeight: 700,
                          color: "#888888",
                        }}
                      >
                        LAST UPDATED
                      </div>

                      <span
                        style={{
                          fontSize: "11px",
                          color: "#444444",
                        }}
                      >
                        {formatDateTime(selectedConsultation.updatedAt)}
                      </span>
                    </div>
                  </div>

                  {/* ==================================================== */}
                  {/* SUBMIT ERROR                                           */}
                  {/* ==================================================== */}

                  {submitError && (
                    <div
                      style={{
                        marginTop: "16px",
                        padding: "12px 14px",
                        border: "1px solid #f2b8b5",
                        borderRadius: "9px",
                        background: "#fff1f0",
                        color: "#b42318",
                        fontSize: "12px",
                        lineHeight: 1.5,
                      }}
                    >
                      {submitError}
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* ============================================================ */}
            {/* DETAILS FOOTER                                                */}
            {/* ============================================================ */}

            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                gap: "10px",
                padding: "14px 24px",
                borderTop: "1px solid #e8e8e2",
                background: "#fafaf7",
                flexShrink: 0,
              }}
            >
              {/* Left side */}

              <div>
                {selectedConsultation?.status === "Draft" && !submitError && (
                  <span
                    style={{
                      fontSize: "10px",
                      color: "#777777",
                    }}
                  >
                    Review the request before submitting.
                  </span>
                )}
              </div>

              {/* Right side */}

              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "8px",
                }}
              >
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={handleCloseDetails}
                  disabled={isLoadingDetails || isSubmittingConsultation}
                >
                  Close
                </button>

                {/* ====================================================== */}
                {/* SUBMIT REQUEST BUTTON                                   */}
                {/* ====================================================== */}

                {selectedConsultation?.status === "Draft" && (
                  <button
                    type="button"
                    onClick={handleSubmitConsultation}
                    disabled={isSubmittingConsultation}
                    style={{
                      minWidth: "135px",
                      height: "38px",
                      padding: "0 16px",
                      border: "none",
                      borderRadius: "8px",
                      background: "#f5c400",
                      color: "#171717",
                      fontSize: "12px",
                      fontWeight: 700,
                      cursor: isSubmittingConsultation
                        ? "not-allowed"
                        : "pointer",
                      opacity: isSubmittingConsultation ? 0.65 : 1,
                    }}
                  >
                    {isSubmittingConsultation
                      ? "Submitting..."
                      : "Submit Request"}
                  </button>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default ConsultationRequestsPage;
