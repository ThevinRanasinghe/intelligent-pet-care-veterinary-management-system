/**
 * API boundary for ASP.NET Core backend integration.
 * Connects frontend UI to real database services.
 */

export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5131/api";

export async function apiRequest<T>(
  path: string,
  options?: RequestInit,
): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(options?.headers ?? {}),
    },
  });

  if (!response.ok) {
    let errorMsg = `API request failed: ${response.status}`;

    try {
      const errData = await response.json();

      if (errData && errData.message) {
        errorMsg = errData.message;
      }
    } catch {
      // Use fallback error message
    }

    throw new Error(errorMsg);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

/* ========================================================================== */
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
