using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Agentic;
using PetCare.Application.DTOs.Agentic.Workflows;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetCare.Tests;

/// <summary>
/// Phase 2A: the recommendations endpoint now delegates to the agentic
/// diagnosis agent through <see cref="IAgenticClient"/>. These tests cover
/// the agent call, DTO mapping, org-scoped medicine-name resolution, and
/// safe degradation ΓÇö and prove nothing is ever persisted.
/// </summary>
public class ExaminationRecommendationTests
{
    private sealed class FakeAgenticClient : IAgenticClient
    {
        public AgenticServiceResult Result { get; set; } = AgenticServiceResult.Failed("not_set");
        public string? LastExaminationId { get; private set; }
        public string? LastBearerToken { get; private set; }
        public int Calls { get; private set; }

        public Task<AgenticServiceResult> AnalyzeDiagnosisAsync(
            string examinationId, string? bearerToken, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastExaminationId = examinationId;
            LastBearerToken = bearerToken;
            return Task.FromResult(Result);
        }

        public Task<AgenticServiceResult> AnalyzeConsultationAsync(string id, string? token, CancellationToken ct = default) =>
            Task.FromResult(AgenticServiceResult.Failed("not_implemented"));

        public Task<AgenticServiceResult> PlanSchedulingAsync(string id, string? token, CancellationToken ct = default) =>
            Task.FromResult(AgenticServiceResult.Failed("not_implemented"));

        public Task<AgenticServiceResult> PlanInventoryAsync(string id, string? token, CancellationToken ct = default) =>
            Task.FromResult(AgenticServiceResult.Failed("not_implemented"));

        public Task<AgenticServiceResult> RunWorkflowAsync(
            string workflowId, WorkflowRunPayload payload, string? bearerToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(AgenticServiceResult.Failed("not_implemented"));

        public Task<AgenticServiceResult> ResumeWorkflowAsync(
            string workflowId, WorkflowResumePayload payload, string? bearerToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(AgenticServiceResult.Failed("not_implemented"));

        public Task<AgenticServiceResult> AdvanceWorkflowAsync(
            string workflowId, WorkflowAdvancePayload payload, string? bearerToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(AgenticServiceResult.Failed("not_implemented"));
    }

    private static PetCareDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Examination SeedExamination(PetCareDbContext context, Guid? vetOrgId = null)
    {
        var vet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Test", OrganizationId = vetOrgId };
        context.Veterinarians.Add(vet);
        var exam = new Examination
        {
            Id = Guid.NewGuid(),
            PetId = Guid.NewGuid().ToString(),
            VeterinarianId = vet.Id,
            Veterinarian = vet,
            Symptoms = "vomiting and lethargy",
            ExaminationDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        context.Examinations.Add(exam);
        context.SaveChanges();
        return exam;
    }

    private static Medicine SeedMedicine(PetCareDbContext context, string name, string strength = "", string form = "", Guid? orgId = null)
    {
        var med = new Medicine
        {
            Id = Guid.NewGuid(),
            Name = name,
            Strength = strength,
            DosageForm = form,
            Category = "General",
            Status = MedicineStatus.Active,
            OrganizationId = orgId
        };
        context.Medicines.Add(med);
        context.SaveChanges();
        return med;
    }

    private static string AgentJson(string condition = "Acute gastroenteritis", string severity = "High", params string[] medicines)
    {
        var medsJson = string.Join(",", medicines.Select(m =>
            $"{{\"medicineName\":\"{m}\",\"suggestedDosage\":\"1 tablet\",\"suggestedDurationDays\":5}}"));
        return $@"{{
            ""suspectedCondition"":""{condition}"",
            ""recommendedSeverity"":""{severity}"",
            ""rationale"":""AI rationale"",
            ""recommendedProcedures"":[""Procedure A""],
            ""suggestedMedicines"":[{medsJson}],
            ""precautionaryNotes"":[""Monitor hydration""]
        }}";
    }

    [Fact]
    public async Task CallsAgentWithExaminationId_AndForwardsBearerToken()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id, "Bearer tok123");

        Assert.Equal(1, agent.Calls);
        Assert.Equal(exam.Id.ToString(), agent.LastExaminationId);
        Assert.Equal("Bearer tok123", agent.LastBearerToken);
        Assert.Equal("agentic-ai", dto.Source);
        Assert.Equal("Acute gastroenteritis", dto.SuspectedCondition);
        Assert.Equal("High", dto.RecommendedSeverity);
        Assert.Equal("AI rationale", dto.Rationale);
        Assert.Contains("Procedure A", dto.RecommendedProcedures);
        Assert.Contains("Monitor hydration", dto.PrecautionaryNotes);
    }

    [Fact]
    public async Task UniqueMedicineName_ResolvesRealCatalogueId()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        var med = SeedMedicine(context, "Amoxicillin");
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(medicines: "Amoxicillin")) };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Single(dto.SuggestedMedicines);
        Assert.Equal(med.Id, dto.SuggestedMedicines[0].MedicineId);
        Assert.Equal("Amoxicillin", dto.SuggestedMedicines[0].MedicineName);
    }

    [Fact]
    public async Task MedicineName_CaseAndWhitespaceNormalized()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        var med = SeedMedicine(context, "Amoxicillin");
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(medicines: "  AMOXICILLIN ")) };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Equal(med.Id, dto.SuggestedMedicines[0].MedicineId);
    }

    [Fact]
    public async Task MedicineNameWithStrength_MatchesCatalogueVariant()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        var med = SeedMedicine(context, "Amoxicillin", strength: "250mg");
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(medicines: "Amoxicillin 250mg")) };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Equal(med.Id, dto.SuggestedMedicines[0].MedicineId);
    }

    [Fact]
    public async Task UnknownMedicine_NoFakeId_NameStillAdvisory()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(medicines: "Unobtainium")) };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Single(dto.SuggestedMedicines);
        Assert.Null(dto.SuggestedMedicines[0].MedicineId);
        Assert.Equal("Unobtainium", dto.SuggestedMedicines[0].MedicineName);
    }

    [Fact]
    public async Task AmbiguousMedicineName_NoFakeId()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        SeedMedicine(context, "Amoxicillin", strength: "250mg");
        SeedMedicine(context, "Amoxicillin", strength: "500mg");
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(medicines: "Amoxicillin")) };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Null(dto.SuggestedMedicines[0].MedicineId);
    }

    [Fact]
    public async Task MedicineInOtherOrganization_NotResolved()
    {
        using var context = NewContext();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var exam = SeedExamination(context, vetOrgId: orgA);
        SeedMedicine(context, "Amoxicillin", orgId: orgB);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(medicines: "Amoxicillin")) };
        var scopedTenant = new TestTenantContext { IsOrganizationScoped = true, IsPlatformAdmin = false, OrganizationId = orgA };
        var service = new ExaminationService(context, scopedTenant, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Single(dto.SuggestedMedicines);
        Assert.Null(dto.SuggestedMedicines[0].MedicineId);
    }

    [Fact]
    public async Task AgentTimeout_ReturnsUnavailable_NoAgentData()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Failed("agentic_timeout") };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Equal("unavailable", dto.Source);
        Assert.Empty(dto.SuggestedMedicines);
        Assert.Empty(dto.RecommendedProcedures);
    }

    [Fact]
    public async Task AgentServerError_ReturnsUnavailable()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Failed("agentic_server_error", 500) };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task MalformedAgentJson_ReturnsUnavailable()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, "not json {") };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task EmptyMedicineList_Safe()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Equal("agentic-ai", dto.Source);
        Assert.Empty(dto.SuggestedMedicines);
    }

    [Fact]
    public async Task InvalidSeverity_NormalizedToModerate()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(severity: "EXTREME")) };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Equal("Moderate", dto.RecommendedSeverity);
    }

    [Fact]
    public async Task MissingExamination_NoAgentCall_Unavailable()
    {
        using var context = NewContext();
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetRecommendationsAsync(Guid.NewGuid());

        Assert.Equal("unavailable", dto.Source);
        Assert.Equal(0, agent.Calls);
    }

    [Fact]
    public async Task Recommendations_NeverPersistAnything()
    {
        using var context = NewContext();
        var exam = SeedExamination(context);
        SeedMedicine(context, "Amoxicillin");
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(medicines: "Amoxicillin")) };
        var service = new ExaminationService(context, TestTenantContext.Unscoped, agent);

        var before = context.ChangeTracker.Entries().Count();
        await service.GetRecommendationsAsync(exam.Id);

        Assert.Empty(context.Diagnoses);
        Assert.Empty(context.TreatmentRecords);
        Assert.Empty(context.Prescriptions);
        // Read-only: no entities became Added/Modified during the call.
        Assert.DoesNotContain(context.ChangeTracker.Entries(),
            e => e.State == EntityState.Added || e.State == EntityState.Modified);
        Assert.Equal(before, context.ChangeTracker.Entries().Count());
    }

    [Fact]
    public async Task ScopedExaminationInOtherOrg_NotVisible_Unavailable()
    {
        using var context = NewContext();
        var exam = SeedExamination(context, vetOrgId: Guid.NewGuid());
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var scopedTenant = new TestTenantContext { IsOrganizationScoped = true, IsPlatformAdmin = false, OrganizationId = Guid.NewGuid() };
        var service = new ExaminationService(context, scopedTenant, agent);

        var dto = await service.GetRecommendationsAsync(exam.Id);

        Assert.Equal("unavailable", dto.Source);
        Assert.Equal(0, agent.Calls);
    }
}
