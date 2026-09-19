/**
 * API boundary for the ASP.NET Core backend.
 * The base URL is configured through VITE_API_BASE_URL (see .env).
 */
import { clearStoredAuth, getStoredAuth } from '../utils/authStorage';

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
  preferredTime?: string | null;
  budget?: number | null;
  latitude?: number | null;
  longitude?: number | null;
  additionalNotes?: string | null;
  status: string;
  createdAt: string;
  updatedAt: string;
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
  name: string;
  address: string;
  latitude: number;
  longitude: number;
  distanceKm: number;
};

export type CreateConsultationPayload = {
  petId: string;
  ownerId: string;
  symptoms: string;
  symptomPhotoUrl?: string | null;
  urgency?: string;
  preferredDate?: string | null;
  preferredTime?: string | null;
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
  getAllPets: () => apiRequest<Pet[]>("/pets"),

  getPetById: (id: string) =>
    apiRequest<Pet>(`/pets/${encodeURIComponent(id)}`),

  getPetsByOwner: (ownerId: string) =>
    apiRequest<Pet[]>(`/pets/owner/${encodeURIComponent(ownerId)}`),

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
};
