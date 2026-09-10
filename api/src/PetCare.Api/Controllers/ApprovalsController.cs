using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Api.DTOs;
using PetCare.Application.DTOs.Approvals;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

/// <summary>
/// AI Proposal and Consultation Approval management endpoints.
/// Executes atomic multi-entity approval transactions and maintains audit history.
/// </summary>
[ApiController]
[Route("api/approvals")]
[Produces("application/json")]
[Authorize(Roles = $"{Roles.ClinicManager},{Roles.SuperAdmin}")]
public sealed class ApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly ICurrentUserService _currentUser;

    public ApprovalsController(
        IApprovalService approvalService,
        ICurrentUserService currentUser)
    {
        _approvalService = approvalService;
        _currentUser = currentUser;
    }

    private Guid? GetOrganizationId() => _currentUser.OrganizationId;
    private string GetUserId() => _currentUser.UserId ?? string.Empty;
    private string GetUserEmail() => _currentUser.Email ?? string.Empty;

    /// <summary>
    /// READ ALL: Lists pending and historical AI proposals for the manager's clinic.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AIProposalSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var proposals = await _approvalService.GetProposalsAsync(orgId.Value, status, ct);
        return Ok(ApiResponse<IReadOnlyList<AIProposalSummaryDto>>.Ok(proposals));
    }

    /// <summary>
    /// READ ONE: Gets complete proposal details including appointment, quotation, pet, and consultation request.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AIProposalDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var proposal = await _approvalService.GetProposalByIdAsync(id, orgId.Value, ct);
        if (proposal is null)
            return NotFound(ApiResponse.Fail("Proposal not found."));

        return Ok(ApiResponse<AIProposalDetailsDto>.Ok(proposal));
    }

    /// <summary>
    /// APPROVE: Executes atomic transactional approval.
    /// Finalizes appointment, assigns veterinarian, reserves medicine, approves quotation,
    /// marks proposal Approved, logs approval history, and notifies relevant parties.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<AIProposalDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveProposalDto request, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var result = await _approvalService.ApproveProposalAsync(id, orgId.Value, GetUserId(), GetUserEmail(), request, ct);
            return Ok(ApiResponse<AIProposalDetailsDto>.Ok(result, "Proposal approved successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// REJECT: Rejects the AI proposal with a documented reason.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<AIProposalDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectProposalDto request, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var result = await _approvalService.RejectProposalAsync(id, orgId.Value, GetUserId(), GetUserEmail(), request, ct);
            return Ok(ApiResponse<AIProposalDetailsDto>.Ok(result, "Proposal rejected."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// REQUEST REVISION: Returns the proposal for revision with specific modification notes.
    /// </summary>
    [HttpPost("{id:guid}/request-revision")]
    [ProducesResponseType(typeof(ApiResponse<AIProposalDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestRevision(Guid id, [FromBody] RequestRevisionDto request, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var result = await _approvalService.RequestRevisionAsync(id, orgId.Value, GetUserId(), GetUserEmail(), request, ct);
            return Ok(ApiResponse<AIProposalDetailsDto>.Ok(result, "Revision requested successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// HISTORY: Retrieves the full audit history of decisions and revisions for a proposal.
    /// </summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ApprovalHistoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var history = await _approvalService.GetApprovalHistoryAsync(id, orgId.Value, ct);
            return Ok(ApiResponse<IReadOnlyList<ApprovalHistoryDto>>.Ok(history));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }
}
