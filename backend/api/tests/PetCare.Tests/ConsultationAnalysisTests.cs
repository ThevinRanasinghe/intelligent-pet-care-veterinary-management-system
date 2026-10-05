using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Agentic;
using PetCare.Application.DTOs.Agentic.Workflows;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetCare.Tests;

/// <summary>
/// Phase 2B: the consultation analysis endpoint delegates to the agentic
/// consultation agent through <see cref="IAgenticClient"/>. These tests
/// cover the agent call, DTO mapping, organization scoping, and safe
/// degradation ΓÇö and prove nothing is ever persisted.
/// </summary>
public class ConsultationAnalysisTests
{
    private sealed class FakeAgenticClient : IAgenticClient
    {
        public AgenticServiceResult Result { get; set; } = AgenticServiceResult.Failed("not_set");
        public string? LastConsultationId { get; private set; }
        public string? LastBearerToken { get; private set; }
        public int Calls { get; private set; }

        public Task<AgenticServiceResult> AnalyzeConsultationAsync(
            string consultationId, string? bearerToken, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastConsultationId = consultationId;
            LastBearerToken = bearerToken;
            return Task.FromResult(Result);
        }

        public Task<AgenticServiceResult> AnalyzeDiagnosisAsync(string id, string? token, CancellationToken ct = default) =>
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

    private static ConsultationRequest SeedConsultation(
        PetCareDbContext context, Guid? organizationId = null)
    {
        var consultation = new ConsultationRequest
        {
            Id = $"CON-{Guid.NewGuid():N}".Substring(0, 12).ToUpper(),
            PetId = "pet-1",
            OwnerId = "own-1",
            SymptomsDescription = "Limping on left hind leg",
            Status = "Submitted",
            OrganizationId = organizationId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ConsultationRequests.Add(consultation);
        context.SaveChanges();
        return consultation;
    }

    private static string AgentJson(
        string priority = "High",
        string consultationType = "Urgent",
        string nextStep = "Schedule an orthopedic examination") =>
        $@"{{
            ""consultationRequestId"":""whatever"",
            ""priority"":""{priority}"",
            ""consultationType"":""{consultationType}"",
            ""keyConcerns"":[{{""concern"":""Possible fracture"",""reason"":""Non-weight-bearing lameness""}}],
            ""recommendedChecks"":[""Physical orthopedic exam"",""Radiographs""],
            ""suggestedNextStep"":""{nextStep}"",
            ""disclaimer"":""Preliminary AI consultation assessment ΓÇö requires veterinary review and confirmation.""
        }}";

    [Fact]
    public async Task CallsAgentWithConsultationId_AndForwardsBearerToken()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetAnalysisAsync(consultation.Id, "Bearer tok123");

        Assert.Equal(1, agent.Calls);
        Assert.Equal(consultation.Id, agent.LastConsultationId);
        Assert.Equal("Bearer tok123", agent.LastBearerToken);
        Assert.Equal("agentic-ai", dto.Source);
        Assert.Equal(consultation.Id, dto.ConsultationRequestId);
        Assert.Equal("High", dto.Priority);
        Assert.Equal("Urgent", dto.ConsultationType);
        Assert.Single(dto.KeyConcerns);
        Assert.Equal("Possible fracture", dto.KeyConcerns[0].Concern);
        Assert.Contains("Radiographs", dto.RecommendedChecks);
        Assert.Equal("Schedule an orthopedic examination", dto.SuggestedNextStep);
        Assert.Contains("veterinary review", dto.Disclaimer);
    }

    [Fact]
    public async Task ScopedConsultationInOtherOrg_NotVisible_Unavailable_NoAgentCall()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context, organizationId: Guid.NewGuid());
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var scopedTenant = new TestTenantContext
        {
            IsOrganizationScoped = true,
            IsPlatformAdmin = false,
            OrganizationId = Guid.NewGuid()
        };
        var service = new ConsultationRequestService(context, scopedTenant, agent);

        var dto = await service.GetAnalysisAsync(consultation.Id);

        Assert.Equal("unavailable", dto.Source);
        Assert.Equal(0, agent.Calls);
    }

    [Fact]
    public async Task SameOrgConsultation_IsVisible()
    {
        using var context = NewContext();
        var orgId = Guid.NewGuid();
        var consultation = SeedConsultation(context, organizationId: orgId);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var scopedTenant = new TestTenantContext
        {
            IsOrganizationScoped = true,
            IsPlatformAdmin = false,
            OrganizationId = orgId
        };
        var service = new ConsultationRequestService(context, scopedTenant, agent);

        var dto = await service.GetAnalysisAsync(consultation.Id);

        Assert.Equal("agentic-ai", dto.Source);
        Assert.Equal(1, agent.Calls);
    }

    [Fact]
    public async Task MissingConsultation_NoAgentCall_Unavailable()
    {
        using var context = NewContext();
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetAnalysisAsync("CON-NOPE");

        Assert.Equal("unavailable", dto.Source);
        Assert.Equal(0, agent.Calls);
    }

    [Fact]
    public async Task AgentTimeout_ReturnsUnavailable()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Failed("agentic_timeout") };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetAnalysisAsync(consultation.Id);

        Assert.Equal("unavailable", dto.Source);
        Assert.Empty(dto.KeyConcerns);
        Assert.Empty(dto.RecommendedChecks);
    }

    [Fact]
    public async Task AgentServerError_ReturnsUnavailable()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Failed("agentic_error", 500) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetAnalysisAsync(consultation.Id);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task MalformedAgentJson_ReturnsUnavailable()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, "not json {") };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetAnalysisAsync(consultation.Id);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task MissingNextStep_ReturnsUnavailable()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(nextStep: " ")) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetAnalysisAsync(consultation.Id);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task InvalidPriority_NormalizedToModerate()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(priority: "CATASTROPHIC")) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetAnalysisAsync(consultation.Id);

        Assert.Equal("Moderate", dto.Priority);
    }

    [Fact]
    public async Task InvalidConsultationType_NormalizedToRoutine()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(consultationType: "WEIRD")) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetAnalysisAsync(consultation.Id);

        Assert.Equal("Routine", dto.ConsultationType);
    }

    [Fact]
    public async Task NoAgenticClient_ReturnsUnavailable()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var dto = await service.GetAnalysisAsync(consultation.Id);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task Analysis_NeverPersistsAnything()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var before = context.ChangeTracker.Entries().Count();
        await service.GetAnalysisAsync(consultation.Id, "Bearer tok");

        // Read-only: no entities became Added/Modified during the call.
        Assert.DoesNotContain(context.ChangeTracker.Entries(),
            e => e.State == EntityState.Added || e.State == EntityState.Modified);
        Assert.Equal(before, context.ChangeTracker.Entries().Count());
    }
}
