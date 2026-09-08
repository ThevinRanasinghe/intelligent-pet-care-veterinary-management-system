import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import {
  isValidShortId,
  isValidPetId,
  isValidOwnerId,
  isValidRequestId,
  isValidMedicalRecordId,
  isValidVaccinationRecordId,
  generateShortId,
  calculateDistanceKm,
  findNearestClinic,
  parseApiError,
  petService,
  consultationService,
  DEMO_OWNER_ID,
  ConsultationStatus,
  type CreatePetDto,
  type UpdatePetProfileDto,
  type CreateConsultationRequestDto,
} from "../services/api";

describe("Short ID Schema Validation & Generator", () => {
  it("validates correct Short ID prefixes and formats", () => {
    expect(isValidPetId("PET-1001")).toBe(true);
    expect(isValidPetId("pet-2005")).toBe(true);
    expect(isValidOwnerId("OWN-2001")).toBe(true);
    expect(isValidRequestId("REQ-5001")).toBe(true);
    expect(isValidMedicalRecordId("MED-3001")).toBe(true);
    expect(isValidVaccinationRecordId("VAC-4001")).toBe(true);
  });

  it("rejects invalid Short ID formats", () => {
    expect(isValidPetId("")).toBe(false);
    expect(isValidPetId("PET-")).toBe(false);
    expect(isValidPetId("PET-12")).toBe(false); // requires at least 3 digits
    expect(isValidPetId("DOG-1001")).toBe(false);
    expect(isValidOwnerId("12345")).toBe(false);
    expect(isValidRequestId("REQ-ABCD")).toBe(false);
  });

  it("generates valid Short IDs with appropriate prefix", () => {
    const petId = generateShortId("PET");
    expect(isValidPetId(petId)).toBe(true);

    const ownerId = generateShortId("OWN");
    expect(isValidOwnerId(ownerId)).toBe(true);

    const reqId = generateShortId("REQ");
    expect(isValidRequestId(reqId)).toBe(true);
  });
});

describe("GPS Distance & Nearest Clinic Matcher", () => {
  it("calculates realistic Haversine distance in kilometers", () => {
    // Distance between Downtown SF (37.7749, -122.4194) and Northside SF (37.7983, -122.4075) is ~2.8 km
    const dist = calculateDistanceKm(37.7749, -122.4194, 37.7983, -122.4075);
    expect(dist).toBeGreaterThan(2);
    expect(dist).toBeLessThan(4);
  });

  it("identifies Downtown branch as nearest for downtown coordinates", () => {
    const nearest = findNearestClinic(37.7749, -122.4194);
    expect(nearest.name).toBe("Downtown Central Vet Clinic");
    expect(nearest.distanceKm).toBe(0);
  });

  it("identifies Northside branch for coordinates close to North Beach", () => {
    const nearest = findNearestClinic(37.7985, -122.4070);
    expect(nearest.name).toBe("Northside Veterinary Hospital");
  });
});

describe("API Error Parsing", () => {
  it("extracts custom error message property from json", async () => {
    const mockResponse = new Response(
      JSON.stringify({ message: "Pet ownership validation failed. Pet 'PET-1001' does not belong to owner 'OWN-2002'." }),
      { status: 400, headers: { "Content-Type": "application/json" } }
    );
    const parsed = await parseApiError(mockResponse);
    expect(parsed).toContain("Pet ownership validation failed");
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

describe("Pet Service API Client (UC-05 to UC-08)", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("rejects invalid Owner ID on createPet before network call", async () => {
    const invalidDto: CreatePetDto = {
      ownerId: "bad-owner-id",
      name: "Buddy",
      species: "Dog",
      breed: "Golden Retriever",
      age: 3,
    };
    await expect(petService.createPet(invalidDto)).rejects.toThrow("Invalid Owner ID");
    expect(fetch).not.toHaveBeenCalled();
  });

  it("successfully calls POST /api/Pets with Short ID payload", async () => {
    const validDto: CreatePetDto = {
      ownerId: DEMO_OWNER_ID,
      name: "Buddy",
      species: "Dog",
      breed: "Golden Retriever",
      dateOfBirth: "2023-01-15T00:00:00.000Z",
      age: 3,
      notes: "Friendly and fully vaccinated",
    };

    const mockCreated = {
      id: "PET-1001",
      ...validDto,
      createdAt: "2026-08-19T10:00:00Z",
      updatedAt: "2026-08-19T10:00:00Z",
    };

    (fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 201,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => mockCreated,
    });

    const result = await petService.createPet(validDto);
    expect(result.id).toBe("PET-1001");
    expect(result.name).toBe("Buddy");
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/Pets"),
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify(validDto),
      })
    );
  });

  it("successfully calls PUT /api/Pets/{id} to update non-clinical profile", async () => {
    const petId = "PET-1001";
    const updateDto: UpdatePetProfileDto = {
      name: "Buddy Updated",
      species: "Dog",
      breed: "Golden Retriever",
      age: 4,
      notes: "Updated diet plan",
    };

    (fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => ({ id: petId, ownerId: DEMO_OWNER_ID, ...updateDto }),
    });

    const res = await petService.updatePetProfile(petId, updateDto, DEMO_OWNER_ID);
    expect(res.name).toBe("Buddy Updated");
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining(`/Pets/${petId}?ownerId=${DEMO_OWNER_ID}`),
      expect.objectContaining({ method: "PUT" })
    );
  });

  it("successfully fetches medical and vaccination history for pet (UC-08)", async () => {
    const petId = "PET-1001";
    const mockHistory = {
      petId,
      petName: "Buddy",
      medicalRecords: [
        {
          id: "MED-3001",
          petId,
          recordDate: "2026-02-10T10:00:00Z",
          diagnosis: "Otitis Externa",
          treatment: "Ear drops",
          veterinarianName: "Dr. Emily Hayes, DVM",
          createdAt: "2026-02-10T10:00:00Z",
        },
      ],
      vaccinationRecords: [
        {
          id: "VAC-4001",
          petId,
          vaccineName: "Rabies Booster",
          dateAdministered: "2025-08-10T10:00:00Z",
          nextDueDate: "2028-08-10T10:00:00Z",
          veterinarianName: "Dr. Emily Hayes, DVM",
          batchNumber: "RAB-2025-99B",
          createdAt: "2025-08-10T10:00:00Z",
        },
      ],
    };

    (fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => mockHistory,
    });

    const res = await petService.getPetMedicalHistory(petId);
    expect(res.petName).toBe("Buddy");
    expect(res.medicalRecords.length).toBe(1);
    expect(res.vaccinationRecords[0].id).toBe("VAC-4001");
  });
});

describe("Consultation Service API Client (UC-09 to UC-17)", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("rejects invalid Pet or Owner Short ID before network call", async () => {
    const invalidDto: CreateConsultationRequestDto = {
      petId: "invalid-pet",
      ownerId: DEMO_OWNER_ID,
      symptomsDescription: "Ear infection symptoms",
      preferredDate: "2026-09-10T10:00:00Z",
      budgetLimit: 5000,
    };
    await expect(consultationService.createConsultation(invalidDto)).rejects.toThrow("Invalid Pet ID");
    expect(fetch).not.toHaveBeenCalled();
  });

  it("submits consultation request with GPS coordinates and branch (UC-09 to UC-13)", async () => {
    const validDto: CreateConsultationRequestDto = {
      petId: "PET-1001",
      ownerId: "OWN-2001",
      symptomsDescription: "Persistent scratching and whimpering",
      photoUrl: "https://images.unsplash.com/photo-1543466835-00a7907e9de1",
      preferredDate: "2026-09-10T09:30:00.000Z",
      budgetLimit: 15000,
      preferredClinicLocationLat: 37.7749,
      preferredClinicLocationLong: -122.4194,
      preferredBranch: "Downtown Central Vet Clinic",
    };

    const mockResponse = {
      id: "REQ-5001",
      ...validDto,
      status: "Submitted",
      createdAt: "2026-09-08T10:00:00Z",
      updatedAt: "2026-09-08T10:00:00Z",
    };

    (fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 201,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => mockResponse,
    });

    const res = await consultationService.createConsultation(validDto);
    expect(res.id).toBe("REQ-5001");
    expect(res.status).toBe("Submitted");
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/Consultations"),
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify(validDto),
      })
    );
  });

  it("validates ownership via UC-14 endpoint", async () => {
    (fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => ({ isValid: true, petId: "PET-1001", ownerId: "OWN-2001", message: "Verified" }),
    });

    const result = await consultationService.validateOwnership("PET-1001", "OWN-2001");
    expect(result.isValid).toBe(true);
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/Consultations/validate-ownership?petId=PET-1001&ownerId=OWN-2001"),
      expect.anything()
    );
  });

  it("fetches consultation status tracking with 7-step history (UC-16)", async () => {
    const reqId = "REQ-5001";
    const mockTracking = {
      consultationId: reqId,
      petId: "PET-1001",
      status: "Processing",
      statusNotes: "Staff assigned",
      updatedAt: "2026-09-08T11:00:00Z",
      history: [
        { id: 1, status: "Submitted", comments: "Initial request", changedAt: "2026-09-08T10:00:00Z" },
        { id: 2, status: "Processing", comments: "Under vet review", changedAt: "2026-09-08T11:00:00Z" },
      ],
    };

    (fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => mockTracking,
    });

    const res = await consultationService.getStatus(reqId);
    expect(res.status).toBe("Processing");
    expect(res.history.length).toBe(2);
  });

  it("updates consultation workflow status via PATCH (UC-16)", async () => {
    const reqId = "REQ-5001";
    (fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => ({ id: reqId, status: "Approved" }),
    });

    const res = await consultationService.updateStatus(reqId, {
      status: ConsultationStatus.Approved,
      comments: "Approved by Clinic Manager",
    });

    expect(res.status).toBe("Approved");
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining(`/Consultations/${reqId}/status`),
      expect.objectContaining({
        method: "PATCH",
        body: JSON.stringify({ status: 4, comments: "Approved by Clinic Manager" }),
      })
    );
  });

  it("retrieves full audit history trail (UC-17)", async () => {
    const reqId = "REQ-5001";
    const mockAudit = [
      { id: 2, status: "PendingApproval", comments: "Quotation created", changedAt: "2026-09-08T12:00:00Z" },
      { id: 1, status: "Submitted", comments: "Submitted by owner", changedAt: "2026-09-08T10:00:00Z" },
    ];

    (fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => mockAudit,
    });

    const res = await consultationService.getHistory(reqId);
    expect(res.length).toBe(2);
    expect(res[0].status).toBe("PendingApproval");
  });
});
