using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Agentic.Workflows;
using PetCare.Application.Interfaces;
using PetCare.Application.Validators;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

/// <summary>
/// AI-supervised consultation workflows: run/resume/advance/status/history.
/// The Python supervisor is advisory — approval decisions come only from an
/// authenticated ClinicManager here, and the actual appointment booking is
/// executed by the authoritative consultation workflow after approval.
/// </summary>
[ApiController]
[Route("api/agent-workflows")]
[Authorize]
public class AgentWorkflowsController : ControllerBase
{
    private const string ManageRoles =
        $"{Roles.ClinicManager},{Roles.SuperAdmin}";

    private const string ReadRoles =
        $"{Roles.ClinicManager},{Roles.SuperAdmin},{Roles.Veterinarian}";

    private const string OwnerReadRoles =
        $"{Roles.ClinicManager},{Roles.SuperAdmin},{Roles.PetOwner}";

    private readonly IAgentWorkflowService _workflows;
    private readonly IOwnerAccessService _ownerAccess;
    private readonly IValidator<WorkflowDecisionRequest> _decisionValidator;
    private readonly WorkflowDecisionWithCommentsValidator _commentsValidator;
    private readonly IValidator<WorkflowEventRequest> _eventValidator;

    public AgentWorkflowsController(
        IAgentWorkflowService workflows,
        IOwnerAccessService ownerAccess,
        IValidator<WorkflowDecisionRequest> decisionValidator,
        WorkflowDecisionWithCommentsValidator commentsValidator,
        IValidator<WorkflowEventRequest> eventValidator)
    {
        _workflows = workflows;
        _ownerAccess = ownerAccess;
        _decisionValidator = decisionValidator;
        _commentsValidator = commentsValidator;
        _eventValidator = eventValidator;
    }

    private string? BearerToken => Request.Headers.Authorization.ToString();

    /// <summary>Manually create (idempotent) a workflow for a consultation.</summary>
    [HttpPost("start")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<AgentWorkflowDto>> Start(
        StartAgentWorkflowRequest request, CancellationToken cancellationToken)
    {
        var dto = await _workflows.StartAsync(request.ConsultationRequestId, cancellationToken);
        return Ok(dto);
    }

    /// <summary>Run (or re-run) the supervisor graph.</summary>
    [HttpPost("{id}/run")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<AgentWorkflowDto>> Run(
        Guid id, CancellationToken cancellationToken)
    {
        var dto = await _workflows.RunAsync(id, BearerToken, cancellationToken);
        return Ok(dto);
    }

    /// <summary>Full workflow detail — clinic staff (org-scoped).</summary>
    [HttpGet("{id}")]
    [Authorize(Roles = ReadRoles)]
    public async Task<ActionResult<AgentWorkflowDto>> Get(
        Guid id, CancellationToken cancellationToken)
    {
        var dto = await _workflows.GetAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Workflow for a consultation. PetOwner callers receive only the
    /// reduced status view; staff get the full DTO.
    /// </summary>
    [HttpGet("by-consultation/{consultationId}")]
    [Authorize(Roles = OwnerReadRoles)]
    public async Task<ActionResult> GetByConsultation(
        string consultationId, CancellationToken cancellationToken)
    {
        if (_ownerAccess.IsPetOwner)
        {
            var status = await _workflows.GetStatusForOwnerAsync(consultationId, cancellationToken);
            return status is null ? NotFound() : Ok(status);
        }

        var dto = await _workflows.GetByConsultationAsync(consultationId, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Full audit history: steps + approvals + trajectory events.</summary>
    [HttpGet("{id}/history")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<AgentWorkflowHistoryDto>> GetHistory(
        Guid id, CancellationToken cancellationToken)
    {
        var dto = await _workflows.GetHistoryAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Approve the pending proposal — ClinicManager only.</summary>
    [HttpPost("{id}/approve")]
    [Authorize(Roles = Roles.ClinicManager)]
    public async Task<ActionResult<AgentWorkflowDto>> Approve(
        Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken)
    {
        await _decisionValidator.ValidateAndThrowAsync(request, cancellationToken);
        var dto = await _workflows.DecideAsync(
            id, AgentWorkflowDecision.Approved, request.Comments, BearerToken, cancellationToken);
        return Ok(dto);
    }

    /// <summary>Reject the pending proposal — ClinicManager only.</summary>
    [HttpPost("{id}/reject")]
    [Authorize(Roles = Roles.ClinicManager)]
    public async Task<ActionResult<AgentWorkflowDto>> Reject(
        Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await DecideWithComments(
            id, request, AgentWorkflowDecision.Rejected, cancellationToken));
    }

    /// <summary>Request a revision — ClinicManager only.</summary>
    [HttpPost("{id}/revision")]
    [Authorize(Roles = Roles.ClinicManager)]
    public async Task<ActionResult<AgentWorkflowDto>> RequestRevision(
        Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await DecideWithComments(
            id, request, AgentWorkflowDecision.RevisionRequested, cancellationToken));
    }

    /// <summary>Assert a business event (manual advance for demos).</summary>
    [HttpPost("{id}/events")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<AgentWorkflowDto>> RecordEvent(
        Guid id, WorkflowEventRequest request, CancellationToken cancellationToken)
    {
        await _eventValidator.ValidateAndThrowAsync(request, cancellationToken);
        var dto = await _workflows.AdvanceAsync(
            id, request.EventType, request.ReferenceId, BearerToken, cancellationToken);
        return Ok(dto);
    }

    private async Task<AgentWorkflowDto> DecideWithComments(
        Guid id, WorkflowDecisionRequest request, string decision,
        CancellationToken cancellationToken)
    {
        await _commentsValidator.ValidateAndThrowAsync(request, cancellationToken);
        return await _workflows.DecideAsync(
            id, decision, request.Comments, BearerToken, cancellationToken);
    }
}
