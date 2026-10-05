using PetCare.Application.DTOs.Approval;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Approval/review use cases. See
/// docs/database/scheduling-billing-approval-domain-model.md#approval for
/// the underlying business rules and
/// docs/api/scheduling-billing-approval-api-contract.md#approval for the
/// planned endpoints (controllers are added separately). No JWT/role
/// authorization is enforced here yet; the API security layer will add that
/// later, so ReviewedBy is currently accepted as caller-supplied input.
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// Approvals currently awaiting a Clinic Manager decision. Lazily
    /// provisions an Approval record (Status = Pending) for any Quotation
    /// that is PendingApproval but does not yet have one, since
    /// BillingService.SubmitQuotationForApprovalAsync only transitions
    /// Quotation.Status and does not create the Approval row itself.
    /// </summary>
    Task<IReadOnlyList<ApprovalResponse>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default);

    Task<ApprovalResponse?> GetApprovalByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a Pending review: Approval.Status -> Approved,
    /// Quotation.Status -> Approved, and appends an ApprovalHistory row.
    /// </summary>
    Task<ApprovalResponse> ApproveAsync(Guid id, ApproveRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a Pending review: Approval.Status -> Rejected,
    /// Quotation.Status -> Rejected, and appends an ApprovalHistory row.
    /// Requires a non-empty reason.
    /// </summary>
    Task<ApprovalResponse> RejectAsync(Guid id, RejectRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a revision on a Pending review: Approval.Status ->
    /// RevisionRequested, Quotation.Status -> RevisionRequested, and appends
    /// an ApprovalHistory row. Requires a non-empty reason.
    /// </summary>
    Task<ApprovalResponse> RequestRevisionAsync(Guid id, RequestRevisionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalHistoryResponse>> GetApprovalHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
