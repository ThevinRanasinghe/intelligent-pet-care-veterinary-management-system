import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import {
  isValidGuid,
  generateGuid,
  parseApiError,
  petService,
  consultationService,
  DEMO_OWNER_ID,
  type CreatePetDto,
  type CreateConsultationRequestDto,
} from "../services/api";

describe("GUID Validation & Generator", () => {
  it("validates correct UUID v4 strings", () => {
    expect(isValidGuid("e2b8d000-0000-0000-0000-000000000001")).toBe(true);
    expect(isValidGuid("7c9e6679-7425-40de-944b-e07fc1f90ae7")).toBe(true);
    expect(isValidGuid("A98B5F2C-6D3E-4B1A-9F0D-8E7C6B5A4D3C")).toBe(true);
  });

  it("rejects invalid GUID formats", () => {
    expect(isValidGuid("")).toBe(false);
    expect(isValidGuid("not-a-guid")).toBe(false);
    expect(isValidGuid("12345")).toBe(false);
    expect(isValidGuid("e2b8d000-0000-0000-0000")).toBe(false);
    expect(isValidGuid("e2b8d000000000000000000000000001")).toBe(false);
  });

  it("generates valid standard UUIDs", () => {
    const guid = generateGuid();
    expect(isValidGuid(guid)).toBe(true);
  });
});

describe("API Error Parsing", () => {
  it("extracts custom error message property from json", async () => {
    const mockResponse = new Response(
      JSON.stringify({ message: "Pet ownership validation failed. Pet does not belong to this owner." }),
      { status: 400, headers: { "Content-Type": "application/json" } }
    );
    const parsed = await parseApiError(mockResponse);
    expect(parsed).toBe("Pet ownership validation failed. Pet does not belong to this owner.");
  });

  it("extracts ProblemDetails title and field errors", async () => {
    const mockResponse = new Response(
      JSON.stringify({
        title: "Validation Error",
        errors: {
          OwnerId: ["The OwnerId field is required."],
        },
      }),
      { status: 400, headers: { "Content-Type": "application/json" } }
    );
    const parsed = await parseApiError(mockResponse);
    expect(parsed).toContain("Validation Error");
    expect(parsed).toContain("The OwnerId field is required.");
  });

  it("falls back to plain text if not json", async () => {
    const mockResponse = new Response("Internal Server Error", {
      status: 500,
      headers: { "Content-Type": "text/plain" },
    });
    const parsed = await parseApiError(mockResponse);
    expect(parsed).toBe("Internal Server Error");
  });
});

describe("Pet Service API Client", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("rejects invalid Owner GUID on createPet before network call", async () => {
    const invalidDto: CreatePetDto = {
      ownerId: "invalid-id",
      name: "Milo",
      species: "Dog",
      breed: "Beagle",
      age: 2,
    };
    await expect(petService.createPet(invalidDto)).rejects.toThrow("Invalid Owner GUID");
    expect(fetch).not.toHaveBeenCalled();
  });

  it("successfully calls POST /api/Pets with valid payload", async () => {
    const validDto: CreatePetDto = {
      ownerId: DEMO_OWNER_ID,
      name: "Bella",
      species: "Cat",
      breed: "Persian",
      age: 3,
      medicalHistorySummary: "Vaccinated",
    };

    const mockCreated = {
      id: "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      ...validDto,
      createdAt: "2026-08-19T10:00:00Z",
    };

    (fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 201,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => mockCreated,
    });

    const result = await petService.createPet(validDto);
    expect(result.id).toBe("7c9e6679-7425-40de-944b-e07fc1f90ae7");
    expect(result.name).toBe("Bella");
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/Pets"),
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify(validDto),
      })
    );
  });
});

describe("Consultation Service API Client", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("rejects invalid Pet or Owner GUID on createConsultation", async () => {
    const invalidDto: CreateConsultationRequestDto = {
      petId: "invalid-pet",
      ownerId: DEMO_OWNER_ID,
      symptomsDescription: "Coughing",
      preferredBranch: "Colombo Branch",
      preferredDate: "2026-08-20T10:00:00Z",
      budgetLimit: 5000,
    };
    await expect(consultationService.createConsultation(invalidDto)).rejects.toThrow("Invalid Pet GUID");
    expect(fetch).not.toHaveBeenCalled();
  });

  it("rejects invalid Consultation GUID on getStatus", async () => {
    await expect(consultationService.getStatus("bad-guid")).rejects.toThrow("Invalid Consultation GUID");
    expect(fetch).not.toHaveBeenCalled();
  });

  it("successfully calls GET /api/Consultations/{id}/status", async () => {
    const validId = "7c9e6679-7425-40de-944b-e07fc1f90ae7";
    const mockStatus = {
      consultationId: validId,
      status: "Pending",
      updatedAt: "2026-08-19T10:30:00Z",
    };

    (fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => mockStatus,
    });

    const res = await consultationService.getStatus(validId);
    expect(res.status).toBe("Pending");
    expect(res.consultationId).toBe(validId);
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining(`/Consultations/${validId}/status`),
      expect.anything()
    );
  });
});
