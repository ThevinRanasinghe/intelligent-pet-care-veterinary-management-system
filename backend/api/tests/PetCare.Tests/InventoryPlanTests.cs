using PetCare.Application.DTOs;
using PetCare.Application.DTOs.Agentic;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.DTOs.Inventory;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetCare.Tests;

/// <summary>
/// Phase 2D: the inventory-plan endpoint delegates to the agentic
/// inventory agent through <see cref="IAgenticClient.PlanInventoryAsync"/>.
/// These tests cover the agent call, DTO mapping, organization scoping
/// (via the prescription repository's tenant scoping), and safe
/// degradation — and prove nothing is ever issued, reserved, or persisted.
/// </summary>
public class InventoryPlanTests
{
    private static readonly Guid RecordId = Guid.NewGuid();

    private sealed class FakeAgenticClient : IAgenticClient
    {
        public AgenticServiceResult Result { get; set; } = AgenticServiceResult.Failed("not_set");
        public string? LastRequestId { get; private set; }
        public string? LastBearerToken { get; private set; }
        public int Calls { get; private set; }

        public Task<AgenticServiceResult> PlanInventoryAsync(
            string treatmentRecordId, string? bearerToken, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequestId = treatmentRecordId;
            LastBearerToken = bearerToken;
            return Task.FromResult(Result);
        }

        public Task<AgenticServiceResult> AnalyzeConsultationAsync(string id, string? token, CancellationToken ct = default) =>
            Task.FromResult(AgenticServiceResult.Failed("not_implemented"));

        public Task<AgenticServiceResult> AnalyzeDiagnosisAsync(string id, string? token, CancellationToken ct = default) =>
            Task.FromResult(AgenticServiceResult.Failed("not_implemented"));

        public Task<AgenticServiceResult> PlanSchedulingAsync(string id, string? token, CancellationToken ct = default) =>
            Task.FromResult(AgenticServiceResult.Failed("not_implemented"));
    }

    /// <summary>Repo stub: scoping decided by the caller-supplied predicate.</summary>
    private sealed class FakePrescriptionRepository : IPrescriptionRepository
    {
        public bool RecordInScope { get; set; } = true;

        public Task<bool> ExistsForTreatmentRecordAsync(Guid treatmentRecordId, CancellationToken ct = default) =>
            Task.FromResult(RecordInScope && treatmentRecordId == RecordId);

        public Task<Prescription?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Prescription?>(null);

        public Task<IReadOnlyList<Prescription>> GetRequestsAsync(MedicineRequestStatus? status, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Prescription>>(new List<Prescription>());

        public Task<IReadOnlyList<Prescription>> GetByVeterinarianAsync(Guid veterinarianId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Prescription>>(new List<Prescription>());
    }

    /// <summary>
    /// Proves no inventory mutation happens: any call here fails the test.
    /// </summary>
    private sealed class GuardedInventoryService : IInventoryService
    {
        public int Calls { get; private set; }

        private Task<T> Guard<T>()
        {
            Calls++;
            throw new InvalidOperationException("AI analysis must not touch inventory.");
        }

        public Task<MedicineResponse> CreateMedicineAsync(CreateMedicineRequest r, CancellationToken ct = default) => Guard<MedicineResponse>();
        public Task<MedicineResponse?> GetMedicineByIdAsync(Guid id, CancellationToken ct = default) => Guard<MedicineResponse?>();
        public Task<PagedResult<MedicineResponse>> SearchMedicinesAsync(SearchMedicinesRequest r, CancellationToken ct = default) => Guard<PagedResult<MedicineResponse>>();
        public Task<MedicineBatchResponse> ReceiveStockAsync(Guid id, ReceiveStockRequest r, Guid u, CancellationToken ct = default) => Guard<MedicineBatchResponse>();
        public Task<ReservationResponse> ReserveMedicineAsync(ReserveMedicineRequest r, Guid u, CancellationToken ct = default) => Guard<ReservationResponse>();
        public Task<IReadOnlyList<ReservationResponse>> GetReservationsAsync(CancellationToken ct = default) => Guard<IReadOnlyList<ReservationResponse>>();
        public Task CancelReservationAsync(Guid id, Guid u, CancellationToken ct = default) => Guard<object>();
        public Task DispenseReservationAsync(Guid id, Guid u, CancellationToken ct = default) => Guard<object>();
        public Task<IReadOnlyList<MedicineResponse>> GetLowStockAsync(CancellationToken ct = default) => Guard<IReadOnlyList<MedicineResponse>>();
        public Task<IReadOnlyList<MedicineBatchResponse>> GetExpiringSoonAsync(int d, CancellationToken ct = default) => Guard<IReadOnlyList<MedicineBatchResponse>>();
        public Task<IReadOnlyList<MedicineBatchResponse>> GetBatchesForMedicineAsync(Guid id, CancellationToken ct = default) => Guard<IReadOnlyList<MedicineBatchResponse>>();
        public Task<IReadOnlyList<InventoryTransactionResponse>> GetTransactionsAsync(Guid id, CancellationToken ct = default) => Guard<IReadOnlyList<InventoryTransactionResponse>>();
        public Task<SupplierResponse> CreateSupplierAsync(CreateSupplierRequest r, CancellationToken ct = default) => Guard<SupplierResponse>();
        public Task<IReadOnlyList<SupplierResponse>> GetSuppliersAsync(CancellationToken ct = default) => Guard<IReadOnlyList<SupplierResponse>>();
        public Task<SupplierResponse?> GetSupplierByIdAsync(Guid id, CancellationToken ct = default) => Guard<SupplierResponse?>();
    }

    private sealed class GuardedBillingService : IBillingService
    {
        public int Calls { get; private set; }

        private Task<T> Guard<T>()
        {
            Calls++;
            throw new InvalidOperationException("AI analysis must not touch billing.");
        }

        public Task<IReadOnlyList<QuotationResponse>> GetQuotationsAsync(CancellationToken ct = default) => Guard<IReadOnlyList<QuotationResponse>>();
        public Task<QuotationResponse?> GetQuotationByIdAsync(Guid id, CancellationToken ct = default) => Guard<QuotationResponse?>();
        public Task<QuotationResponse> CreateQuotationAsync(CreateQuotationRequest r, CancellationToken ct = default) => Guard<QuotationResponse>();
        public Task<QuotationResponse> UpdateQuotationAsync(Guid id, UpdateQuotationRequest r, CancellationToken ct = default) => Guard<QuotationResponse>();
        public Task<QuotationResponse> CalculateQuotationAsync(Guid id, CancellationToken ct = default) => Guard<QuotationResponse>();
        public Task<QuotationResponse> SubmitQuotationForApprovalAsync(Guid id, CancellationToken ct = default) => Guard<QuotationResponse>();
        public Task<QuotationResponse> FinalizeQuotationAsync(Guid id, CancellationToken ct = default) => Guard<QuotationResponse>();
        public Task<QuotationResponse?> GenerateOrRefreshBillForExaminationAsync(Guid id, Guid u, CancellationToken ct = default) => Guard<QuotationResponse?>();
        public Task<QuotationResponse> MarkPaidAsync(Guid id, CancellationToken ct = default) => Guard<QuotationResponse>();
        public Task<IReadOnlyList<QuotationResponse>> GetQuotationsForOwnerAsync(string o, CancellationToken ct = default) => Guard<IReadOnlyList<QuotationResponse>>();
    }

    private sealed class GuardedUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            Saves++;
            return Task.FromResult(0);
        }
    }

    private static (MedicineRequestService service, FakeAgenticClient agent, GuardedInventoryService inventory, GuardedBillingService billing, GuardedUnitOfWork uow)
        NewService(AgenticServiceResult result, bool recordInScope = true, bool withAgent = true)
    {
        var repo = new FakePrescriptionRepository { RecordInScope = recordInScope };
        var agent = new FakeAgenticClient { Result = result };
        var inventory = new GuardedInventoryService();
        var billing = new GuardedBillingService();
        var uow = new GuardedUnitOfWork();
        var service = new MedicineRequestService(
            repo, inventory, billing, TestTenantContext.Unscoped, uow,
            withAgent ? agent : null);
        return (service, agent, inventory, billing, uow);
    }

    private static string AgentJson(
        string confidence = "High",
        string planningNotes = "Issue from the earliest valid batch") =>
        $@"{{
            ""requestId"":""whatever"",
            ""medicineRecommendation"":{{
                ""medicineId"":""med-1"",
                ""medicineName"":""Amoxicillin"",
                ""requiredQuantity"":14,
                ""availableQuantity"":40,
                ""sufficientStock"":true,
                ""reason"":""First requested item is in stock""
            }},
            ""recommendedBatch"":{{
                ""batchId"":""b1"",
                ""batchNumber"":""BN-100"",
                ""quantityAvailable"":40,
                ""expiryDate"":""2027-06-01"",
                ""expiryStatus"":""Valid""
            }},
            ""alternativeMedicines"":[{{
                ""medicineId"":""med-9"",
                ""medicineName"":""Doxycycline"",
                ""availableQuantity"":12,
                ""reason"":""In stock if Amoxicillin cannot be issued""
            }}],
            ""inventorySummary"":{{
                ""medicineFound"":true,
                ""stockAvailable"":true,
                ""sufficientQuantity"":true,
                ""batchAvailable"":true,
                ""notExpired"":true,
                ""lowStock"":false
            }},
            ""confidence"":""{confidence}"",
            ""planningNotes"":""{planningNotes}"",
            ""disclaimer"":""AI-generated medicine and inventory recommendation""
        }}";

    [Fact]
    public async Task CallsAgentWithTreatmentRecordId_AndForwardsBearerToken()
    {
        var (service, agent, _, _, _) = NewService(AgenticServiceResult.Ok(200, AgentJson()));

        var dto = await service.GetInventoryPlanAsync(RecordId, "Bearer tok123");

        Assert.Equal(1, agent.Calls);
        Assert.Equal(RecordId.ToString(), agent.LastRequestId);
        Assert.Equal("Bearer tok123", agent.LastBearerToken);
        Assert.Equal("agentic-ai", dto.Source);
        Assert.Equal(RecordId.ToString(), dto.RequestId);
        Assert.NotNull(dto.MedicineRecommendation);
        Assert.Equal("Amoxicillin", dto.MedicineRecommendation!.MedicineName);
        Assert.Equal(40, dto.MedicineRecommendation.AvailableQuantity);
        Assert.True(dto.MedicineRecommendation.SufficientStock);
        Assert.NotNull(dto.RecommendedBatch);
        Assert.Equal("BN-100", dto.RecommendedBatch!.BatchNumber);
        Assert.Equal("Valid", dto.RecommendedBatch.ExpiryStatus);
        Assert.Single(dto.AlternativeMedicines);
        Assert.Equal("Doxycycline", dto.AlternativeMedicines[0].MedicineName);
        Assert.Equal("High", dto.Confidence);
        Assert.True(dto.InventorySummary.NotExpired);
        Assert.Contains("inventory", dto.Disclaimer);
    }

    [Fact]
    public async Task RecordInOtherOrg_NotVisible_Unavailable_NoAgentCall()
    {
        var (service, agent, _, _, _) = NewService(
            AgenticServiceResult.Ok(200, AgentJson()), recordInScope: false);

        var dto = await service.GetInventoryPlanAsync(RecordId);

        Assert.Equal("unavailable", dto.Source);
        Assert.Equal(0, agent.Calls);
    }

    [Fact]
    public async Task MissingTreatmentRecord_NoAgentCall_Unavailable()
    {
        var (service, agent, _, _, _) = NewService(AgenticServiceResult.Ok(200, AgentJson()));

        var dto = await service.GetInventoryPlanAsync(Guid.NewGuid());

        Assert.Equal("unavailable", dto.Source);
        Assert.Equal(0, agent.Calls);
    }

    [Fact]
    public async Task AgentTimeout_ReturnsUnavailable()
    {
        var (service, _, _, _, _) = NewService(AgenticServiceResult.Failed("agentic_timeout"));

        var dto = await service.GetInventoryPlanAsync(RecordId);

        Assert.Equal("unavailable", dto.Source);
        Assert.Null(dto.MedicineRecommendation);
        Assert.Null(dto.RecommendedBatch);
        Assert.Empty(dto.AlternativeMedicines);
    }

    [Fact]
    public async Task AgentServerError_ReturnsUnavailable()
    {
        var (service, _, _, _, _) = NewService(AgenticServiceResult.Failed("agentic_error", 500));

        var dto = await service.GetInventoryPlanAsync(RecordId);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task MalformedAgentJson_ReturnsUnavailable()
    {
        var (service, _, _, _, _) = NewService(AgenticServiceResult.Ok(200, "not json {"));

        var dto = await service.GetInventoryPlanAsync(RecordId);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task MissingPlanningNotes_ReturnsUnavailable()
    {
        var (service, _, _, _, _) = NewService(AgenticServiceResult.Ok(200, AgentJson(planningNotes: " ")));

        var dto = await service.GetInventoryPlanAsync(RecordId);

        Assert.Equal("unavailable", dto.Source);
    }

    [Fact]
    public async Task InvalidConfidence_NormalizedToLow()
    {
        var (service, _, _, _, _) = NewService(AgenticServiceResult.Ok(200, AgentJson(confidence: "ABSOLUTE")));

        var dto = await service.GetInventoryPlanAsync(RecordId);

        Assert.Equal("Low", dto.Confidence);
    }

    [Fact]
    public async Task NoAgenticClient_ReturnsUnavailable()
    {
        var (service, agent, _, _, _) = NewService(
            AgenticServiceResult.Ok(200, AgentJson()), withAgent: false);

        var dto = await service.GetInventoryPlanAsync(RecordId);

        Assert.Equal("unavailable", dto.Source);
        Assert.Equal(0, agent.Calls);
    }

    [Fact]
    public async Task InventoryPlan_NeverMutatesInventoryBillingOrState()
    {
        var (service, _, inventory, billing, uow) = NewService(
            AgenticServiceResult.Ok(200, AgentJson()));

        await service.GetInventoryPlanAsync(RecordId, "Bearer tok");

        // Read-only: the analysis never reserves, dispenses, cancels,
        // refreshes a bill, or saves anything.
        Assert.Equal(0, inventory.Calls);
        Assert.Equal(0, billing.Calls);
        Assert.Equal(0, uow.Saves);
    }
}
