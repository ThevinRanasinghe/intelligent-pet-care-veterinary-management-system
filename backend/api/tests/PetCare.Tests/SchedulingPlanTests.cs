using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Agentic;
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
/// Phase 2C: the scheduling-plan endpoint delegates to the agentic
/// scheduling agent through <see cref="IAgenticClient"/>. These tests
/// cover the agent call, DTO mapping, organization scoping, and safe
/// degradation — and prove nothing is ever persisted or booked.
/// </summary>
public class SchedulingPlanTests
{
    private sealed class FakeAgenticClient : IAgenticClient
    {
        public AgenticServiceResult Result { get; set; } = AgenticServiceResult.Failed("not_set");
        public string? LastRequestId { get; private set; }
        public string? LastBearerToken { get; private set; }
        public int Calls { get; private set; }

        public Task<AgenticServiceResult> PlanSchedulingAsync(
            string requestId, string? bearerToken, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequestId = requestId;
            LastBearerToken = bearerToken;
            return Task.FromResult(Result);
        }

        public Task<AgenticServiceResult> AnalyzeConsultationAsync(string id, string? token, CancellationToken ct = default) =>
            Task.FromResult(AgenticServiceResult.Failed("not_implemented"));

        public Task<AgenticServiceResult> AnalyzeDiagnosisAsync(string id, string? token, CancellationToken ct = default) =>
            Task.FromResult(AgenticServiceResult.Failed("not_implemented"));

        public Task<AgenticServiceResult> PlanInventoryAsync(string id, string? token, CancellationToken ct = default) =>
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
            SymptomsDescription = "Vomiting since yesterday",
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
        string confidence = "High",
        string planningNotes = "Requested slot is free for Dr. Perera") =>
        $@"{{
            ""requestId"":""whatever"",
            ""recommendedAppointment"":{{
                ""appointmentSlotId"":""slot-1"",
                ""veterinarianId"":""vet-1"",
                ""date"":""2026-10-07"",
                ""startTime"":""10:00"",
                ""endTime"":""11:00"",
                ""branch"":""Main"",
                ""reason"":""Requested slot is free""
            }},
            ""alternativeSlots"":[{{
                ""appointmentSlotId"":""slot-2"",
                ""veterinarianId"":""vet-2"",
                ""date"":""2026-10-07"",
                ""startTime"":""11:00"",
                ""endTime"":""12:00"",
                ""branch"":""Main"",
                ""reason"":""Next free slot""
            }}],
            ""quotationProposal"":{{
                ""budget"":5000,
                ""items"":[{{
                    ""category"":""Consultation"",
                    ""description"":""Standard consultation"",
                    ""quantity"":1,
                    ""unitPrice"":2500,
                    ""reason"":""Initial visit""
                }}],
                ""estimatedSubtotal"":2500,
                ""estimatedTotal"":2500,
                ""withinBudget"":true
            }},
            ""validationSummary"":{{
                ""slotFound"":true,
                ""veterinarianAvailable"":true,
                ""noKnownConflict"":true,
                ""withinRequestedTime"":true,
                ""withinBudget"":true
            }},
            ""confidence"":""{confidence}"",
            ""planningNotes"":""{planningNotes}"",
            ""disclaimer"":""AI-generated scheduling proposal""
        }}";

    [Fact]
    public async Task CallsAgentWithRequestId_AndForwardsBearerToken()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetSchedulingPlanAsync(consultation.Id, "Bearer tok123");

        Assert.Equal(1, agent.Calls);
        Assert.Equal(consultation.Id, agent.LastRequestId);
        Assert.Equal("Bearer tok123", agent.LastBearerToken);
        Assert.Equal("agentic-ai", dto.Source);
        Assert.Equal(consultation.Id, dto.RequestId);
        Assert.NotNull(dto.RecommendedAppointment);
        Assert.Equal("vet-1", dto.RecommendedAppointment!.VeterinarianId);
        Assert.Equal("2026-10-07", dto.RecommendedAppointment.Date);
        Assert.Single(dto.AlternativeSlots);
        Assert.Equal("High", dto.Confidence);
        Assert.True(dto.ValidationSummary.NoKnownConflict);
        Assert.NotNull(dto.QuotationProposal);
        Assert.Equal(2500m, dto.QuotationProposal!.EstimatedTotal);
        Assert.Contains("scheduling system", dto.Disclaimer);
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

        var dto = await service.GetSchedulingPlanAsync(consultation.Id);

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

        var dto = await service.GetSchedulingPlanAsync(consultation.Id);

        Assert.Equal("agentic-ai", dto.Source);
        Assert.Equal(1, agent.Calls);
    }

    [Fact]
    public async Task MissingConsultation_NoAgentCall_Unavailable()
    {
        using var context = NewContext();
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetSchedulingPlanAsync("CON-NOPE");

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

        var dto = await service.GetSchedulingPlanAsync(consultation.Id);

        Assert.Equal("unavailable", dto.Source);
        Assert.Null(dto.RecommendedAppointment);
        Assert.Empty(dto.AlternativeSlots);
    }

    [Fact]
    public async Task AgentServerError_ReturnsUnavailable()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Failed("agentic_error", 500) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetSchedulingPlanAsync(consultation.Id);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task MalformedAgentJson_ReturnsUnavailable()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, "not json {") };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetSchedulingPlanAsync(consultation.Id);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task MissingPlanningNotes_ReturnsUnavailable()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(planningNotes: " ")) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetSchedulingPlanAsync(consultation.Id);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task InvalidConfidence_NormalizedToLow()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson(confidence: "ABSOLUTE")) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetSchedulingPlanAsync(consultation.Id);

        Assert.Equal("Low", dto.Confidence);
    }

    [Fact]
    public async Task IncompleteAppointment_IsDropped()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        // Agent returns an appointment missing the fields that identify a
        // slot (empty date/startTime) — it must be dropped, not surfaced.
        var json = @"{
            ""requestId"":""whatever"",
            ""recommendedAppointment"":{
                ""appointmentSlotId"":"""",""veterinarianId"":"""",
                ""date"":"""",""startTime"":"""",""endTime"":"""",
                ""branch"":"""",""reason"":""""
            },
            ""alternativeSlots"":[],
            ""quotationProposal"":null,
            ""validationSummary"":{
                ""slotFound"":false,""veterinarianAvailable"":false,
                ""noKnownConflict"":false,""withinRequestedTime"":false,
                ""withinBudget"":false
            },
            ""confidence"":""Low"",
            ""planningNotes"":""No usable slot proposal"",
            ""disclaimer"":""AI-generated scheduling proposal""
        }";
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, json) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var dto = await service.GetSchedulingPlanAsync(consultation.Id);

        Assert.Equal("agentic-ai", dto.Source);
        Assert.Null(dto.RecommendedAppointment);
    }

    [Fact]
    public async Task NoAgenticClient_ReturnsUnavailable()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var dto = await service.GetSchedulingPlanAsync(consultation.Id);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task SchedulingPlan_NeverPersistsAnything()
    {
        using var context = NewContext();
        var consultation = SeedConsultation(context);
        var agent = new FakeAgenticClient { Result = AgenticServiceResult.Ok(200, AgentJson()) };
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped, agent);

        var before = context.ChangeTracker.Entries().Count();
        await service.GetSchedulingPlanAsync(consultation.Id, "Bearer tok");

        // Read-only: no entities became Added/Modified during the call —
        // the AI never books, assigns, or mutates business records.
        Assert.DoesNotContain(context.ChangeTracker.Entries(),
            e => e.State == EntityState.Added || e.State == EntityState.Modified);
        Assert.Equal(before, context.ChangeTracker.Entries().Count());
    }
}
