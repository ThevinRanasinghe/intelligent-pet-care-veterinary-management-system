using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Approval;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

/// <summary>
/// Approval/review endpoints. All business rules (only PendingApproval
/// quotations can be reviewed, reason requirements for reject/revision,
/// status transition rules, ApprovalHistory persistence) are enforced by
/// <see cref="IApprovalService"/> in PetCare.Application; this controller
/// only handles HTTP concerns (routing, status codes, model binding). See
/// docs/api/scheduling-billing-approval-api-contract.md#approval.
/// Approve/Reject/RequestRevision require the Clinic Manager role (see
/// <see cref="Roles.ClinicManager"/>); ReviewedBy is still accepted as
/// part of the request body (unchanged business logic) rather than derived
/// from the token, to keep this change scoped to authorization only.
/// </summary>
[ApiController]
[Route("api/approvals")]
[Produces("application/json")]
public class ApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvalService;

    public ApprovalsController(IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    /// <summary>
    /// Gets all approvals currently awaiting a Clinic Manager decision.
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<ApprovalResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApprovalResponse>>> GetPendingApprovals(CancellationToken cancellationToken)
    {
        var approvals = await _approvalService.GetPendingApprovalsAsync(cancellationToken);
        return Ok(approvals);
    }

    /// <summary>Gets a single approval by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApprovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApprovalResponse>> GetApprovalById(Guid id, CancellationToken cancellationToken)
    {
        var approval = await _approvalService.GetApprovalByIdAsync(id, cancellationToken);
        return approval is null ? NotFound() : Ok(approval);
    }

    /// <summary>
    /// Approves a Pending review. Moves the linked Quotation to Approved and
    /// appends an ApprovalHistory row. Rejected with 409 if the approval or
    /// its quotation is not currently reviewable.
    /// </summary>
    [Authorize(Roles = Roles.ClinicManager)]
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApprovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApprovalResponse>> Approve(
        Guid id,
        [FromBody] ApproveRequest request,
        CancellationToken cancellationToken)
    {
        var approval = await _approvalService.ApproveAsync(id, request, cancellationToken);
        return Ok(approval);
    }

    /// <summary>
    /// Rejects a Pending review. Requires a non-empty Reason. Moves the
    /// linked Quotation to Rejected and appends an ApprovalHistory row.
    /// Rejected with 409 if the approval or its quotation is not currently
    /// reviewable.
    /// </summary>
    [Authorize(Roles = Roles.ClinicManager)]
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ApprovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApprovalResponse>> Reject(
        Guid id,
        [FromBody] RejectRequest request,
        CancellationToken cancellationToken)
    {
        var approval = await _approvalService.RejectAsync(id, request, cancellationToken);
        return Ok(approval);
    }

    /// <summary>
    /// Requests a revision on a Pending review. Requires a non-empty Reason.
    /// Moves the linked Quotation to RevisionRequested and appends an
    /// ApprovalHistory row. Rejected with 409 if the approval or its
    /// quotation is not currently reviewable.
    /// </summary>
    [Authorize(Roles = Roles.ClinicManager)]
    [HttpPost("{id:guid}/revision")]
    [ProducesResponseType(typeof(ApprovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApprovalResponse>> RequestRevision(
        Guid id,
        [FromBody] RequestRevisionRequest request,
        CancellationToken cancellationToken)
    {
        var approval = await _approvalService.RequestRevisionAsync(id, request, cancellationToken);
        return Ok(approval);
    }

    /// <summary>
    /// Gets the full audit trail of status changes for an approval, in
    /// chronological order.
    /// </summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<ApprovalHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ApprovalHistoryResponse>>> GetApprovalHistory(Guid id, CancellationToken cancellationToken)
    {
        var history = await _approvalService.GetApprovalHistoryAsync(id, cancellationToken);
        return Ok(history);
    }
}
