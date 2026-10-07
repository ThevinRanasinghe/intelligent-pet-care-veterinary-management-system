using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PetCare.Domain.Constants;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Se3110.Tests;

/// <summary>
/// SE3110 security discovery tests (catalogue TC-SEC-001..012): authn,
/// token integrity, role authorization, owner isolation and cross-org
/// tenant isolation. Assertions are taken from the catalogue's expected
/// values — failures are recorded, not fixed.
/// </summary>
public class SecurityTests : IClassFixture<Se3110Fixture>
{
    private readonly Se3110Fixture _fx;

    public SecurityTests(Se3110Fixture fx) => _fx = fx;

    private static readonly string ObsDir = Path.Combine(
        "D:", "SLIIT", "3year", "1_sem",
        "SE3110 - Quality Management in Software Engineering",
        "Assignment", "raw-results", "test-output");

    // ---------- TC-SEC-001 ----------
    [Theory]
    [InlineData("/api/appointments")]
    [InlineData("/api/pets")]
    [InlineData("/api/consultations")]
    public async Task Unauthenticated_ProtectedEndpoints_Return401(string route)
    {
        var client = _fx.CreateClient();
        var res = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---------- TC-SEC-002 ----------
    [Theory]
    [InlineData(true)]   // valid token with flipped last signature char
    [InlineData(false)]  // "garbage"
    public async Task TamperedToken_Returns401(bool useTamperedSignature)
    {
        var user = await _fx.SeedUserAsync(Roles.ClinicManager);
        var token = _fx.TokenFor(user);
        var attacked = useTamperedSignature
            ? FlipLastSignatureChar(token)
            : "garbage";

        var client = _fx.ClientForToken(attacked);
        var res = await client.GetAsync("/api/appointments");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private static string FlipLastSignatureChar(string jwt)
    {
        var parts = jwt.Split('.');
        var sig = parts[^1].ToCharArray();
        sig[^1] = sig[^1] == 'A' ? 'B' : 'A';
        parts[^1] = new string(sig);
        return string.Join('.', parts);
    }

    // ---------- TC-SEC-003 ----------
    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var user = await _fx.SeedUserAsync(Roles.ClinicManager);
        var client = _fx.ClientFor(user, expiryMinutes: -5);

        var res = await client.GetAsync("/api/appointments");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---------- TC-SEC-004 ----------
    [Fact]
    public async Task PetOwner_CreateAppointment_Returns403()
    {
        var (ownerUser, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        var client = _fx.ClientFor(ownerUser);

        var res = await client.PostAsJsonAsync("/api/appointments", new
        {
            petId = pet.Id,
            veterinarianId = Guid.NewGuid(),
            appointmentSlotId = Guid.NewGuid(),
            scheduledStart = DateTime.UtcNow.AddDays(3),
            scheduledEnd = DateTime.UtcNow.AddDays(3).AddMinutes(30),
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ---------- TC-SEC-005 ----------
    [Fact]
    public async Task Vet_Approve_Returns403()
    {
        var org = await _fx.SeedOrgAsync();
        var vetUser = await _fx.SeedUserAsync(Roles.Veterinarian, org.Id);
        var vet = await _fx.SeedVetAsync(org.Id, vetUser.Id);
        var (_, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        var (_, appt) = await _fx.SeedBookedAppointmentAsync(pet.Id, vet.Id);
        var (_, approval) = await _fx.SeedPendingApprovalAsync(appt.Id);
        var client = _fx.ClientFor(vetUser);

        // Quotation approval route.
        var res1 = await client.PostAsJsonAsync(
            $"/api/approvals/{approval.Id}/approve", new { reviewedBy = vetUser.Id });
        Assert.Equal(HttpStatusCode.Forbidden, res1.StatusCode);

        // Agent-workflow approval route.
        var res2 = await client.PostAsJsonAsync(
            $"/api/agent-workflows/{Guid.NewGuid()}/approve", new { comments = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, res2.StatusCode);
    }

    // ---------- TC-SEC-006 ----------
    [Fact]
    public async Task InventoryOfficer_GetPets_Returns403()
    {
        var org = await _fx.SeedOrgAsync();
        var io = await _fx.SeedUserAsync(Roles.InventoryOfficer, org.Id);
        var client = _fx.ClientFor(io);

        var res = await client.GetAsync("/api/pets");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ---------- TC-SEC-007 ----------
    [Fact]
    public async Task OwnerA_AccessOwnerBPet_Masked404()
    {
        var (ownerAUser, _) = await _fx.SeedOwnerAsync();
        var (ownerBUser, ownerB) = await _fx.SeedOwnerAsync();
        var petB = await _fx.SeedPetAsync(ownerB.Id, archived: false);
        var clientA = _fx.ClientFor(ownerAUser);

        var get = await clientA.GetAsync($"/api/pets/{petB.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        var put = await clientA.PutAsJsonAsync($"/api/pets/{petB.Id}", new
        {
            name = "Hijacked",
            species = "Cat",
        });
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

        var del = await clientA.DeleteAsync($"/api/pets/{petB.Id}");
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);

        // Re-read as owner B: the pet must be untouched.
        var clientB = _fx.ClientFor(ownerBUser);
        var reRead = await clientB.GetAsync($"/api/pets/{petB.Id}");
        Assert.Equal(HttpStatusCode.OK, reRead.StatusCode);
        var doc = JsonDocument.Parse(await reRead.Content.ReadAsStringAsync());
        Assert.Equal(petB.Name, doc.RootElement.GetProperty("name").GetString());
    }

    // ---------- TC-SEC-008 ----------
    [Fact]
    public async Task OwnerA_ListOwnerBPets_Returns403()
    {
        var (ownerAUser, _) = await _fx.SeedOwnerAsync();
        var (_, ownerB) = await _fx.SeedOwnerAsync();
        var clientA = _fx.ClientFor(ownerAUser);

        var res = await clientA.GetAsync($"/api/pets/owner/{ownerB.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ---------- TC-SEC-009 ----------
    [Fact]
    public async Task OwnerA_GetAppointmentOfOwnerB_Returns404()
    {
        var org = await _fx.SeedOrgAsync();
        var vetUser = await _fx.SeedUserAsync(Roles.Veterinarian, org.Id);
        var vet = await _fx.SeedVetAsync(org.Id, vetUser.Id);
        var (ownerAUser, _) = await _fx.SeedOwnerAsync();
        var (_, ownerB) = await _fx.SeedOwnerAsync();
        var petB = await _fx.SeedPetAsync(ownerB.Id);
        var (_, apptB) = await _fx.SeedBookedAppointmentAsync(petB.Id, vet.Id);
        var clientA = _fx.ClientFor(ownerAUser);

        var res = await clientA.GetAsync($"/api/appointments/{apptB.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ---------- TC-SEC-010 ----------
    [Fact]
    public async Task CrossOrg_ManagerB_CannotSeeOrgA()
    {
        var orgA = await _fx.SeedOrgAsync();
        var orgB = await _fx.SeedOrgAsync();
        var vetUserA = await _fx.SeedUserAsync(Roles.Veterinarian, orgA.Id);
        var vetA = await _fx.SeedVetAsync(orgA.Id, vetUserA.Id);
        var (_, ownerA) = await _fx.SeedOwnerAsync();
        var petA = await _fx.SeedPetAsync(ownerA.Id);
        var (_, apptA) = await _fx.SeedBookedAppointmentAsync(petA.Id, vetA.Id);
        var medA = await _fx.SeedMedicineAsync(orgA.Id);

        var managerB = await _fx.SeedUserAsync(Roles.ClinicManager, orgB.Id);
        var clientB = _fx.ClientFor(managerB);

        var appt = await clientB.GetAsync($"/api/appointments/{apptA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, appt.StatusCode);

        var med = await clientB.GetAsync($"/api/medicines/{medA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, med.StatusCode);

        var list = await clientB.GetAsync("/api/appointments");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain(apptA.Id.ToString(), body);
    }

    // ---------- TC-SEC-011 (observation) ----------
    [Fact]
    public async Task CrossOrg_ManagerB_ListPets_RecordBehaviour()
    {
        var orgA = await _fx.SeedOrgAsync();
        var orgB = await _fx.SeedOrgAsync();
        var (_, ownerA) = await _fx.SeedOwnerAsync();
        var petA = await _fx.SeedPetAsync(ownerA.Id);

        var managerB = await _fx.SeedUserAsync(Roles.ClinicManager, orgB.Id);
        var clientB = _fx.ClientFor(managerB);

        var res = await clientB.GetAsync("/api/pets");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var total = doc.RootElement.GetArrayLength();
        var orgAPetVisible = body.Contains(petA.Id);

        Directory.CreateDirectory(ObsDir);
        await File.WriteAllTextAsync(Path.Combine(ObsDir, "OBS-TC-SEC-011.txt"),
            $"TC-SEC-011 observation ({DateTimeOffset.Now:o})\n" +
            $"GET /api/pets as ClinicManager of org {orgB.Id}\n" +
            $"Total pets returned: {total}\n" +
            $"Pet owned by unrelated owner ({petA.Id}) visible in list: {orgAPetVisible}\n" +
            $"Note: pets have no OrganizationId column; manager list scope is recorded as observed.\n");
    }

    // ---------- TC-SEC-012 (observation) ----------
    [Fact]
    public async Task SuperAdmin_ApproveWorkflow_RecordBehaviour()
    {
        var org = await _fx.SeedOrgAsync();
        var (_, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        var cr = await _fx.SeedConsultationAsync(pet.Id, owner.Id, org.Id, "Submitted");
        var wf = await _fx.SeedWorkflowAsync(cr.Id, org.Id,
            PetCare.Domain.Constants.AgentWorkflowStatus.PendingManagerApproval);
        var admin = await _fx.SeedUserAsync(Roles.SuperAdmin);
        var client = _fx.ClientFor(admin);

        var res = await client.PostAsJsonAsync(
            $"/api/agent-workflows/{wf.Id}/approve", new { comments = "SE3110 probe" });
        var body = await res.Content.ReadAsStringAsync();

        Directory.CreateDirectory(ObsDir);
        await File.WriteAllTextAsync(Path.Combine(ObsDir, "OBS-TC-SEC-012.txt"),
            $"TC-SEC-012 observation ({DateTimeOffset.Now:o})\n" +
            $"POST /api/agent-workflows/{wf.Id}/approve as SuperAdmin\n" +
            $"Status: {(int)res.StatusCode} {res.StatusCode}\n" +
            $"Body: {body}\n");

        Assert.True((int)res.StatusCode is >= 400 and < 500,
            $"expected a 4xx, got {(int)res.StatusCode}");
    }
}
