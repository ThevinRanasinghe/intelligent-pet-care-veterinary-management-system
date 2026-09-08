using FluentValidation;
using FluentValidation.Results;
using Moq;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// Unit tests for BillingService: quotation calculation (Subtotal/Total
/// derived from line items, never trusted from client input), budget
/// comparison, and quotation status transition rules. All dependencies
/// (repositories, unit of work, validators) are mocked so these tests
/// exercise only the Application layer, per
/// docs/database/scheduling-billing-approval-domain-model.md#billing.
/// </summary>
public class BillingServiceTests
{
    private readonly Mock<IQuotationRepository> _quotationRepository = new();
    private readonly Mock<IAppointmentRepository> _appointmentRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CreateQuotationRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateQuotationRequest>> _updateValidator = new();

    private static readonly Guid AppointmentId = Guid.NewGuid();
    private static readonly Guid QuotationId = Guid.NewGuid();

    public BillingServiceTests()
    {
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateQuotationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateQuotationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private BillingService CreateService() => new(
        _quotationRepository.Object,
        _appointmentRepository.Object,
        _unitOfWork.Object,
        _createValidator.Object,
        _updateValidator.Object);

    private static QuotationItemRequest Item(string category, string description, int quantity, decimal unitPrice) => new()
    {
        Category = category,
        Description = description,
        Quantity = quantity,
        UnitPrice = unitPrice
    };

    private static Quotation ExistingQuotation(
        decimal budget,
        QuotationStatus status,
        params QuotationItem[] items) => new()
    {
        Id = QuotationId,
        AppointmentId = AppointmentId,
        Budget = budget,
        Subtotal = items.Sum(i => i.TotalPrice),
        Total = items.Sum(i => i.TotalPrice),
        Status = status,
        Items = items.ToList()
    };

    private static QuotationItem ExistingItem(int quantity, decimal unitPrice) => new()
    {
        Id = Guid.NewGuid(),
        QuotationId = QuotationId,
        Category = "Consultation",
        Description = "Existing line",
        Quantity = quantity,
        UnitPrice = unitPrice,
        TotalPrice = quantity * unitPrice
    };

    // 1. Subtotal/Total are computed server-side as Quantity * UnitPrice, summed across items.
    [Fact]
    public async Task CreateQuotationAsync_ComputesSubtotalAndTotalFromItems()
    {
        var request = new CreateQuotationRequest
        {
            AppointmentId = AppointmentId,
            Budget = 200m,
            Items = new List<QuotationItemRequest>
            {
                Item("Consultation", "General consultation", 1, 20m),
                Item("Treatment", "Wound dressing", 2, 25m)
            }
        };

        var service = CreateService();

        var result = await service.CreateQuotationAsync(request);

        Assert.Equal(70m, result.Subtotal);
        Assert.Equal(70m, result.Total);
        Assert.Equal(QuotationStatus.Draft.ToString(), result.Status);
        _quotationRepository.Verify(r => r.AddAsync(It.IsAny<Quotation>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // 2. Total within budget -> IsWithinBudget is true.
    [Fact]
    public async Task CreateQuotationAsync_TotalWithinBudget_IsWithinBudgetTrue()
    {
        var request = new CreateQuotationRequest
        {
            AppointmentId = AppointmentId,
            Budget = 100m,
            Items = new List<QuotationItemRequest> { Item("Consultation", "Checkup", 1, 50m) }
        };

        var service = CreateService();

        var result = await service.CreateQuotationAsync(request);

        Assert.True(result.IsWithinBudget);
    }

    // 3. Total exceeding budget -> IsWithinBudget is false, but creation as Draft still succeeds
    // (submission, not creation, is where the budget rule blocks the workflow).
    [Fact]
    public async Task CreateQuotationAsync_TotalExceedsBudget_IsWithinBudgetFalse()
    {
        var request = new CreateQuotationRequest
        {
            AppointmentId = AppointmentId,
            Budget = 10m,
            Items = new List<QuotationItemRequest> { Item("Treatment", "Surgery", 1, 500m) }
        };

        var service = CreateService();

        var result = await service.CreateQuotationAsync(request);

        Assert.False(result.IsWithinBudget);
        Assert.Equal(QuotationStatus.Draft.ToString(), result.Status);
    }

    // 4. Updating a quotation replaces its items and recomputes Subtotal/Total.
    [Fact]
    public async Task UpdateQuotationAsync_ReplacesItemsAndRecomputesTotals()
    {
        var existing = ExistingQuotation(200m, QuotationStatus.Draft, ExistingItem(1, 20m));
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var request = new UpdateQuotationRequest
        {
            Budget = 200m,
            Items = new List<QuotationItemRequest> { Item("Medicine", "Antibiotics", 3, 10m) }
        };

        var service = CreateService();

        var result = await service.UpdateQuotationAsync(QuotationId, request);

        Assert.Equal(30m, result.Subtotal);
        Assert.Equal(30m, result.Total);
        Assert.Single(result.Items);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // 5. Approved quotations are read-only: update is rejected.
    [Fact]
    public async Task UpdateQuotationAsync_ApprovedQuotation_ThrowsConflict()
    {
        var existing = ExistingQuotation(200m, QuotationStatus.Approved, ExistingItem(1, 20m));
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var request = new UpdateQuotationRequest { Budget = 200m, Items = new List<QuotationItemRequest>() };
        var service = CreateService();

        await Assert.ThrowsAsync<BillingConflictException>(() => service.UpdateQuotationAsync(QuotationId, request));
    }

    // 6. Finalised quotations are read-only: update is rejected.
    [Fact]
    public async Task UpdateQuotationAsync_FinalisedQuotation_ThrowsConflict()
    {
        var existing = ExistingQuotation(200m, QuotationStatus.Finalised, ExistingItem(1, 20m));
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var request = new UpdateQuotationRequest { Budget = 200m, Items = new List<QuotationItemRequest>() };
        var service = CreateService();

        await Assert.ThrowsAsync<BillingConflictException>(() => service.UpdateQuotationAsync(QuotationId, request));
    }

    // 7. Calculate recomputes Subtotal/Total from the quotation's current items and persists.
    [Fact]
    public async Task CalculateQuotationAsync_RecomputesFromCurrentItems()
    {
        var item = ExistingItem(2, 15m);
        var existing = ExistingQuotation(100m, QuotationStatus.Draft, item);
        // Simulate a stale cached Total that no longer matches the items.
        existing.Total = 999m;
        existing.Subtotal = 999m;

        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = CreateService();

        var result = await service.CalculateQuotationAsync(QuotationId);

        Assert.Equal(30m, result.Subtotal);
        Assert.Equal(30m, result.Total);
        Assert.True(result.IsWithinBudget);
    }

    // 8. Submitting a Draft quotation within budget moves it to PendingApproval.
    [Fact]
    public async Task SubmitQuotationForApprovalAsync_WithinBudget_MovesToPendingApproval()
    {
        var existing = ExistingQuotation(100m, QuotationStatus.Draft, ExistingItem(1, 50m));
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = CreateService();

        var result = await service.SubmitQuotationForApprovalAsync(QuotationId);

        Assert.Equal(QuotationStatus.PendingApproval.ToString(), result.Status);
    }

    // 9. Submission is blocked when Total exceeds Budget.
    [Fact]
    public async Task SubmitQuotationForApprovalAsync_ExceedsBudget_ThrowsConflict()
    {
        var existing = ExistingQuotation(10m, QuotationStatus.Draft, ExistingItem(1, 50m));
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = CreateService();

        await Assert.ThrowsAsync<BillingConflictException>(() => service.SubmitQuotationForApprovalAsync(QuotationId));
        Assert.Equal(QuotationStatus.Draft, existing.Status);
    }

    // 10. Only Draft/RevisionRequested quotations can be submitted.
    [Fact]
    public async Task SubmitQuotationForApprovalAsync_AlreadyPendingApproval_ThrowsConflict()
    {
        var existing = ExistingQuotation(100m, QuotationStatus.PendingApproval, ExistingItem(1, 50m));
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = CreateService();

        await Assert.ThrowsAsync<BillingConflictException>(() => service.SubmitQuotationForApprovalAsync(QuotationId));
    }

    // 11. Finalising an Approved quotation succeeds.
    [Fact]
    public async Task FinalizeQuotationAsync_ApprovedQuotation_MovesToFinalised()
    {
        var existing = ExistingQuotation(100m, QuotationStatus.Approved, ExistingItem(1, 50m));
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = CreateService();

        var result = await service.FinalizeQuotationAsync(QuotationId);

        Assert.Equal(QuotationStatus.Finalised.ToString(), result.Status);
    }

    // 12. Finalising a non-Approved quotation is rejected.
    [Fact]
    public async Task FinalizeQuotationAsync_NotApproved_ThrowsConflict()
    {
        var existing = ExistingQuotation(100m, QuotationStatus.Draft, ExistingItem(1, 50m));
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = CreateService();

        await Assert.ThrowsAsync<BillingConflictException>(() => service.FinalizeQuotationAsync(QuotationId));
    }

    // 13. Fetching a non-existent quotation returns null (no exception).
    [Fact]
    public async Task GetQuotationByIdAsync_NotFound_ReturnsNull()
    {
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>())).ReturnsAsync((Quotation?)null);

        var service = CreateService();

        var result = await service.GetQuotationByIdAsync(QuotationId);

        Assert.Null(result);
    }

    // 14. Updating a non-existent quotation throws NotFoundException.
    [Fact]
    public async Task UpdateQuotationAsync_NotFound_ThrowsNotFound()
    {
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>())).ReturnsAsync((Quotation?)null);
        var request = new UpdateQuotationRequest { Budget = 100m, Items = new List<QuotationItemRequest>() };

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateQuotationAsync(QuotationId, request));
    }
}
