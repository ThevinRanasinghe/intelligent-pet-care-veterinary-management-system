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

  const [isNewConsultationOpen, setIsNewConsultationOpen] = useState(false);

  const [requests, setRequests] = useState<ConsultationRequest[]>([]);

  const [successNotice, setSuccessNotice] = useState("");
  const [errorNotice, setErrorNotice] = useState("");

  const [isLoading, setIsLoading] = useState(true);

  /* ======================================================================== */
  /* Load consultations from backend                                         */
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
      /* Create lookup maps                                                    */
      /* -------------------------------------------------------------------- */

      const petMap = new Map<string, Pet>();

      petsData.forEach((pet) => {
        petMap.set(pet.id, pet);
      });

      const ownerMap = new Map<string, PetOwner>();

      ownersData.forEach((owner) => {
        ownerMap.set(owner.id, owner);
      });

      /* -------------------------------------------------------------------- */
      /* Convert API data to UI data                                           */
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
  /* Initial API Load                                                         */
  /* ======================================================================== */

  useEffect(() => {
    loadConsultations();
  }, []);

  /* ======================================================================== */
  /* Filter Requests                                                          */
  /* ======================================================================== */

  const filteredRequests = useMemo(() => {
    const searchValue = search.trim().toLowerCase();

    return requests.filter((request) => {
      /* -------------------------------------------------------------------- */
      /* Search filter                                                         */
      /* -------------------------------------------------------------------- */

      const matchesSearch =
        !searchValue ||
        request.id.toLowerCase().includes(searchValue) ||
        request.petName.toLowerCase().includes(searchValue) ||
        request.ownerName.toLowerCase().includes(searchValue) ||
        request.species.toLowerCase().includes(searchValue) ||
        request.breed.toLowerCase().includes(searchValue) ||
        request.symptoms.toLowerCase().includes(searchValue);

      /* -------------------------------------------------------------------- */
      /* Status filter                                                         */
      /* -------------------------------------------------------------------- */

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

    /* ---------------------------------------------------------------------- */
    /* Automatically hide success message after 4 seconds                    */
    /* ---------------------------------------------------------------------- */

    window.setTimeout(() => {
      setSuccessNotice("");
    }, 4000);
  };

  /* ======================================================================== */
  /* Render                                                                   */
  /* ======================================================================== */

  return (
    <div className="page-wrap consultation-page">
      {/* ================================================================== */}
      {/* Page Header                                                        */}
      {/* ================================================================== */}

      <div className="page-heading consultation-heading">
        <div>
          <div className="eyebrow">PET CARE MANAGEMENT</div>

          <h2>Consultation Requests</h2>

          <p>
            Manage pet consultation requests and track their progress in
            real-time.
          </p>
        </div>

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

      {/* ================================================================== */}
      {/* Success Notification                                               */}
      {/* ================================================================== */}

      {successNotice && <div className="notice">{successNotice}</div>}

      {/* ================================================================== */}
      {/* Error Notification                                                 */}
      {/* ================================================================== */}

      {errorNotice && <div className="notice">{errorNotice}</div>}

      {/* ================================================================== */}
      {/* Statistics                                                         */}
      {/* ================================================================== */}

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

      {/* ================================================================== */}
      {/* Main Consultation Panel                                           */}
      {/* ================================================================== */}

      <div className="card consultation-panel">
        {/* ---------------------------------------------------------------- */}
        {/* Filters                                                          */}
        {/* ---------------------------------------------------------------- */}

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

        {/* ================================================================= */}
        {/* Request Count                                                     */}
        {/* ================================================================= */}

        <div className="consultation-panel-header">
          <span className="eyebrow">REQUESTS</span>

          <h3>
            {filteredRequests.length} consultation
            {filteredRequests.length !== 1 ? "s" : ""}
          </h3>

          <p>Showing consultation requests matching active filters</p>
        </div>

        {/* ================================================================= */}
        {/* Request List                                                       */}
        {/* ================================================================= */}

        <div className="consultation-request-list">
          {/* ---------------------------------------------------------------- */}
          {/* Loading                                                           */}
          {/* ---------------------------------------------------------------- */}

          {isLoading ? (
            <div className="empty-state">
              <div className="empty-state-icon">
                <PawPrint size={24} />
              </div>

              <h3>Loading consultation requests...</h3>

              <p>Please wait while we load data from the backend.</p>
            </div>
          ) : filteredRequests.length === 0 ? (
            /* -------------------------------------------------------------- */
            /* Empty State                                                     */
            /* -------------------------------------------------------------- */

            <div className="empty-state">
              <div className="empty-state-icon">
                <PawPrint size={24} />
              </div>

              <h3>No consultation requests found</h3>

              <p>Try changing your search or status filter.</p>
            </div>
          ) : (
            /* -------------------------------------------------------------- */
            /* Requests                                                         */
            /* -------------------------------------------------------------- */

            filteredRequests.map((request) => (
              <div key={request.id} className="consultation-request-card">
                {/* ======================================================== */}
                {/* Pet Icon                                                  */}
                {/* ======================================================== */}

                <div className="consultation-pet-icon">
                  <PawPrint size={22} />
                </div>

                {/* ======================================================== */}
                {/* Main Content                                               */}
                {/* ======================================================== */}

                <div className="consultation-request-main">
                  <div className="consultation-request-info">
                    {/* ---------------------------------------------------- */}
                    {/* Title Row                                               */}
                    {/* ---------------------------------------------------- */}

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

                    {/* ---------------------------------------------------- */}
                    {/* Owner                                                   */}
                    {/* ---------------------------------------------------- */}

                    <p className="consultation-owner">
                      Owner: <strong>{request.ownerName}</strong>
                    </p>

                    {/* ---------------------------------------------------- */}
                    {/* Symptoms                                                */}
                    {/* ---------------------------------------------------- */}

                    <p className="consultation-symptoms">{request.symptoms}</p>

                    {/* ---------------------------------------------------- */}
                    {/* Metadata                                                 */}
                    {/* ---------------------------------------------------- */}

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

                {/* ======================================================== */}
                {/* Actions                                                    */}
                {/* ======================================================== */}

                <div className="consultation-request-actions">
                  <span className="consultation-request-id">{request.id}</span>

                  <button
                    type="button"
                    className="btn btn-secondary consultation-view-btn"
                    onClick={() => {
                      console.log("View consultation:", request.id);
                    }}
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

      {/* ================================================================== */}
      {/* Information Strip                                                  */}
      {/* ================================================================== */}

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

      {/* ================================================================== */}
      {/* New Consultation Modal                                            */}
      {/* ================================================================== */}

      <NewConsultationModal
        isOpen={isNewConsultationOpen}
        onClose={() => {
          setIsNewConsultationOpen(false);
        }}
        onSubmit={handleNewConsultationSuccess}
      />
    </div>
  );
}

export default ConsultationRequestsPage;
