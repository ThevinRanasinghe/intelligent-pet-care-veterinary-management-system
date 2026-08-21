using FluentValidation;
using PetCare.Application.DTOs.Approval;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Application.Services;

/// <summary>
/// Approval/review business logic. This is the single source of truth for
/// approve/reject/request-revision rules and the ApprovalHistory audit
/// trail, shared by every client through ASP.NET Core. See
/// docs/database/scheduling-billing-approval-domain-model.md#approval for
/// the underlying business rules.
///
/// No JWT/role authorization is enforced here yet (added later by the API
/// security layer); ReviewedBy is accepted as caller-supplied input.
/// </summary>
public class ApprovalService : IApprovalService
{
    private readonly IApprovalRepository _approvalRepository;
    private readonly IQuotationRepository _quotationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<ApproveRequest> _approveValidator;
    private readonly IValidator<RejectRequest> _rejectValidator;
    private readonly IValidator<RequestRevisionRequest> _requestRevisionValidator;

    public ApprovalService(
        IApprovalRepository approvalRepository,
        IQuotationRepository quotationRepository,
        IUnitOfWork unitOfWork,
        IValidator<ApproveRequest> approveValidator,
        IValidator<RejectRequest> rejectValidator,
        IValidator<RequestRevisionRequest> requestRevisionValidator)
    {
        _approvalRepository = approvalRepository;
        _quotationRepository = quotationRepository;
        _unitOfWork = unitOfWork;
        _approveValidator = approveValidator;
        _rejectValidator = rejectValidator;
        _requestRevisionValidator = requestRevisionValidator;
    }

    public async Task<IReadOnlyList<ApprovalResponse>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureApprovalRecordsAreCurrentAsync(cancellationToken);

        var pending = await _approvalRepository.GetPendingAsync(cancellationToken);
        return pending.Select(ToResponse).ToList();
    }

    public async Task<ApprovalResponse?> GetApprovalByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var approval = await _approvalRepository.GetByIdAsync(id, cancellationToken);
        return approval is null ? null : ToResponse(approval);
    }

    public async Task<ApprovalResponse> ApproveAsync(Guid id, ApproveRequest request, CancellationToken cancellationToken = default)
    {
        await _approveValidator.ValidateAndThrowAsync(request, cancellationToken);

        var approval = await GetReviewableApprovalAsync(id, cancellationToken);

        var previousStatus = approval.Status;
        approval.Status = ApprovalStatus.Approved;
        approval.ReviewedBy = request.ReviewedBy;
        approval.ReviewedAt = DateTimeOffset.UtcNow;
        approval.Comment = request.Comment;
        approval.Quotation.Status = QuotationStatus.Approved;

        await AppendHistoryAsync(approval, previousStatus, ApprovalStatus.Approved, request.ReviewedBy, request.Comment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(approval);
    }

    public async Task<ApprovalResponse> RejectAsync(Guid id, RejectRequest request, CancellationToken cancellationToken = default)
    {
        await _rejectValidator.ValidateAndThrowAsync(request, cancellationToken);

        var approval = await GetReviewableApprovalAsync(id, cancellationToken);

        var previousStatus = approval.Status;
        approval.Status = ApprovalStatus.Rejected;
        approval.ReviewedBy = request.ReviewedBy;
        approval.ReviewedAt = DateTimeOffset.UtcNow;
        approval.Comment = request.Reason;
        approval.Quotation.Status = QuotationStatus.Rejected;

        await AppendHistoryAsync(approval, previousStatus, ApprovalStatus.Rejected, request.ReviewedBy, request.Reason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(approval);
    }

    public async Task<ApprovalResponse> RequestRevisionAsync(Guid id, RequestRevisionRequest request, CancellationToken cancellationToken = default)
    {
        await _requestRevisionValidator.ValidateAndThrowAsync(request, cancellationToken);

        var approval = await GetReviewableApprovalAsync(id, cancellationToken);

        var previousStatus = approval.Status;
        approval.Status = ApprovalStatus.RevisionRequested;
        approval.ReviewedBy = request.ReviewedBy;
        approval.ReviewedAt = DateTimeOffset.UtcNow;
        approval.Comment = request.Reason;
        approval.Quotation.Status = QuotationStatus.RevisionRequested;

        await AppendHistoryAsync(approval, previousStatus, ApprovalStatus.RevisionRequested, request.ReviewedBy, request.Reason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(approval);
    }

    public async Task<IReadOnlyList<ApprovalHistoryResponse>> GetApprovalHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = await _approvalRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Approval '{id}' does not exist.");

        var history = await _approvalRepository.GetHistoryAsync(id, cancellationToken);
        return history.Select(ToHistoryResponse).ToList();
    }

    /// <summary>
    /// Loads an approval and re-validates the two business rules that gate
    /// every decision: "only PendingApproval quotations can be reviewed" and
    /// "approved/rejected items cannot be reviewed again" (i.e. the approval
    /// itself must still be Pending). Both are checked defensively — the
    /// Approval.Status check catches direct re-review attempts, and the
    /// Quotation.Status check catches the case where the two have drifted
    /// out of sync.
    /// </summary>
    private async Task<Approval> GetReviewableApprovalAsync(Guid id, CancellationToken cancellationToken)
    {
        var approval = await _approvalRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Approval '{id}' does not exist.");

        if (approval.Status != ApprovalStatus.Pending)
        {
            throw new ApprovalConflictException(
                $"Only Pending approvals can be reviewed. Current status: '{approval.Status}'.");
        }

        if (approval.Quotation.Status != QuotationStatus.PendingApproval)
        {
            throw new ApprovalConflictException(
                $"Only quotations with status PendingApproval can be reviewed. Current quotation status: '{approval.Quotation.Status}'.");
        }

        return approval;
    }

    private async Task AppendHistoryAsync(
        Approval approval,
        ApprovalStatus previousStatus,
        ApprovalStatus newStatus,
        Guid changedBy,
        string? reason,
        CancellationToken cancellationToken)
    {
        var history = new ApprovalHistory
        {
            ApprovalId = approval.Id,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = DateTimeOffset.UtcNow
        };

        await _approvalRepository.AddHistoryAsync(history, cancellationToken);
    }

    /// <summary>
    /// Reconciles Approval rows against Quotation.Status = PendingApproval.
    /// Two cases are handled: (1) a Quotation just transitioned to
    /// PendingApproval for the first time and has no Approval row yet, so one
    /// is created as Pending; (2) a Quotation was RevisionRequested/Rejected,
    /// was edited and resubmitted by Billing (Quotation.Status back to
    /// PendingApproval), so the existing Approval row is reset to Pending for
    /// a new review cycle. Both cases only touch Approval, never
    /// Quotation.Status, since Billing already owns that transition.
    /// </summary>
    private async Task EnsureApprovalRecordsAreCurrentAsync(CancellationToken cancellationToken)
    {
        var quotations = await _quotationRepository.GetAllAsync(cancellationToken);
        var pendingApprovalQuotations = quotations.Where(q => q.Status == QuotationStatus.PendingApproval);

        foreach (var quotation in pendingApprovalQuotations)
        {
            var existing = await _approvalRepository.GetByQuotationIdAsync(quotation.Id, cancellationToken);

            if (existing is null)
            {
                await _approvalRepository.AddAsync(new Approval
                {
                    QuotationId = quotation.Id,
                    Status = ApprovalStatus.Pending
                }, cancellationToken);
            }
            else if (existing.Status != ApprovalStatus.Pending)
            {
                var previousStatus = existing.Status;
                existing.Status = ApprovalStatus.Pending;
                existing.ReviewedBy = null;
                existing.ReviewedAt = null;
                existing.Comment = null;

                await _approvalRepository.AddHistoryAsync(new ApprovalHistory
                {
                    ApprovalId = existing.Id,
                    PreviousStatus = previousStatus,
                    NewStatus = ApprovalStatus.Pending,
                    ChangedBy = Guid.Empty,
                    Reason = "Quotation resubmitted for approval.",
                    ChangedAt = DateTimeOffset.UtcNow
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static ApprovalResponse ToResponse(Approval approval) => new()
    {
        Id = approval.Id,
        QuotationId = approval.QuotationId,
        QuotationTotal = approval.Quotation.Total,
        QuotationBudget = approval.Quotation.Budget,
        Status = approval.Status.ToString(),
        ReviewedBy = approval.ReviewedBy,
        ReviewedAt = approval.ReviewedAt,
        Comment = approval.Comment
    };

    private static ApprovalHistoryResponse ToHistoryResponse(ApprovalHistory history) => new()
    {
        Id = history.Id,
        ApprovalId = history.ApprovalId,
        PreviousStatus = history.PreviousStatus.ToString(),
        NewStatus = history.NewStatus.ToString(),
        ChangedBy = history.ChangedBy,
        Reason = history.Reason,
        ChangedAt = history.ChangedAt
    };
}
