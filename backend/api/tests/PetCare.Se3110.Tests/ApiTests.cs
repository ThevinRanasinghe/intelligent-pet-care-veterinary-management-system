using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PetCare.Domain.Constants;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Se3110.Tests;

/// <summary>
/// SE3110 API-level discovery tests (catalogue TC-BE-003..025, TC-AI-018,
/// TC-E2E-002, TC-E2E-004). Assertions are taken from the catalogue's
/// expected values — failures are recorded, not fixed.
/// </summary>
public class ApiTests : IClassFixture<Se3110Fixture>
{
    private readonly Se3110Fixture _fx;

    public ApiTests(Se3110Fixture fx) => _fx = fx;

    private static StringContent JsonBody(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private async Task<(Domain.Entities.Organization org, Domain.Entities.User manager,
        HttpClient client)> OrgWithManagerAsync()
    {
        var org = await _fx.SeedOrgAsync();
        var manager = await _fx.SeedUserAsync(Roles.ClinicManager, org.Id);
        return (org, manager, _fx.ClientFor(manager));
    }

    // ---------- TC-BE-003 ----------
    [Fact]
    public async Task Login_Valid_Returns200AndToken()
    {
        var user = await _fx.SeedUserAsync(Roles.PetOwner, password: "Passw0rd!");
        var client = _fx.CreateClient();

        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "Passw0rd!" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"token\"", body, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- TC-BE-004 ----------
    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var user = await _fx.SeedUserAsync(Roles.PetOwner, password: "Passw0rd!");
        var client = _fx.CreateClient();

        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "WrongPass123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---------- TC-BE-005 ----------
    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var client = _fx.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register/pet-owner", new
        {
            firstName = "SE3110",
            lastName = "Tester",
            email = "not-an-email",
            password = "Passw0rd!",
            confirmPassword = "Passw0rd!",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- TC-BE-006 ----------
    [Fact]
    public async Task Register_DuplicateEmail_Rejected()
    {
        var client = _fx.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@se3110.test";
        var payload = new
        {
            firstName = "SE3110",
            lastName = "Tester",
            email,
            password = "Passw0rd!",
            confirmPassword = "Passw0rd!",
        };

        var first = await client.PostAsJsonAsync("/api/auth/register/pet-owner", payload);
        Assert.True(first.IsSuccessStatusCode,
            $"first registration unexpectedly failed: {(int)first.StatusCode}");

        var second = await client.PostAsJsonAsync("/api/auth/register/pet-owner", payload);
        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.True((int)second.StatusCode is 400 or 409,
            $"expected 400/409, got {(int)second.StatusCode} body: {secondBody}");
    }

    // ---------- appointment helpers ----------

    private async Task<(Domain.Entities.Organization org, HttpClient mgrClient,
        Domain.Entities.Veterinarian vet, Domain.Entities.Pet pet)> BookingSeedAsync()
    {
        var org = await _fx.SeedOrgAsync();
        var manager = await _fx.SeedUserAsync(Roles.ClinicManager, org.Id);
        var vetUser = await _fx.SeedUserAsync(Roles.Veterinarian, org.Id);
        var vet = await _fx.SeedVetAsync(org.Id, vetUser.Id);
        var (_, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        return (org, _fx.ClientFor(manager), vet, pet);
    }

    private static object ApptPayload(Domain.Entities.Pet pet, Guid vetId,
        Guid slotId, DateTime start, DateTime end) => new
    {
        petId = pet.Id,
        veterinarianId = vetId,
        appointmentSlotId = slotId,
        scheduledStart = start,
        scheduledEnd = end,
    };

    // ---------- TC-BE-007 ----------
    [Fact]
    public async Task CreateAppointment_OverlappingTime_Returns409()
    {
        var (_, client, vet, pet) = await BookingSeedAsync();
        var day = DateTime.UtcNow.Date.AddDays(3);
        var slot1 = await _fx.SeedSlotAsync(vet.Id, new TimeOnly(9, 0), new TimeOnly(9, 30));
        var slot2 = await _fx.SeedSlotAsync(vet.Id, new TimeOnly(9, 15), new TimeOnly(9, 45));

        var first = await client.PostAsJsonAsync("/api/appointments",
            ApptPayload(pet, vet.Id, slot1.Id, day.AddHours(9), day.AddHours(9.5)));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var overlap = await client.PostAsJsonAsync("/api/appointments",
            ApptPayload(pet, vet.Id, slot2.Id, day.Add(new TimeSpan(9, 15, 0)),
                day.Add(new TimeSpan(9, 45, 0))));
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
    }

    // ---------- TC-BE-008 ----------
    [Fact]
    public async Task CreateAppointment_StartNotBeforeEnd_Returns400()
    {
        var (_, client, vet, pet) = await BookingSeedAsync();
        var day = DateTime.UtcNow.Date.AddDays(3);
        var slot = await _fx.SeedSlotAsync(vet.Id, new TimeOnly(9, 0), new TimeOnly(10, 0));

        var res = await client.PostAsJsonAsync("/api/appointments",
            ApptPayload(pet, vet.Id, slot.Id, day.AddHours(10), day.AddHours(9)));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- TC-BE-009 ----------
    [Fact]
    public async Task CreateAppointment_SpansDays_Returns400()
    {
        var (_, client, vet, pet) = await BookingSeedAsync();
        var day = DateTime.UtcNow.Date.AddDays(3);
        var slot = await _fx.SeedSlotAsync(vet.Id, new TimeOnly(23, 0), new TimeOnly(23, 59));

        var res = await client.PostAsJsonAsync("/api/appointments",
            ApptPayload(pet, vet.Id, slot.Id,
                day.Add(new TimeSpan(23, 30, 0)),
                day.AddDays(1).Add(new TimeSpan(0, 30, 0))));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- TC-BE-010 ----------
    [Fact]
    public async Task CreateAppointment_OutsideSlot_Returns409()
    {
        var (_, client, vet, pet) = await BookingSeedAsync();
        var day = DateTime.UtcNow.Date.AddDays(3);
        var slot = await _fx.SeedSlotAsync(vet.Id, new TimeOnly(9, 0), new TimeOnly(9, 30));

        var res = await client.PostAsJsonAsync("/api/appointments",
            ApptPayload(pet, vet.Id, slot.Id, day.AddHours(11), day.Add(new TimeSpan(11, 30, 0))));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ---------- TC-BE-011 ----------
    [Fact]
    public async Task CreateAppointment_SlotOfOtherVet_Returns409()
    {
        var (org, client, vet, pet) = await BookingSeedAsync();
        var otherVet = await _fx.SeedVetAsync(org.Id);
        var day = DateTime.UtcNow.Date.AddDays(3);
        var otherSlot = await _fx.SeedSlotAsync(otherVet.Id,
            new TimeOnly(9, 0), new TimeOnly(9, 30));

        var res = await client.PostAsJsonAsync("/api/appointments",
            ApptPayload(pet, vet.Id, otherSlot.Id, day.AddHours(9), day.AddHours(9.5)));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ---------- TC-BE-012 ----------
    [Fact]
    public async Task CreateAppointment_InactiveVet_Returns409()
    {
        var (org, client, _, pet) = await BookingSeedAsync();
        var inactiveVet = await _fx.SeedVetAsync(org.Id, active: false);
        var day = DateTime.UtcNow.Date.AddDays(3);
        var slot = await _fx.SeedSlotAsync(inactiveVet.Id,
            new TimeOnly(9, 0), new TimeOnly(9, 30));

        var res = await client.PostAsJsonAsync("/api/appointments",
            ApptPayload(pet, inactiveVet.Id, slot.Id, day.AddHours(9), day.AddHours(9.5)));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ---------- TC-BE-013 ----------
    [Fact]
    public async Task CreateAppointment_ArchivedPet_Returns409()
    {
        var (_, client, vet, _) = await BookingSeedAsync();
        var (_, owner) = await _fx.SeedOwnerAsync();
        var archived = await _fx.SeedPetAsync(owner.Id, archived: true);
        var day = DateTime.UtcNow.Date.AddDays(3);
        var slot = await _fx.SeedSlotAsync(vet.Id, new TimeOnly(9, 0), new TimeOnly(9, 30));

        var res = await client.PostAsJsonAsync("/api/appointments",
            ApptPayload(archived, vet.Id, slot.Id, day.AddHours(9), day.AddHours(9.5)));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ---------- TC-BE-014 ----------
    [Fact]
    public async Task CancelThenRebook_Succeeds()
    {
        var (_, client, vet, pet) = await BookingSeedAsync();
        var day = DateTime.UtcNow.Date.AddDays(3);
        var slot1 = await _fx.SeedSlotAsync(vet.Id, new TimeOnly(9, 0), new TimeOnly(9, 30));
        var slot2 = await _fx.SeedSlotAsync(vet.Id, new TimeOnly(10, 0), new TimeOnly(10, 30));

        var created = await client.PostAsJsonAsync("/api/appointments",
            ApptPayload(pet, vet.Id, slot1.Id, day.AddHours(9), day.AddHours(9.5)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var apptId = doc.RootElement.GetProperty("id").GetString();

        var del = await client.DeleteAsync($"/api/appointments/{apptId}");
        Assert.True(del.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK,
            $"cancel returned {(int)del.StatusCode}");

        var rebook = await client.PostAsJsonAsync("/api/appointments",
            ApptPayload(pet, vet.Id, slot2.Id, day.AddHours(10), day.AddHours(10.5)));
        Assert.Equal(HttpStatusCode.Created, rebook.StatusCode);
    }

    // ---------- TC-BE-015 ----------
    [Fact]
    public async Task CreateMedicine_NegativePrice_Returns400()
    {
        var org = await _fx.SeedOrgAsync();
        var io = await _fx.SeedUserAsync(Roles.InventoryOfficer, org.Id);
        var client = _fx.ClientFor(io);

        var res = await client.PostAsJsonAsync("/api/medicines", new
        {
            name = "SE3110 NegPrice " + Guid.NewGuid().ToString("N")[..6],
            category = "Test",
            description = "x",
            dosageForm = "Tablet",
            strength = "10mg",
            unitPrice = -5m,
            manufacturer = "T",
            reorderLevel = 1,
        });
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.BadRequest,
            $"expected 400, got {(int)res.StatusCode} body: {body}");
    }

    // ---------- TC-BE-016 ----------
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task StockIn_NonPositiveQty_Returns400(int quantity)
    {
        var org = await _fx.SeedOrgAsync();
        var io = await _fx.SeedUserAsync(Roles.InventoryOfficer, org.Id);
        var med = await _fx.SeedMedicineAsync(org.Id);
        var supplier = await _fx.SeedSupplierAsync(org.Id);
        var client = _fx.ClientFor(io);

        var res = await client.PostAsJsonAsync($"/api/medicines/{med.Id}/stock-in", new
        {
            supplierId = supplier.Id,
            batchNumber = "BN-" + Guid.NewGuid().ToString("N")[..8],
            quantity,
            expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
        });
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.BadRequest,
            $"expected 400, got {(int)res.StatusCode} body: {body}");
    }

    // ---------- TC-BE-017 ----------
    [Fact]
    public async Task Reserve_ExceedsStock_Returns409()
    {
        var org = await _fx.SeedOrgAsync();
        var io = await _fx.SeedUserAsync(Roles.InventoryOfficer, org.Id);
        var (med, _) = await _fx.SeedStockAsync(org.Id, 5);
        var client = _fx.ClientFor(io);

        var res = await client.PostAsJsonAsync("/api/medicine-reservations",
            new { medicineId = med.Id, quantity = 6 });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ---------- TC-BE-018 ----------
    [Fact]
    public async Task Reserve_ExactlyAvailable_Boundary()
    {
        var org = await _fx.SeedOrgAsync();
        var io = await _fx.SeedUserAsync(Roles.InventoryOfficer, org.Id);
        var (med, _) = await _fx.SeedStockAsync(org.Id, 5);
        var client = _fx.ClientFor(io);

        var exact = await client.PostAsJsonAsync("/api/medicine-reservations",
            new { medicineId = med.Id, quantity = 5 });
        Assert.True(exact.IsSuccessStatusCode,
            $"reserve exactly-available failed: {(int)exact.StatusCode}");

        var oneMore = await client.PostAsJsonAsync("/api/medicine-reservations",
            new { medicineId = med.Id, quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, oneMore.StatusCode);
    }

    // ---------- TC-BE-019 ----------
    [Fact]
    public async Task Dispense_Twice_SecondReturns409()
    {
        var org = await _fx.SeedOrgAsync();
        var io = await _fx.SeedUserAsync(Roles.InventoryOfficer, org.Id);
        var (med, _) = await _fx.SeedStockAsync(org.Id, 5);
        var client = _fx.ClientFor(io);

        var res = await client.PostAsJsonAsync("/api/medicine-reservations",
            new { medicineId = med.Id, quantity = 2 });
        Assert.True(res.IsSuccessStatusCode);
        var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var resvId = doc.RootElement.GetProperty("id").GetString();

        var first = await client.PostAsync(
            $"/api/medicine-reservations/{resvId}/dispense", null);
        Assert.True(first.IsSuccessStatusCode,
            $"first dispense failed: {(int)first.StatusCode}");

        var second = await client.PostAsync(
            $"/api/medicine-reservations/{resvId}/dispense", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ---------- approval helpers ----------

    private async Task<(HttpClient mgrClient, Domain.Entities.Approval approval)>
        PendingApprovalSeedAsync()
    {
        var org = await _fx.SeedOrgAsync();
        var manager = await _fx.SeedUserAsync(Roles.ClinicManager, org.Id);
        var vetUser = await _fx.SeedUserAsync(Roles.Veterinarian, org.Id);
        var vet = await _fx.SeedVetAsync(org.Id, vetUser.Id);
        var (_, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        var (_, appt) = await _fx.SeedBookedAppointmentAsync(pet.Id, vet.Id);
        var (_, approval) = await _fx.SeedPendingApprovalAsync(appt.Id,
            QuotationStatus.PendingApproval);
        return (_fx.ClientFor(manager), approval);
    }

    // ---------- TC-BE-020 ----------
    [Fact]
    public async Task Approve_Twice_SecondReturns409()
    {
        var (client, approval) = await PendingApprovalSeedAsync();

        var first = await client.PostAsJsonAsync(
            $"/api/approvals/{approval.Id}/approve", new { reviewedBy = Guid.NewGuid() });
        Assert.True(first.IsSuccessStatusCode,
            $"first approve failed: {(int)first.StatusCode} {await first.Content.ReadAsStringAsync()}");

        var second = await client.PostAsJsonAsync(
            $"/api/approvals/{approval.Id}/approve", new { reviewedBy = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ---------- TC-BE-021 ----------
    [Fact]
    public async Task Reject_EmptyReason_Returns400()
    {
        var (client, approval) = await PendingApprovalSeedAsync();

        var res = await client.PostAsJsonAsync(
            $"/api/approvals/{approval.Id}/reject",
            new { reviewedBy = Guid.NewGuid(), reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- TC-BE-022 ----------
    [Fact]
    public async Task MarkPaid_Twice_SecondReturns409()
    {
        var org = await _fx.SeedOrgAsync();
        var io = await _fx.SeedUserAsync(Roles.InventoryOfficer, org.Id);
        var vetUser = await _fx.SeedUserAsync(Roles.Veterinarian, org.Id);
        var vet = await _fx.SeedVetAsync(org.Id, vetUser.Id);
        var (_, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        var (_, appt) = await _fx.SeedBookedAppointmentAsync(pet.Id, vet.Id);
        var (quotation, _) = await _fx.SeedPendingApprovalAsync(appt.Id);
        var client = _fx.ClientFor(io);

        var first = await client.PostAsync(
            $"/api/quotations/{quotation.Id}/mark-paid", null);
        Assert.True(first.IsSuccessStatusCode,
            $"first mark-paid failed: {(int)first.StatusCode} {await first.Content.ReadAsStringAsync()}");

        var second = await client.PostAsync(
            $"/api/quotations/{quotation.Id}/mark-paid", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ---------- TC-BE-023 ----------
    [Fact]
    public async Task Consultation_InvalidTransitions_Return409()
    {
        var org = await _fx.SeedOrgAsync();
        // Consultation create requires an active vet free at the preferred hour.
        await _fx.SeedVetAsync(org.Id);
        var (ownerUser, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        var ownerClient = _fx.ClientFor(ownerUser);

        var created = await ownerClient.PostAsJsonAsync("/api/consultations", new
        {
            petId = pet.Id,
            ownerId = owner.Id,
            symptoms = "SE3110 symptom description for transition test",
            urgency = "Medium",
            organizationId = org.Id,
            preferredDate = DateTime.UtcNow.AddDays(3),
            preferredTime = TimeSpan.FromHours(9),
        });
        Assert.True(created.IsSuccessStatusCode,
            $"consultation create failed: {(int)created.StatusCode} {await created.Content.ReadAsStringAsync()}");
        var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetString();

        var submit1 = await ownerClient.PostAsync($"/api/consultations/{id}/submit", null);
        Assert.True(submit1.IsSuccessStatusCode,
            $"first submit failed: {(int)submit1.StatusCode}");

        var submit2 = await ownerClient.PostAsync($"/api/consultations/{id}/submit", null);
        var submit2Body = await submit2.Content.ReadAsStringAsync();
        Assert.True(submit2.StatusCode == HttpStatusCode.Conflict,
            $"expected 409, got {(int)submit2.StatusCode} body: {submit2Body}");

        // Assigning a still-Draft consultation is an invalid transition too.
        var draft = await _fx.SeedConsultationAsync(pet.Id, owner.Id, org.Id, "Draft");
        var manager = await _fx.SeedUserAsync(Roles.ClinicManager, org.Id);
        var vetUser = await _fx.SeedUserAsync(Roles.Veterinarian, org.Id);
        var vet = await _fx.SeedVetAsync(org.Id, vetUser.Id);
        var mgrClient = _fx.ClientFor(manager);
        var assign = await mgrClient.PostAsJsonAsync($"/api/consultations/{draft.Id}/assign", new
        {
            veterinarianId = vet.Id,
            date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            startTime = new TimeOnly(9, 0),
            endTime = new TimeOnly(9, 30),
        });
        Assert.Equal(HttpStatusCode.Conflict, assign.StatusCode);
    }

    // ---------- TC-BE-024 ----------
    [Fact]
    public async Task MalformedGuid_Returns404Not500()
    {
        var org = await _fx.SeedOrgAsync();
        var manager = await _fx.SeedUserAsync(Roles.ClinicManager, org.Id);
        var client = _fx.ClientFor(manager);

        var res = await client.GetAsync("/api/appointments/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ---------- TC-BE-025 ----------
    [Fact]
    public async Task MalformedJson_Returns400()
    {
        var org = await _fx.SeedOrgAsync();
        var manager = await _fx.SeedUserAsync(Roles.ClinicManager, org.Id);
        var client = _fx.ClientFor(manager);

        var res = await client.PostAsync("/api/appointments", JsonBody("{ not json"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- TC-AI-018 ----------
    [Fact]
    public async Task AgenticUnavailable_RunReturnsSafeError()
    {
        var org = await _fx.SeedOrgAsync();
        var manager = await _fx.SeedUserAsync(Roles.ClinicManager, org.Id);
        var (_, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        var cr = await _fx.SeedConsultationAsync(pet.Id, owner.Id, org.Id, "Submitted");
        var wf = await _fx.SeedWorkflowAsync(cr.Id, org.Id,
            PetCare.Domain.Constants.AgentWorkflowStatus.Created);

        // A second factory whose agentic client points at an unreachable port.
        using var deadFactory = new DeadAgenticFactory(_fx.ConnectionString);
        var client = deadFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", _fx.TokenFor(manager));

        var res = await client.PostAsync($"/api/agent-workflows/{wf.Id}/run", null);
        var body = await res.Content.ReadAsStringAsync();

        Assert.NotEqual(HttpStatusCode.InternalServerError, res.StatusCode);
        Assert.DoesNotContain("at System.", body);
        Assert.DoesNotContain("StackTrace", body);
        // Actual status recorded in the run evidence file.
        Console.WriteLine($"TC-AI-018 actual status: {(int)res.StatusCode} body: {body[..Math.Min(300, body.Length)]}");
    }

    // ---------- TC-E2E-004 ----------
    [Fact]
    public async Task Contract_ConsultationListShape()
    {
        var org = await _fx.SeedOrgAsync();
        var manager = await _fx.SeedUserAsync(Roles.ClinicManager, org.Id);
        var (_, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        await _fx.SeedConsultationAsync(pet.Id, owner.Id, org.Id, "Submitted");
        var client = _fx.ClientFor(manager);

        var res = await client.GetAsync("/api/consultations");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var items = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : doc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0, "expected at least one consultation");

        var first = items[0];
        // Field names the React ConsultationRequestApi type consumes.
        string[] expected = { "id", "petId", "ownerId", "symptoms", "status" };
        foreach (var field in expected)
        {
            Assert.True(first.TryGetProperty(field, out _),
                $"consultation list item missing field '{field}'");
        }
    }

    // ---------- TC-E2E-002 ----------
    [Fact]
    public async Task ClinicalChain_EndToEnd()
    {
        var org = await _fx.SeedOrgAsync();
        var vetUser = await _fx.SeedUserAsync(Roles.Veterinarian, org.Id);
        var vet = await _fx.SeedVetAsync(org.Id, vetUser.Id);
        var io = await _fx.SeedUserAsync(Roles.InventoryOfficer, org.Id);
        var (_, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        var (med, _) = await _fx.SeedStockAsync(org.Id, 10);
        var (_, appt) = await _fx.SeedBookedAppointmentAsync(pet.Id, vet.Id);

        var vetClient = _fx.ClientFor(vetUser);
        var ioClient = _fx.ClientFor(io);

        var exam = await vetClient.PostAsJsonAsync("/api/examinations", new
        {
            petId = pet.Id,
            veterinarianId = vet.Id,
            appointmentId = appt.Id,
            veterinarianCharge = 50m,
            symptoms = "SE3110 chain symptoms",
            notes = "chain exam",
            examinationDate = DateTime.UtcNow,
        });
        Assert.True(exam.IsSuccessStatusCode,
            $"examination failed: {(int)exam.StatusCode} {await exam.Content.ReadAsStringAsync()}");
        var examId = JsonDocument.Parse(await exam.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetGuid();

        var diag = await vetClient.PostAsJsonAsync("/api/diagnoses", new
        {
            examinationId = examId,
            conditionName = "SE3110 condition",
            description = "chain diagnosis",
            severity = "Moderate",
        });
        Assert.True(diag.IsSuccessStatusCode,
            $"diagnosis failed: {(int)diag.StatusCode} {await diag.Content.ReadAsStringAsync()}");
        var diagId = JsonDocument.Parse(await diag.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetGuid();

        var treatment = await vetClient.PostAsJsonAsync("/api/treatmentrecords", new
        {
            diagnosisId = diagId,
            procedureName = "SE3110 procedure",
            notes = "chain treatment",
        });
        Assert.True(treatment.IsSuccessStatusCode,
            $"treatment failed: {(int)treatment.StatusCode} {await treatment.Content.ReadAsStringAsync()}");
        var trDoc = JsonDocument.Parse(await treatment.Content.ReadAsStringAsync());
        var trId = trDoc.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Planned", trDoc.RootElement.GetProperty("status").GetString());

        var rx = await vetClient.PostAsJsonAsync("/api/prescriptions", new
        {
            treatmentRecordId = trId,
            items = new[]
            {
                new { medicineId = med.Id, dosage = "5mg", durationDays = 7, quantity = 2 },
            },
        });
        Assert.True(rx.IsSuccessStatusCode,
            $"prescription failed: {(int)rx.StatusCode} {await rx.Content.ReadAsStringAsync()}");
        var rxDoc = JsonDocument.Parse(await rx.Content.ReadAsStringAsync());
        var rxId = rxDoc.RootElement.ValueKind == JsonValueKind.Array
            ? rxDoc.RootElement[0].GetProperty("id").GetGuid()
            : rxDoc.RootElement.GetProperty("id").GetGuid();

        var reserve = await ioClient.PostAsJsonAsync("/api/medicine-reservations", new
        {
            medicineId = med.Id,
            quantity = 1,
            referenceType = "prescription",
            referenceId = rxId,
        });
        Assert.True(reserve.IsSuccessStatusCode,
            $"reservation failed: {(int)reserve.StatusCode} {await reserve.Content.ReadAsStringAsync()}");
    }
}

/// <summary>
/// A WebApplicationFactory whose agentic service base URL points at an
/// unreachable port, to exercise the agentic-unavailable path (TC-AI-018).
/// </summary>
public sealed class DeadAgenticFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public DeadAgenticFactory(string connectionString) =>
        _connectionString = connectionString;

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:PetCareDb", _connectionString);
        builder.UseSetting("Jwt:Key", Se3110Fixture.TestJwtKey);
        builder.UseSetting("Jwt:Issuer", Se3110Fixture.TestIssuer);
        builder.UseSetting("Jwt:Audience", Se3110Fixture.TestAudience);
        builder.UseSetting("AgenticService:BaseUrl", "http://localhost:59999");
        builder.UseSetting("AgenticService:TimeoutSeconds", "5");
    }
}
