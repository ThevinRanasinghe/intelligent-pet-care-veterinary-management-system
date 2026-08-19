/**
 * API boundary for the ASP.NET Core backend (Component 1: Pet & Consultation Management).
 */

export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5143/api";

/**
 * Validates whether a given string is a valid standard UUID/GUID.
 */
export function isValidGuid(value: string): boolean {
  if (!value || typeof value !== "string") return false;
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
    value.trim()
  );
}

/**
 * Generates a random v4 UUID/GUID.
 */
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

/**
 * Demo preset Owner IDs for quick testing and UI simulation.
 */
export const DEMO_OWNER_ID = "e2b8d000-0000-0000-0000-000000000001";
export const DEMO_OWNERS = [
  { id: "e2b8d000-0000-0000-0000-000000000001", name: "Sarah Fernando", phone: "+94 77 123 4567" },
  { id: "e2b8d000-0000-0000-0000-000000000002", name: "Kamal Perera", phone: "+94 71 987 6543" },
  { id: "e2b8d000-0000-0000-0000-000000000003", name: "Anura Silva", phone: "+94 75 555 1212" },
];

export const DEMO_BRANCHES = [
  "Colombo Branch",
  "Kandy Branch",
  "Nugegoda Branch",
  "Negombo Branch",
  "Galle Branch",
];

/**
 * Parses HTTP error response body into a clean human-readable error message.
 */
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
  options?: RequestInit,
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
// TypeScript Domain Interfaces & DTOs
// ----------------------------------------------------

export interface Pet {
  id: string;
  ownerId: string;
  name: string;
  species: string;
  breed: string;
  age: number;
  medicalHistorySummary?: string | null;
  createdAt?: string;
}

export interface CreatePetDto {
  ownerId: string;
  name: string;
  species: string;
  breed: string;
  age: number;
  medicalHistorySummary?: string | null;
}

export interface ConsultationRequest {
  id: string;
  petId: string;
  ownerId: string;
  symptomsDescription: string;
  photoUrl?: string | null;
  preferredBranch: string;
  preferredDate: string;
  budgetLimit: number;
  status: string;
  createdAt?: string;
}

export interface CreateConsultationRequestDto {
  petId: string;
  ownerId: string;
  symptomsDescription: string;
  photoUrl?: string | null;
  preferredBranch: string;
  preferredDate: string;
  budgetLimit: number;
}

export interface ConsultationStatusResponse {
  consultationId: string;
  status: string;
  updatedAt?: string;
}

// ----------------------------------------------------
// Pet API Service
// ----------------------------------------------------

export const petService = {
  /**
   * Register a new pet (Endpoint 1: POST /api/Pets)
   */
  createPet: (dto: CreatePetDto): Promise<Pet> => {
    if (!isValidGuid(dto.ownerId)) {
      return Promise.reject(new Error(`Invalid Owner GUID: "${dto.ownerId}". Must be a valid UUID.`));
    }
    return apiRequest<Pet>("/Pets", {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /**
   * Get all pets for a specific owner (Endpoint 2: GET /api/Pets/owner/{ownerId})
   */
  getPetsByOwner: (ownerId: string): Promise<Pet[]> => {
    if (!isValidGuid(ownerId)) {
      return Promise.reject(new Error(`Invalid Owner GUID: "${ownerId}". Must be a valid UUID.`));
    }
    return apiRequest<Pet[]>(`/Pets/owner/${ownerId}`);
  },

  /**
   * Get all registered pets in the clinic (GET /api/Pets)
   */
  getAllPets: (): Promise<Pet[]> => {
    return apiRequest<Pet[]>("/Pets");
  },

  /**
   * Get a single pet by ID (GET /api/Pets/{id})
   */
  getPetById: (id: string): Promise<Pet> => {
    if (!isValidGuid(id)) {
      return Promise.reject(new Error(`Invalid Pet GUID: "${id}". Must be a valid UUID.`));
    }
    return apiRequest<Pet>(`/Pets/${id}`);
  },
};

// ----------------------------------------------------
// Consultation API Service
// ----------------------------------------------------

export const consultationService = {
  /**
   * Create consultation request with ownership validation (Endpoint 3: POST /api/Consultations)
   */
  createConsultation: (dto: CreateConsultationRequestDto): Promise<ConsultationRequest> => {
    if (!isValidGuid(dto.ownerId)) {
      return Promise.reject(new Error(`Invalid Owner GUID: "${dto.ownerId}". Must be a valid UUID.`));
    }
    if (!isValidGuid(dto.petId)) {
      return Promise.reject(new Error(`Invalid Pet GUID: "${dto.petId}". Must be a valid UUID.`));
    }
    return apiRequest<ConsultationRequest>("/Consultations", {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /**
   * Check status of a consultation request (Endpoint 4: GET /api/Consultations/{id}/status)
   */
  getStatus: (id: string): Promise<ConsultationStatusResponse> => {
    if (!isValidGuid(id)) {
      return Promise.reject(new Error(`Invalid Consultation GUID: "${id}". Must be a valid UUID.`));
    }
    return apiRequest<ConsultationStatusResponse>(`/Consultations/${id}/status`);
  },

  /**
   * Get all consultation requests for an owner (GET /api/Consultations/owner/{ownerId})
   */
  getConsultationsByOwner: (ownerId: string): Promise<ConsultationRequest[]> => {
    if (!isValidGuid(ownerId)) {
      return Promise.reject(new Error(`Invalid Owner GUID: "${ownerId}". Must be a valid UUID.`));
    }
    return apiRequest<ConsultationRequest[]>(`/Consultations/owner/${ownerId}`);
  },

  /**
   * Get all consultation requests across the clinic (GET /api/Consultations)
   */
  getAllConsultations: (): Promise<ConsultationRequest[]> => {
    return apiRequest<ConsultationRequest[]>("/Consultations");
  },

  /**
   * Get a single consultation request by ID (GET /api/Consultations/{id})
   */
  getConsultationById: (id: string): Promise<ConsultationRequest> => {
    if (!isValidGuid(id)) {
      return Promise.reject(new Error(`Invalid Consultation GUID: "${id}". Must be a valid UUID.`));
    }
    return apiRequest<ConsultationRequest>(`/Consultations/${id}`);
  },
};
