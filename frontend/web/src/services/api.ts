/**
 * API boundary for the ASP.NET Core backend.
 * The base URL is configured through VITE_API_BASE_URL (see .env).
 */
import { clearStoredAuth, getStoredAuth } from '../utils/authStorage';
import type { AppointmentResponse } from './schedulingService';

export const API_BASE_URL =
  ((import.meta as unknown as { env: Record<string, string> }).env.VITE_API_BASE_URL) ??
  'http://localhost:5080/api';

export class ApiError extends Error {
  status: number;
  body: unknown;

  constructor(status: number, body: unknown, message: string) {
    super(message);
    this.status = status;
    this.body = body;
    this.name = 'ApiError';
  }
}

export async function apiRequest<T>(path: string, options?: RequestInit): Promise<T> {
  const token = getStoredAuth()?.token;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(options?.headers ?? {}),
    },
  });

  if (!response.ok) {
    let body: unknown;
    const text = await response.text();
    try {
      body = text ? JSON.parse(text) : undefined;
    } catch {
      body = text;
    }

    // The session is no longer valid (expired/invalid token): clear it so
    // the next protected-route check redirects to /login. 403 is left
    // alone since it means "authenticated but not permitted", not "please
    // log in again".
    if (response.status === 401) {
      clearStoredAuth();
    }

    throw new ApiError(response.status, body, `API request failed: ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as unknown as T;
  }

  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

/* Domain Types                                                               */
/* ========================================================================== */

export type PetOwner = {
  id: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  address?: string | null;
};

export type CreatePetOwnerPayload = {
  fullName: string;
  email: string;
  phoneNumber: string;
  address?: string | null;
};

export type Pet = {
  id: string;
  ownerId: string;
  name: string;
  species: string;
  breed?: string | null;
  gender?: string | null;
  dateOfBirth?: string | null;
  weight?: number | null;
  age?: number | null;
  photoUrl?: string | null;
  notes?: string | null;
  /** Archived pets are out of the active list but keep all history. */
  isArchived?: boolean;
};

export type CreatePetPayload = {
  ownerId: string;
  name: string;
  species: string;
  breed?: string | null;
  gender?: string | null;
  dateOfBirth?: string | null;
  weight?: number | null;
  photoUrl?: string | null;
  notes?: string | null;
};

export type UpdatePetPayload = Partial<CreatePetPayload>;

/* ========================================================================== */
/* Consultation Types                                                         */
/* ========================================================================== */

export type ConsultationRequestApi = {
  id: string;
  petId: string;
  ownerId: string;
  petName?: string | null;
  symptoms: string;
  symptomPhotoUrl?: string | null;
  urgency: string;
  preferredDate?: string | null;
  /** One-hour slot start "HH:mm:ss" (derived from the booked slot). */
  preferredTime?: string | null;
  /** The clinic the consultation is booked at (null for legacy rows). */
  organizationId?: string | null;
  organizationName?: string | null;
  budget?: number | null;
  latitude?: number | null;
  longitude?: number | null;
  additionalNotes?: string | null;
  status: string;
  /** "Initial" (owner-filed) | "FollowUp" (veterinarian-requested). */
  requestType?: string;
  requestedByVeterinarianId?: string | null;
  requestedByVeterinarianName?: string | null;
  createdAt: string;
  updatedAt: string;
};

/** Advisory AI triage of a consultation request (manager-facing). */
export type ConsultationAnalysisApi = {
  /** "agentic-ai" | "unavailable" — the safe placeholder carries no assessment. */
  source: string;
  consultationRequestId: string;
  priority: string;
  consultationType: string;
  keyConcerns: { concern: string; reason: string }[];
  recommendedChecks: string[];
  suggestedNextStep: string;
  disclaimer: string;
};

/** Advisory AI scheduling/quotation proposal (manager-facing). */
export type SchedulingPlanApi = {
  /** "agentic-ai" | "unavailable" — the safe placeholder carries no proposal. */
  source: string;
  requestId: string;
  recommendedAppointment: {
    appointmentSlotId: string;
    veterinarianId: string;
    date: string;
    startTime: string;
    endTime: string;
    branch: string;
    reason: string;
  } | null;
  alternativeSlots: {
    appointmentSlotId: string;
    veterinarianId: string;
    date: string;
    startTime: string;
    endTime: string;
    branch: string;
    reason: string;
  }[];
  quotationProposal: {
    budget: number;
    items: {
      category: string;
      description: string;
      quantity: number;
      unitPrice: number;
      reason: string;
    }[];
    estimatedSubtotal: number;
    estimatedTotal: number;
    withinBudget: boolean;
  } | null;
  validationSummary: {
    slotFound: boolean;
    veterinarianAvailable: boolean;
    noKnownConflict: boolean;
    withinRequestedTime: boolean;
    withinBudget: boolean;
  };
  confidence: string;
  planningNotes: string;
  disclaimer: string;
};

export type AssignVeterinarianPayload = {
  veterinarianId: string;
  /** YYYY-MM-DD */
  date: string;
  /** Hour-aligned slot start "HH:mm" — a one-hour slot is implied. */
  startTime: string;
  /** Optional — the server computes start + 1h; when sent it must match. */
  endTime?: string | null;
  notes?: string;
};

export type CreateFollowUpPayload = {
  petId: string;
  /** Anchors the follow-up to the treating clinic (required). */
  examinationId: string;
  /** YYYY-MM-DD */
  preferredDate: string;
  /** One-hour slot start "HH:mm" — required. */
  preferredTime: string;
  reason: string;
  notes?: string;
};

/* ========================================================================== */
/* Consultation Status History Type                                           */
/* ========================================================================== */

export type ConsultationStatusHistoryApi = {
  id: number;
  consultationRequestId: string;
  status: string;
  comments?: string | null;
  changedAt: string;
};

/* ========================================================================== */
/* Nearest Clinic Type                                                        */
/* ========================================================================== */

export type NearestClinicApi = {
  id: string;
  name: string;
  address: string;
  latitude: number;
  longitude: number;
  distanceKm: number;
};

export type CreateConsultationPayload = {
  petId: string;
  ownerId: string;
  /** The clinic the owner is booking at (required). */
  organizationId: string;
  symptoms: string;
  symptomPhotoUrl?: string | null;
  urgency?: string;
  /** Booking date "YYYY-MM-DD" — required. */
  preferredDate: string;
  /** One-hour slot start "HH:mm:ss" — required; end is always start+1h. */
  preferredTime: string;
  budget?: number | null;
  latitude?: number | null;
  longitude?: number | null;
  additionalNotes?: string | null;
};

export type UpdateConsultationPayload = {
  symptoms: string;
  symptomPhotoUrl?: string | null;
  preferredDate?: string | null;
  preferredTime?: string | null;
  budget?: number | null;
  latitude?: number | null;
  longitude?: number | null;
  additionalNotes?: string | null;
};

export type OwnershipValidationResponse = {
  isValid: boolean;
  message: string;
};

/* ========================================================================== */
/* Owner API                                                                  */
/* ========================================================================== */

export const ownerService = {
  getAllOwners: () => apiRequest<PetOwner[]>("/owners"),

  getOwnerById: (id: string) =>
    apiRequest<PetOwner>(`/owners/${encodeURIComponent(id)}`),

  createOwner: (payload: CreatePetOwnerPayload) =>
    apiRequest<PetOwner>("/owners", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
};

/* ========================================================================== */
/* Pet API                                                                    */
/* ========================================================================== */

export const petService = {
  getAllPets: (includeArchived = false) =>
    apiRequest<Pet[]>(
      `/pets${includeArchived ? "?includeArchived=true" : ""}`,
    ),

  getPetById: (id: string) =>
    apiRequest<Pet>(`/pets/${encodeURIComponent(id)}`),

  getPetsByOwner: (ownerId: string, includeArchived = false) =>
    apiRequest<Pet[]>(
      `/pets/owner/${encodeURIComponent(ownerId)}${
        includeArchived ? "?includeArchived=true" : ""
      }`,
    ),

  createPet: (payload: CreatePetPayload) =>
    apiRequest<Pet>("/pets", {
      method: "POST",
      body: JSON.stringify(payload),
    }),

  updatePet: (id: string, payload: UpdatePetPayload) =>
    apiRequest<Pet>(`/pets/${encodeURIComponent(id)}`, {
      method: "PUT",
      body: JSON.stringify(payload),
    }),

  /** Removes the pet from the active list; all history is preserved. */
  archivePet: (id: string) =>
    apiRequest<Pet>(`/pets/${encodeURIComponent(id)}/archive`, {
      method: "POST",
    }),

  /** Returns an archived pet to the active list. */
  restorePet: (id: string) =>
    apiRequest<Pet>(`/pets/${encodeURIComponent(id)}/restore`, {
      method: "POST",
    }),

  deletePet: (id: string) =>
    apiRequest<{ message: string }>(`/pets/${encodeURIComponent(id)}`, {
      method: "DELETE",
    }),
};

/* ========================================================================== */
/* Consultation API                                                           */
/* ========================================================================== */

export const consultationService = {
  /**
   * Get all consultation requests
   * GET /api/consultations
   */
  getAllConsultations: () =>
    apiRequest<ConsultationRequestApi[]>("/consultations"),

  /**
   * Get one consultation request
   * GET /api/consultations/{id}
   */
  getConsultationById: (id: string) =>
    apiRequest<ConsultationRequestApi>(
      `/consultations/${encodeURIComponent(id)}`,
    ),

  /**
   * Get consultation requests belonging to one owner
   * GET /api/consultations/owner/{ownerId}
   */
  getConsultationsByOwner: (ownerId: string) =>
    apiRequest<ConsultationRequestApi[]>(
      `/consultations/owner/${encodeURIComponent(ownerId)}`,
    ),

  /**
   * Get consultation status history
   * GET /api/consultations/{id}/history
   */
  getStatusHistory: (id: string) =>
    apiRequest<ConsultationStatusHistoryApi[]>(
      `/consultations/${encodeURIComponent(id)}/history`,
    ),

  /**
   * Find nearest clinic using GPS coordinates
   * GET /api/consultations/nearest-clinic
   */
  getNearestClinic: (latitude: number, longitude: number) =>
    apiRequest<NearestClinicApi>(
      `/consultations/nearest-clinic?latitude=${encodeURIComponent(
        latitude,
      )}&longitude=${encodeURIComponent(longitude)}`,
    ),

  /**
   * Validate that a pet belongs to the selected owner
   * GET /api/consultations/validate-ownership
   */
  validateOwnership: (petId: string, ownerId: string) =>
    apiRequest<OwnershipValidationResponse>(
      `/consultations/validate-ownership?petId=${encodeURIComponent(
        petId,
      )}&ownerId=${encodeURIComponent(ownerId)}`,
    ),

  /**
   * Create a new consultation request
   * POST /api/consultations
   */
  createConsultation: (payload: CreateConsultationPayload) =>
    apiRequest<ConsultationRequestApi>("/consultations", {
      method: "POST",
      body: JSON.stringify(payload),
    }),

  /**
   * Update a consultation request
   * PUT /api/consultations/{id}
   */
  updateConsultation: (id: string, payload: UpdateConsultationPayload) =>
    apiRequest<ConsultationRequestApi>(
      `/consultations/${encodeURIComponent(id)}`,
      {
        method: "PUT",
        body: JSON.stringify(payload),
      },
    ),

  /**
   * Submit a draft consultation request
   * POST /api/consultations/{id}/submit
   */
  submitConsultation: (id: string) =>
    apiRequest<ConsultationRequestApi>(
      `/consultations/${encodeURIComponent(id)}/submit`,
      {
        method: "POST",
      },
    ),

  /**
   * Cancel a consultation request
   * PATCH /api/consultations/{id}/cancel
   */
  cancelConsultation: (id: string) =>
    apiRequest<{ message: string }>(
      `/consultations/${encodeURIComponent(id)}/cancel`,
      {
        method: "PATCH",
      },
    ),

  /**
   * Advisory AI triage of a consultation request — informational only;
   * never changes the request. Source "unavailable" means the agent
   * could not produce an assessment.
   * GET /api/consultations/{id}/analysis (ClinicManager/Admin)
   */
  getConsultationAnalysis: (id: string) =>
    apiRequest<ConsultationAnalysisApi>(
      `/consultations/${encodeURIComponent(id)}/analysis`,
    ),

  /**
   * Advisory AI scheduling/quotation proposal — informational only;
   * never books or changes anything. Source "unavailable" means the
   * agent could not produce a plan.
   * GET /api/consultations/{id}/scheduling-plan (ClinicManager/Admin)
   */
  getSchedulingPlan: (id: string) =>
    apiRequest<SchedulingPlanApi>(
      `/consultations/${encodeURIComponent(id)}/scheduling-plan`,
    ),

  /**
   * Assign a veterinarian to a Submitted/Processing request — books a
   * Confirmed appointment in the same operation.
   * POST /api/consultations/{id}/assign (ClinicManager/Admin)
   */
  assignVeterinarian: (id: string, payload: AssignVeterinarianPayload) =>
    apiRequest<AppointmentResponse>(
      `/consultations/${encodeURIComponent(id)}/assign`,
      {
        method: "POST",
        body: JSON.stringify(payload),
      },
    ),

  /**
   * Veterinarian files a follow-up consultation request for a pet.
   * POST /api/consultations/follow-up (Veterinarian/Admin)
   */
  createFollowUp: (payload: CreateFollowUpPayload) =>
    apiRequest<ConsultationRequestApi>("/consultations/follow-up", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
};
