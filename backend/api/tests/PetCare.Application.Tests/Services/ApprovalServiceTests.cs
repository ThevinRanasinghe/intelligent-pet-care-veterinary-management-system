using FluentValidation;
using FluentValidation.Results;
using Moq;
using PetCare.Application.DTOs.Approval;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// Unit tests for ApprovalService: approve/reject/request-revision
/// transitions, reason requirements, invalid status transitions,
/// ApprovalHistory creation, and not-found handling. All dependencies
/// (repositories, unit of work, validators) are mocked so these tests
/// exercise only the Application layer, per
/// docs/database/scheduling-billing-approval-domain-model.md#approval.
/// </summary>
public class ApprovalServiceTests
{
    private readonly Mock<IApprovalRepository> _approvalRepository = new();
    private readonly Mock<IQuotationRepository> _quotationRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<ApproveRequest>> _approveValidator = new();
    private readonly Mock<IValidator<RejectRequest>> _rejectValidator = new();
    private readonly Mock<IValidator<RequestRevisionRequest>> _requestRevisionValidator = new();

    private static readonly Guid QuotationId = Guid.NewGuid();
    private static readonly Guid ApprovalId = Guid.NewGuid();
    private static readonly Guid ReviewerId = Guid.NewGuid();

    public ApprovalServiceTests()
    {
        _approveValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ApproveRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _rejectValidator
            .Setup(v => v.ValidateAsync(It.IsAny<RejectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _requestRevisionValidator
            .Setup(v => v.ValidateAsync(It.IsAny<RequestRevisionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private ApprovalService CreateService() => new(
        _approvalRepository.Object,
        _quotationRepository.Object,
        _unitOfWork.Object,
        _approveValidator.Object,
        _rejectValidator.Object,
        _requestRevisionValidator.Object);

    private static Quotation PendingApprovalQuotation(decimal budget = 100m, decimal total = 50m) => new()
    {
        Id = QuotationId,
        AppointmentId = Guid.NewGuid(),
        Budget = budget,
        Subtotal = total,
        Total = total,
        Status = QuotationStatus.PendingApproval
    };

    private static Approval PendingApproval(Quotation quotation) => new()
    {
        Id = ApprovalId,
        QuotationId = quotation.Id,
        Quotation = quotation,
        Status = ApprovalStatus.Pending
    };

    private void SetUpExistingApproval(Approval approval)
    {
        _approvalRepository
            .Setup(r => r.GetByIdAsync(ApprovalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);
    }

    // 1. Approve: a Pending approval on a PendingApproval quotation succeeds.
    [Fact]
    public async Task ApproveAsync_PendingApproval_TransitionsToApproved()
    {
        var quotation = PendingApprovalQuotation();
        var approval = PendingApproval(quotation);
        SetUpExistingApproval(approval);

        var result = await CreateService().ApproveAsync(ApprovalId, new ApproveRequest { ReviewedBy = ReviewerId });

        Assert.Equal("Approved", result.Status);
        Assert.Equal(ReviewerId, approval.ReviewedBy);
        Assert.NotNull(approval.ReviewedAt);
        Assert.Equal(QuotationStatus.Approved, quotation.Status);
    }

    // 2. Reject: a Pending approval with a reason succeeds.
    [Fact]
    public async Task RejectAsync_PendingApproval_TransitionsToRejected()
    {
        var quotation = PendingApprovalQuotation();
        var approval = PendingApproval(quotation);
        SetUpExistingApproval(approval);

        var result = await CreateService().RejectAsync(
            ApprovalId,
            new RejectRequest { ReviewedBy = ReviewerId, Reason = "Budget too low for requested treatment." });

        Assert.Equal("Rejected", result.Status);
        Assert.Equal(QuotationStatus.Rejected, quotation.Status);
        Assert.Equal("Budget too low for requested treatment.", approval.Comment);
    }

    // 3. Request revision: a Pending approval with a reason succeeds.
    [Fact]
    public async Task RequestRevisionAsync_PendingApproval_TransitionsToRevisionRequested()
    {
        var quotation = PendingApprovalQuotation();
        var approval = PendingApproval(quotation);
        SetUpExistingApproval(approval);

        var result = await CreateService().RequestRevisionAsync(
            ApprovalId,
            new RequestRevisionRequest { ReviewedBy = ReviewerId, Reason = "Please itemize medication costs." });

        Assert.Equal("RevisionRequested", result.Status);
        Assert.Equal(QuotationStatus.RevisionRequested, quotation.Status);
        Assert.Equal("Please itemize medication costs.", approval.Comment);
    }

    // 4. Reason validation: RejectRequestValidator rejects an empty reason.
    [Fact]
    public async Task RejectRequestValidator_EmptyReason_Fails()
    {
        var validator = new PetCare.Application.Validators.RejectRequestValidator();

        var result = await validator.ValidateAsync(new RejectRequest { ReviewedBy = ReviewerId, Reason = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RejectRequest.Reason));
    }

    // 5. Reason validation: RequestRevisionRequestValidator rejects an empty reason.
    [Fact]
    public async Task RequestRevisionRequestValidator_EmptyReason_Fails()
    {
        var validator = new PetCare.Application.Validators.RequestRevisionRequestValidator();

        var result = await validator.ValidateAsync(new RequestRevisionRequest { ReviewedBy = ReviewerId, Reason = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RequestRevisionRequest.Reason));
    }

    // 6. Reason validation: ApproveRequestValidator does not require a reason/comment.
    [Fact]
    public async Task ApproveRequestValidator_NoComment_Passes()
    {
        var validator = new PetCare.Application.Validators.ApproveRequestValidator();

        var result = await validator.ValidateAsync(new ApproveRequest { ReviewedBy = ReviewerId });

        Assert.True(result.IsValid);
    }

    // 7. Invalid status transition: approving an already-Approved approval is rejected.
    [Fact]
    public async Task ApproveAsync_AlreadyApproved_ThrowsApprovalConflictException()
    {
        var quotation = PendingApprovalQuotation();
        quotation.Status = QuotationStatus.Approved;
        var approval = PendingApproval(quotation);
        approval.Status = ApprovalStatus.Approved;
        SetUpExistingApproval(approval);

        await Assert.ThrowsAsync<ApprovalConflictException>(() =>
            CreateService().ApproveAsync(ApprovalId, new ApproveRequest { ReviewedBy = ReviewerId }));
    }

    // 8. Invalid status transition: rejecting an already-Rejected approval is rejected
    // ("approved/rejected items cannot be reviewed again").
    [Fact]
    public async Task RejectAsync_AlreadyRejected_ThrowsApprovalConflictException()
    {
        var quotation = PendingApprovalQuotation();
        quotation.Status = QuotationStatus.Rejected;
        var approval = PendingApproval(quotation);
        approval.Status = ApprovalStatus.Rejected;
        SetUpExistingApproval(approval);

        await Assert.ThrowsAsync<ApprovalConflictException>(() =>
            CreateService().RejectAsync(ApprovalId, new RejectRequest { ReviewedBy = ReviewerId, Reason = "Retry" }));
    }

    // 9. Invalid status transition: reviewing a quotation that is not
    // PendingApproval is rejected even if the Approval row itself is Pending
    // (defensive re-validation against Quotation.Status).
    [Fact]
    public async Task ApproveAsync_QuotationNotPendingApproval_ThrowsApprovalConflictException()
    {
        var quotation = PendingApprovalQuotation();
        quotation.Status = QuotationStatus.Draft;
        var approval = PendingApproval(quotation);
        SetUpExistingApproval(approval);

        await Assert.ThrowsAsync<ApprovalConflictException>(() =>
            CreateService().ApproveAsync(ApprovalId, new ApproveRequest { ReviewedBy = ReviewerId }));
    }

    // 10. Approval history creation: every decision appends exactly one
    // ApprovalHistory row with the correct previous/new status and reason.
    [Fact]
    public async Task ApproveAsync_Success_AppendsApprovalHistory()
    {
        var quotation = PendingApprovalQuotation();
        var approval = PendingApproval(quotation);
        SetUpExistingApproval(approval);

        await CreateService().ApproveAsync(ApprovalId, new ApproveRequest { ReviewedBy = ReviewerId, Comment = "Looks good" });

        _approvalRepository.Verify(r => r.AddHistoryAsync(
            It.Is<ApprovalHistory>(h =>
                h.ApprovalId == ApprovalId &&
                h.PreviousStatus == ApprovalStatus.Pending &&
                h.NewStatus == ApprovalStatus.Approved &&
                h.ChangedBy == ReviewerId &&
                h.Reason == "Looks good"),
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectAsync_Success_AppendsApprovalHistoryWithReason()
    {
        var quotation = PendingApprovalQuotation();
        var approval = PendingApproval(quotation);
        SetUpExistingApproval(approval);

        await CreateService().RejectAsync(ApprovalId, new RejectRequest { ReviewedBy = ReviewerId, Reason = "Over budget" });

        _approvalRepository.Verify(r => r.AddHistoryAsync(
            It.Is<ApprovalHistory>(h =>
                h.PreviousStatus == ApprovalStatus.Pending &&
                h.NewStatus == ApprovalStatus.Rejected &&
                h.Reason == "Over budget"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // 11. Not-found handling: approving a nonexistent approval throws NotFoundException.
    [Fact]
    public async Task ApproveAsync_ApprovalDoesNotExist_ThrowsNotFoundException()
    {
        _approvalRepository
            .Setup(r => r.GetByIdAsync(ApprovalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Approval?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService().ApproveAsync(ApprovalId, new ApproveRequest { ReviewedBy = ReviewerId }));
    }

    [Fact]
    public async Task RejectAsync_ApprovalDoesNotExist_ThrowsNotFoundException()
    {
        _approvalRepository
            .Setup(r => r.GetByIdAsync(ApprovalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Approval?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService().RejectAsync(ApprovalId, new RejectRequest { ReviewedBy = ReviewerId, Reason = "x" }));
    }

    [Fact]
    public async Task GetApprovalByIdAsync_DoesNotExist_ReturnsNull()
    {
        _approvalRepository
            .Setup(r => r.GetByIdAsync(ApprovalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Approval?)null);

        var result = await CreateService().GetApprovalByIdAsync(ApprovalId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetApprovalHistoryAsync_ApprovalDoesNotExist_ThrowsNotFoundException()
    {
        _approvalRepository
            .Setup(r => r.GetByIdAsync(ApprovalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Approval?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetApprovalHistoryAsync(ApprovalId));
    }

    // 12. Approval history retrieval returns the persisted rows mapped to DTOs.
    [Fact]
    public async Task GetApprovalHistoryAsync_ApprovalExists_ReturnsHistory()
    {
        var quotation = PendingApprovalQuotation();
        var approval = PendingApproval(quotation);
        SetUpExistingApproval(approval);

        var historyRow = new ApprovalHistory
        {
            Id = Guid.NewGuid(),
            ApprovalId = ApprovalId,
            PreviousStatus = ApprovalStatus.Pending,
            NewStatus = ApprovalStatus.Approved,
            ChangedBy = ReviewerId,
            Reason = null,
            ChangedAt = DateTimeOffset.UtcNow
        };

        _approvalRepository
            .Setup(r => r.GetHistoryAsync(ApprovalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApprovalHistory> { historyRow });

        var result = await CreateService().GetApprovalHistoryAsync(ApprovalId);

        Assert.Single(result);
        Assert.Equal("Pending", result[0].PreviousStatus);
        Assert.Equal("Approved", result[0].NewStatus);
    }

    // 13. Getting pending approvals lazily provisions an Approval row for a
    // PendingApproval quotation that doesn't have one yet.
    [Fact]
    public async Task GetPendingApprovalsAsync_QuotationWithoutApprovalRow_ProvisionsPendingApproval()
    {
        var quotation = PendingApprovalQuotation();

        _quotationRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Quotation> { quotation });

        _approvalRepository
            .Setup(r => r.GetByQuotationIdAsync(quotation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Approval?)null);

        _approvalRepository
            .Setup(r => r.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Approval> { PendingApproval(quotation) });

        var result = await CreateService().GetPendingApprovalsAsync();

        _approvalRepository.Verify(r => r.AddAsync(
            It.Is<Approval>(a => a.QuotationId == quotation.Id && a.Status == ApprovalStatus.Pending),
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.Single(result);
    }
}
