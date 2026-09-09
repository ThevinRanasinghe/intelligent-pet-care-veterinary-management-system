/**
 * API boundary for the ASP.NET Core backend (Component 1: Pet & Consultation Management).
 * Aligned with C# .NET 8 backend handling UC-05 through UC-17 and Short ID schema.
 */

import {
  type Pet,
  type CreatePetDto,
  type UpdatePetProfileDto,
  type PetOwner,
  type MedicalRecordDto,
  type VaccinationRecordDto,
  type PetMedicalHistoryDto,
  type CreateMedicalRecordDto,
  type CreateVaccinationRecordDto,
  type ConsultationRequest,
  type CreateConsultationRequestDto,
  type ConsultationStatusTrackingDto,
  type ConsultationHistoryItemDto,
  type UpdateConsultationStatusDto,
  type ClinicBranch,
  type ConsultationStatusString,
  ConsultationStatus,
} from "../types/domain";

export {
  type Pet,
  type CreatePetDto,
  type UpdatePetProfileDto,
  type PetOwner,
  type MedicalRecordDto,
  type VaccinationRecordDto,
  type PetMedicalHistoryDto,
  type CreateMedicalRecordDto,
  type CreateVaccinationRecordDto,
  type ConsultationRequest,
  type CreateConsultationRequestDto,
  type ConsultationStatusTrackingDto,
  type ConsultationHistoryItemDto,
  type UpdateConsultationStatusDto,
  type ClinicBranch,
  type ConsultationStatusString,
  ConsultationStatus,
};

export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5143/api";

// ----------------------------------------------------
// Short ID Format Validation & Generation
// Prefixes: PET-xxxx, OWN-xxxx, REQ-xxxx, MED-xxxx, VAC-xxxx
// ----------------------------------------------------

export function isValidShortId(value: string, prefix?: string): boolean {
  if (!value || typeof value !== "string") return false;
  const trimmed = value.trim();
  if (prefix) {
    const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const regex = new RegExp(`^${escaped}-\\d{3,}$`, "i");
    return regex.test(trimmed);
  }
  return /^(PET|OWN|REQ|MED|VAC)-\d{3,}$/i.test(trimmed);
}

export function isValidPetId(id: string): boolean {
  return isValidShortId(id, "PET");
}

export function isValidOwnerId(id: string): boolean {
  return isValidShortId(id, "OWN");
}

export function isValidRequestId(id: string): boolean {
  return isValidShortId(id, "REQ");
}

export function isValidMedicalRecordId(id: string): boolean {
  return isValidShortId(id, "MED");
}

export function isValidVaccinationRecordId(id: string): boolean {
  return isValidShortId(id, "VAC");
}

/**
 * Backward compatibility helper for legacy code/tests
 */
export function isValidGuid(value: string): boolean {
  if (!value || typeof value !== "string") return false;
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
    value.trim()
  );
}

/**
 * Generates a mock sequential Short ID (e.g. for testing / client-side previews)
 */
export function generateShortId(prefix: "PET" | "OWN" | "REQ" | "MED" | "VAC"): string {
  const min = 1000;
  const max = 9999;
  const randomNum = Math.floor(Math.random() * (max - min + 1)) + min;
  return `${prefix}-${randomNum}`;
}

export function generateGuid(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

// ----------------------------------------------------
// Demo Data & Presets (matching backend DbInitializer)
// ----------------------------------------------------

export const DEMO_OWNER_ID = "OWN-2001";

export const DEMO_OWNERS: PetOwner[] = [
  {
    id: "OWN-2001",
    fullName: "Sarah Jenkins",
    email: "sarah.jenkins@example.com",
    phoneNumber: "+94 77 123 4567",
    address: "742 Galle Road, Colombo 03",
  },
  {
    id: "OWN-2002",
    fullName: "Michael Chen",
    email: "michael.chen@example.com",
    phoneNumber: "+94 71 987 6543",
    address: "45 Peradeniya Road, Kandy",
  },
];

export const CLINIC_BRANCHES: ClinicBranch[] = [
  {
    name: "Colombo 03 Central Clinic",
    address: "128 Galle Road, Kollupitiya, Colombo 03",
    lat: 6.9044,
    lng: 79.8528,
  },
  {
    name: "Rajagiriya Pet Care",
    address: "312 Kotte Road, Rajagiriya",
    lat: 6.9088,
    lng: 79.8974,
  },
  {
    name: "Kandy Veterinary Hospital",
    address: "45 Peradeniya Road, Kandy",
    lat: 7.2906,
    lng: 80.6337,
  },
  {
    name: "Negombo Coastal Clinic",
    address: "88 Main Street, Negombo",
    lat: 7.2008,
    lng: 79.8736,
  },
  {
    name: "Galle Regional Center",
    address: "14 Wakwella Road, Galle",
    lat: 6.0535,
    lng: 80.221,
  },
];

export const DEMO_BRANCHES: string[] = CLINIC_BRANCHES.map((b) => b.name);

/**
 * Calculates Haversine distance in kilometers between two GPS coordinates
 */
export function calculateDistanceKm(
  lat1: number,
  lon1: number,
  lat2: number,
  lon2: number
): number {
  const toRad = (deg: number) => (deg * Math.PI) / 180;
  const R = 6371; // Earth radius in km
  const dLat = toRad(lat2 - lat1);
  const dLon = toRad(lon2 - lon1);
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) *
    Math.sin(dLon / 2) * Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return Math.round(R * c * 10) / 10;
}

/**
 * Finds the nearest clinic branch based on user GPS coordinates
 */
export function findNearestClinic(userLat: number, userLng: number): ClinicBranch & { distanceKm: number } {
  let nearest = CLINIC_BRANCHES[0];
  let minDistance = calculateDistanceKm(userLat, userLng, nearest.lat, nearest.lng);

  for (let i = 1; i < CLINIC_BRANCHES.length; i++) {
    const branch = CLINIC_BRANCHES[i];
    const dist = calculateDistanceKm(userLat, userLng, branch.lat, branch.lng);
    if (dist < minDistance) {
      minDistance = dist;
      nearest = branch;
    }
  }

  return {
    ...nearest,
    distanceKm: minDistance,
  };
}

// ----------------------------------------------------
// HTTP Error Parser & Fetch Wrapper
// ----------------------------------------------------

export async function parseApiError(response: Response): Promise<string> {
  try {
    const text = await response.text();
    try {
      const data = JSON.parse(text);
      if (typeof data === "string") return data;
      if (data?.message) return data.message;
      if (data?.title) {
        if (data?.errors) {
          const details = Object.entries(data.errors)
            .map(([field, msgs]) => `${field}: ${Array.isArray(msgs) ? msgs.join(", ") : msgs}`)
            .join("; ");
          return `${data.title} (${details})`;
        }
        return data.title;
      }
      return JSON.stringify(data);
    } catch {
      return text || `Request failed with HTTP status ${response.status}`;
    }
  } catch {
    return `Request failed with HTTP status ${response.status}`;
  }
}

export async function apiRequest<T>(
  path: string,
  options?: RequestInit
): Promise<T> {
  const url = `${API_BASE_URL}${path.startsWith("/") ? path : `/${path}`}`;
  let response: Response;

  try {
    response = await fetch(url, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
        ...(options?.headers ?? {}),
      },
    });
  } catch (netErr: any) {
    throw new Error(
      `Unable to connect to backend at ${API_BASE_URL}. Ensure the .NET API is running. (${netErr?.message ?? "Network error"})`
    );
  }

  if (!response.ok) {
    const errorMsg = await parseApiError(response);
    throw new Error(errorMsg);
  }

  if (response.status === 204) {
    return {} as T;
  }

  const contentType = response.headers.get("content-type");
  if (contentType && contentType.includes("application/json")) {
    return (await response.json()) as T;
  }

  return (await response.text()) as unknown as T;
}

// ----------------------------------------------------
// Pet API Service (UC-05 to UC-08)
// ----------------------------------------------------

export const petService = {
  /**
   * UC-05: Register a new pet (POST /api/Pets)
   */
  createPet: (dto: CreatePetDto): Promise<Pet> => {
    if (dto.ownerId && !isValidOwnerId(dto.ownerId) && !isValidGuid(dto.ownerId)) {
      return Promise.reject(new Error(`Invalid Owner ID: "${dto.ownerId}". Must be in Short ID format (e.g., OWN-2001).`));
    }
    return apiRequest<Pet>("/Pets", {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /**
   * UC-06: View single pet profile by ID (GET /api/Pets/{id})
   */
  getPetById: (id: string): Promise<Pet> => {
    if (!isValidPetId(id) && !isValidGuid(id)) {
      return Promise.reject(new Error(`Invalid Pet ID: "${id}". Must be in Short ID format (e.g., PET-1001).`));
    }
    return apiRequest<Pet>(`/Pets/${id}`);
  },

  /**
   * UC-06: View all pets for a specific owner (GET /api/Pets/owner/{ownerId})
   */
  getPetsByOwner: (ownerId: string): Promise<Pet[]> => {
    if (!isValidOwnerId(ownerId) && !isValidGuid(ownerId)) {
      return Promise.reject(new Error(`Invalid Owner ID: "${ownerId}". Must be in Short ID format (e.g., OWN-2001).`));
    }
    return apiRequest<Pet[]>(`/Pets/owner/${ownerId}`);
  },

  /**
   * UC-06: View all registered pets clinic-wide (GET /api/Pets)
   */
  getAllPets: (): Promise<Pet[]> => {
    return apiRequest<Pet[]>("/Pets");
  },

  /**
   * UC-06 & UC-07: Update Pet Profile (PUT /api/Pets/{id}?ownerId={ownerId})
   * Strictly updates non-clinical fields (clinical records are protected)
   */
  updatePetProfile: (id: string, dto: UpdatePetProfileDto, ownerId?: string): Promise<Pet> => {
    if (!isValidPetId(id) && !isValidGuid(id)) {
      return Promise.reject(new Error(`Invalid Pet ID: "${id}". Must be in Short ID format (e.g., PET-1001).`));
    }
    const query = ownerId ? `?ownerId=${encodeURIComponent(ownerId)}` : "";
    return apiRequest<Pet>(`/Pets/${id}${query}`, {
      method: "PUT",
      body: JSON.stringify(dto),
    });
  },

  /**
   * UC-08: View Pet Medical & Vaccination History (GET /api/Pets/{id}/medical-history)
   */
  getPetMedicalHistory: (id: string): Promise<PetMedicalHistoryDto> => {
    if (!isValidPetId(id) && !isValidGuid(id)) {
      return Promise.reject(new Error(`Invalid Pet ID: "${id}". Must be in Short ID format (e.g., PET-1001).`));
    }
    return apiRequest<PetMedicalHistoryDto>(`/Pets/${id}/medical-history`);
  },

  /**
   * Add clinical medical record (Authorized staff only) (POST /api/Pets/{id}/medical-records)
   */
  addMedicalRecord: (petId: string, dto: CreateMedicalRecordDto): Promise<MedicalRecordDto> => {
    if (!isValidPetId(petId) && !isValidGuid(petId)) {
      return Promise.reject(new Error(`Invalid Pet ID: "${petId}". Must be in Short ID format (e.g., PET-1001).`));
    }
    return apiRequest<MedicalRecordDto>(`/Pets/${petId}/medical-records`, {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /**
   * Add vaccination record (Authorized staff only) (POST /api/Pets/{id}/vaccinations)
   */
  addVaccinationRecord: (petId: string, dto: CreateVaccinationRecordDto): Promise<VaccinationRecordDto> => {
    if (!isValidPetId(petId) && !isValidGuid(petId)) {
      return Promise.reject(new Error(`Invalid Pet ID: "${petId}". Must be in Short ID format (e.g., PET-1001).`));
    }
    return apiRequest<VaccinationRecordDto>(`/Pets/${petId}/vaccinations`, {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },
};

// ----------------------------------------------------
// Consultation API Service (UC-09 to UC-17)
// ----------------------------------------------------

export const consultationService = {
  /**
   * UC-09 to UC-13: Submit Consultation Request (POST /api/Consultations)
   */
  createConsultation: (dto: CreateConsultationRequestDto): Promise<ConsultationRequest> => {
    if (!isValidOwnerId(dto.ownerId) && !isValidGuid(dto.ownerId)) {
      return Promise.reject(new Error(`Invalid Owner ID: "${dto.ownerId}". Must be in Short ID format (e.g., OWN-2001).`));
    }
    if (!isValidPetId(dto.petId) && !isValidGuid(dto.petId)) {
      return Promise.reject(new Error(`Invalid Pet ID: "${dto.petId}". Must be in Short ID format (e.g., PET-1001).`));
    }
    return apiRequest<ConsultationRequest>("/Consultations", {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /**
   * UC-14: Validate Pet Ownership explicitly (GET /api/Consultations/validate-ownership?petId=...&ownerId=...)
   */
  validateOwnership: (
    petId: string,
    ownerId: string
  ): Promise<{ isValid: boolean; message: string; petId?: string; ownerId?: string }> => {
    return apiRequest<{ isValid: boolean; message: string; petId?: string; ownerId?: string }>(
      `/Consultations/validate-ownership?petId=${encodeURIComponent(petId)}&ownerId=${encodeURIComponent(ownerId)}`
    );
  },

  /**
   * UC-15: View all consultation requests (GET /api/Consultations?status=...)
   */
  getAllConsultations: (statusFilter?: ConsultationStatus | number): Promise<ConsultationRequest[]> => {
    const query = statusFilter ? `?status=${statusFilter}` : "";
    return apiRequest<ConsultationRequest[]>(`/Consultations${query}`);
  },

  /**
   * UC-15: View single consultation request by ID (GET /api/Consultations/{id})
   */
  getConsultationById: (id: string): Promise<ConsultationRequest> => {
    if (!isValidRequestId(id) && !isValidGuid(id)) {
      return Promise.reject(new Error(`Invalid Consultation ID: "${id}". Must be in Short ID format (e.g., REQ-5001).`));
    }
    return apiRequest<ConsultationRequest>(`/Consultations/${id}`);
  },

  /**
   * UC-15: View consultation requests for owner (GET /api/Consultations/owner/{ownerId})
   */
  getConsultationsByOwner: (ownerId: string): Promise<ConsultationRequest[]> => {
    if (!isValidOwnerId(ownerId) && !isValidGuid(ownerId)) {
      return Promise.reject(new Error(`Invalid Owner ID: "${ownerId}". Must be in Short ID format (e.g., OWN-2001).`));
    }
    return apiRequest<ConsultationRequest[]>(`/Consultations/owner/${ownerId}`);
  },

  /**
   * UC-15: View consultation requests for pet (GET /api/Consultations/pet/{petId})
   */
  getConsultationsByPet: (petId: string): Promise<ConsultationRequest[]> => {
    if (!isValidPetId(petId) && !isValidGuid(petId)) {
      return Promise.reject(new Error(`Invalid Pet ID: "${petId}". Must be in Short ID format (e.g., PET-1001).`));
    }
    return apiRequest<ConsultationRequest[]>(`/Consultations/pet/${petId}`);
  },

  /**
   * UC-16: Track Consultation Workflow Status (GET /api/Consultations/{id}/status)
   */
  getStatus: (id: string): Promise<ConsultationStatusTrackingDto> => {
    if (!isValidRequestId(id) && !isValidGuid(id)) {
      return Promise.reject(new Error(`Invalid Consultation ID: "${id}". Must be in Short ID format (e.g., REQ-5001).`));
    }
    return apiRequest<ConsultationStatusTrackingDto>(`/Consultations/${id}/status`);
  },

  /**
   * UC-16: Update Consultation Workflow Status (PATCH /api/Consultations/{id}/status)
   */
  updateStatus: (id: string, dto: UpdateConsultationStatusDto): Promise<ConsultationRequest> => {
    if (!isValidRequestId(id) && !isValidGuid(id)) {
      return Promise.reject(new Error(`Invalid Consultation ID: "${id}". Must be in Short ID format (e.g., REQ-5001).`));
    }
    return apiRequest<ConsultationRequest>(`/Consultations/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify(dto),
    });
  },

  /**
   * UC-17: View Consultation Request History / Audit Log (GET /api/Consultations/{id}/history)
   */
  getHistory: (id: string): Promise<ConsultationHistoryItemDto[]> => {
    if (!isValidRequestId(id) && !isValidGuid(id)) {
      return Promise.reject(new Error(`Invalid Consultation ID: "${id}". Must be in Short ID format (e.g., REQ-5001).`));
    }
    return apiRequest<ConsultationHistoryItemDto[]>(`/Consultations/${id}/history`);
  },
};

// ----------------------------------------------------
// Pet Owners API Service
// ----------------------------------------------------

export const ownerService = {
  getAllOwners: (): Promise<PetOwner[]> => {
    return apiRequest<PetOwner[]>("/owners");
  },
  getOwnerById: (id: string): Promise<PetOwner> => {
    if (!isValidOwnerId(id) && !isValidGuid(id)) {
      return Promise.reject(new Error(`Invalid Owner ID: "${id}". Must be in Short ID format (e.g., OWN-2001).`));
    }
    return apiRequest<PetOwner>(`/owners/${id}`);
  },
};
