using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using PetCare.Application.DTOs.Agentic.Workflows;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;

namespace PetCare.Application.Services;

/// <summary>
/// Persists the supervisor workflow, forwards calls to the Python service,
/// and executes approved actions through the authoritative
/// <see cref="IConsultationWorkflowService"/>. The AI never writes business
/// data itself — the backend is the system of record for every decision.
/// </summary>
public class AgentWorkflowService : IAgentWorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> AdvanceableEventTypes = new()
    {
        AgentWorkflowEventType.ExaminationRecorded,
        AgentWorkflowEventType.PrescriptionCreated
    };

    private static readonly HashSet<string> RunnableStatuses = new()
    {
        AgentWorkflowStatus.Created,
        AgentWorkflowStatus.Running,
        AgentWorkflowStatus.Failed
    };

    private readonly IAgentWorkflowRepository _workflows;
    private readonly IConsultationRequestRepository _consultations;
    private readonly IAgenticClient _agentic;
    private readonly IConsultationWorkflowService _consultationWorkflow;
    private readonly IOwnerAccessService _ownerAccess;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AgentWorkflowService>? _logger;

    public AgentWorkflowService(
        IAgentWorkflowRepository workflows,
        IConsultationRequestRepository consultations,
        IAgenticClient agentic,
        IConsultationWorkflowService consultationWorkflow,
        IOwnerAccessService ownerAccess,
        ITenantContext tenant,
        IUnitOfWork unitOfWork,
        ILogger<AgentWorkflowService>? logger = null)
    {
        _workflows = workflows;
        _consultations = consultations;
        _agentic = agentic;
        _consultationWorkflow = consultationWorkflow;
        _ownerAccess = ownerAccess;
        _tenant = tenant;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // --------------------------------------------------------------
    // Creation
    // --------------------------------------------------------------

    public async Task<AgentWorkflowDto?> EnsureCreatedForConsultationAsync(
        string consultationId, CancellationToken cancellationToken = default)
    {
        var consultation = await _consultations.GetByIdAsync(consultationId, cancellationToken);
        if (consultation is null)
            return null;

        var existing = await _workflows.GetByConsultationIdAsync(consultationId, cancellationToken);
        if (existing is not null)
            return MapToDto(existing);

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            ConsultationRequestId = consultation.Id,
            OrganizationId = consultation.OrganizationId,
            Objective =
                $"Plan and schedule consultation {consultation.Id}: triage, " +
                "propose an appointment, book it after manager approval, then " +
                "advance through examination and prescription.",
            Status = AgentWorkflowStatus.Created,
            InitiatedByUserId = _tenant.UserId
        };

        await _workflows.AddAsync(workflow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(workflow);
    }

    public async Task<AgentWorkflowDto> StartAsync(
        string consultationId, CancellationToken cancellationToken = default)
    {
        var dto = await EnsureCreatedForConsultationAsync(consultationId, cancellationToken);
        return dto ?? throw new NotFoundException(
            $"Consultation request '{consultationId}' was not found.");
    }

    // --------------------------------------------------------------
    // Run
    // --------------------------------------------------------------

    public async Task<AgentWorkflowDto> RunAsync(
        Guid workflowId, string? bearerToken, CancellationToken cancellationToken = default)
    {
        var workflow = await RequireWorkflowAsync(workflowId, cancellationToken);

        if (workflow.Status == AgentWorkflowStatus.PendingManagerApproval)
            throw new ApprovalConflictException(
                "This workflow is awaiting a manager decision — decide the pending approval first.");
        if (!RunnableStatuses.Contains(workflow.Status))
            throw new ApprovalConflictException(
                $"Workflow in status '{workflow.Status}' cannot be (re)run.");

        var (availableEvents, eventRefs) = RebuildEvents(workflow);
        var payload = new WorkflowRunPayload
        {
            ConsultationRequestId = workflow.ConsultationRequestId,
            Objective = workflow.Objective,
            AvailableEvents = availableEvents,
            EventRefs = eventRefs,
            Snapshot = BuildSnapshot(workflow)
        };

        var result = await _agentic.RunWorkflowAsync(
            workflow.Id.ToString(), payload, bearerToken, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            return await RecordAgenticFailureAsync(workflow, result.Error, cancellationToken);

        var run = ParseResult(result.Content);
        ApplyRunResult(workflow, run);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(workflow);
    }

    // --------------------------------------------------------------
    // Decide (approve / reject / revision) — ClinicManager only
    // --------------------------------------------------------------

    public async Task<AgentWorkflowDto> DecideAsync(
        Guid workflowId, string decision, string? comments,
        string? bearerToken, CancellationToken cancellationToken = default)
    {
        if (!_tenant.IsInRole(Roles.ClinicManager))
            throw new ForbiddenException(
                "Only a ClinicManager may approve, reject, or request revisions on a workflow.");

        var workflow = await RequireWorkflowAsync(workflowId, cancellationToken);
        var approval = workflow.Approvals
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault(a => a.Status == AgentWorkflowApprovalStatus.Pending)
            ?? throw new ApprovalConflictException(
                "This workflow has no pending approval to decide.");

        var payload = new WorkflowResumePayload
        {
            Decision = new WorkflowDecisionPayload
            {
                Decision = decision,
                Comments = comments,
                ApproverId = _tenant.UserId?.ToString(),
                DecidedAt = DateTime.UtcNow.ToString("O")
            },
            Snapshot = BuildSnapshot(workflow)
        };

        var result = await _agentic.ResumeWorkflowAsync(
            workflow.Id.ToString(), payload, bearerToken, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            return await RecordAgenticFailureAsync(workflow, result.Error, cancellationToken);

        var run = ParseResult(result.Content);

        // The decision exists only now — record it exactly once.
        approval.Status = decision;
        approval.DecidedByUserId = _tenant.UserId;
        approval.DecidedAt = DateTime.UtcNow;
        approval.Comments = comments;

        ApplyRunResult(workflow, run);

        if (decision == AgentWorkflowDecision.Approved && run.ApprovedAction is { } action)
        {
            await ExecuteBookingAsync(workflow, run, action, cancellationToken);
        }
        else if (decision == AgentWorkflowDecision.Rejected)
        {
            workflow.Status = AgentWorkflowStatus.Rejected;
            workflow.CompletedAt ??= DateTime.UtcNow;
        }
        // RevisionRequested: ApplyRunResult already opens a fresh Pending
        // approval when the supervisor returns a new proposal; otherwise the
        // returned status (Failed/Running) stands.

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(workflow);
    }

    // --------------------------------------------------------------
    // Advance (business event)
    // --------------------------------------------------------------

    public async Task<AgentWorkflowDto> AdvanceAsync(
        Guid workflowId, string eventType, string referenceId,
        string? bearerToken, CancellationToken cancellationToken = default)
    {
        if (!AdvanceableEventTypes.Contains(eventType))
            throw new ValidationException(
                $"Unsupported workflow event '{eventType}'.");

        var workflow = await RequireWorkflowAsync(workflowId, cancellationToken);

        var consultationId = await _workflows.ResolveConsultationIdAsync(
            eventType, referenceId, cancellationToken);
        if (!string.Equals(consultationId, workflow.ConsultationRequestId, StringComparison.Ordinal))
            throw new NotFoundException(
                $"The '{eventType}' reference does not belong to this workflow's consultation.");

        var (availableEvents, eventRefs) = RebuildEvents(workflow);
        var payload = new WorkflowAdvancePayload
        {
            Event = new WorkflowEventPayload { Type = eventType, ReferenceId = referenceId },
            AvailableEvents = availableEvents,
            Snapshot = BuildSnapshot(workflow)
        };

        var result = await _agentic.AdvanceWorkflowAsync(
            workflow.Id.ToString(), payload, bearerToken, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            return await RecordAgenticFailureAsync(workflow, result.Error, cancellationToken);

        var run = ParseResult(result.Content);
        ApplyRunResult(workflow, run);

        // Marker so the asserted event survives process restarts —
        // subsequent snapshots rebuild availableEvents from it.
        AppendEvent(workflow, new AgentWorkflowEvent
        {
            Node = "backend",
            EventType = eventType,
            DetailJson = JsonSerializer.Serialize(new { referenceId })
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(workflow);
    }

    // --------------------------------------------------------------
    // Reads
    // --------------------------------------------------------------

    public async Task<AgentWorkflowDto?> GetAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflows.GetWithDetailsAsync(workflowId, cancellationToken);
        if (workflow is null || !await IsInScopeAsync(workflow))
            return null;
        return MapToDto(workflow);
    }

    public async Task<AgentWorkflowDto?> GetByConsultationAsync(
        string consultationId, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflows.GetByConsultationIdAsync(consultationId, cancellationToken);
        if (workflow is null || !await IsInScopeAsync(workflow))
            return null;
        return MapToDto(workflow);
    }

    public async Task<AgentWorkflowStatusDto?> GetStatusForOwnerAsync(
        string consultationId, CancellationToken cancellationToken = default)
    {
        if (!_ownerAccess.IsPetOwner ||
            !await _ownerAccess.OwnsConsultationAsync(consultationId, cancellationToken))
            return null;

        var workflow = await _workflows.GetByConsultationIdAsync(consultationId, cancellationToken);
        return workflow is null
            ? null
            : new AgentWorkflowStatusDto
            {
                WorkflowId = workflow.Id,
                Status = workflow.Status,
                UpdatedAt = workflow.UpdatedAt
            };
    }

    public async Task<AgentWorkflowDto?> GetByClinicalEventReferenceAsync(
        string eventType, string referenceId, CancellationToken cancellationToken = default)
    {
        if (!AdvanceableEventTypes.Contains(eventType))
            return null;
        var consultationId = await _workflows.ResolveConsultationIdAsync(
            eventType, referenceId, cancellationToken);
        if (consultationId is null)
            return null;
        return await GetByConsultationAsync(consultationId, cancellationToken);
    }

    public async Task<AgentWorkflowHistoryDto?> GetHistoryAsync(
        Guid workflowId, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflows.GetDetailsNoTrackingAsync(workflowId, cancellationToken);
        if (workflow is null || !await IsInScopeAsync(workflow))
            return null;

        return new AgentWorkflowHistoryDto
        {
            Workflow = MapToDto(workflow),
            Steps = workflow.Steps.OrderBy(s => s.StepNumber).ThenBy(s => s.StartedAt)
                .Select(MapStep).ToList(),
            Approvals = workflow.Approvals.OrderBy(a => a.CreatedAt).Select(MapApproval).ToList(),
            Events = workflow.Events.OrderBy(e => e.Seq).Select(MapEvent).ToList()
        };
    }

    // --------------------------------------------------------------
    // Internals
    // --------------------------------------------------------------

    private async Task<AgentWorkflow> RequireWorkflowAsync(
        Guid workflowId, CancellationToken cancellationToken)
    {
        var workflow = await _workflows.GetWithDetailsAsync(workflowId, cancellationToken);
        if (workflow is null || !await IsInScopeAsync(workflow))
            throw new NotFoundException($"Workflow '{workflowId}' was not found.");
        return workflow;
    }

    /// <summary>
    /// Defense in depth: the repository already scopes reads, but the
    /// service re-checks so a mocked/misconfigured repository can never
    /// leak or mutate a foreign-organization workflow.
    /// </summary>
    private async Task<bool> IsInScopeAsync(AgentWorkflow workflow)
    {
        if (!_tenant.IsOrganizationScoped)
            return true;
        var orgId = await _tenant.GetOrganizationIdAsync();
        return workflow.OrganizationId == orgId;
    }

    private async Task ExecuteBookingAsync(
        AgentWorkflow workflow, AgenticWorkflowRunResult run,
        JsonElement action, CancellationToken cancellationToken)
    {
        try
        {
            var veterinarianId = action.GetProperty("veterinarianId").GetString();
            var date = DateOnly.Parse(action.GetProperty("date").GetString()!);
            var start = TimeOnly.Parse(action.GetProperty("startTime").GetString()!);
            var end = TimeOnly.Parse(action.GetProperty("endTime").GetString()!);

            // Multi-slot proposals carry the approved consecutive slot ids;
            // AssignConsultationAsync revalidates them (existence, org,
            // vet/date, still-Available, consecutive) and reserves all of
            // them transactionally — no partial bookings.
            var slotIds = action.TryGetProperty("slotIds", out var slotIdsElement)
                && slotIdsElement.ValueKind == JsonValueKind.Array
                    ? slotIdsElement.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => Guid.Parse(e.GetString()!))
                        .ToList()
                    : null;

            var appointment = await _consultationWorkflow.AssignConsultationAsync(
                workflow.ConsultationRequestId,
                new AssignVeterinarianRequest
                {
                    VeterinarianId = Guid.Parse(veterinarianId!),
                    Date = date,
                    StartTime = start,
                    EndTime = end,
                    SlotIds = slotIds is { Count: > 0 } ? slotIds : null,
                    Notes = $"Booked from approved AI workflow {workflow.Id}"
                },
                cancellationToken);

            workflow.ApprovedActionJson = action.GetRawText();
            workflow.Status = AgentWorkflowStatus.AwaitingExamination;
            var bookedStep = new AgentWorkflowStep
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                StepNumber = workflow.CurrentStep,
                Task = "backend:book_appointment",
                Status = "Completed",
                OutputJson = JsonSerializer.Serialize(new { appointmentId = appointment.Id }),
                CompletedAt = DateTime.UtcNow
            };
            _workflows.AddStep(bookedStep);
            if (!workflow.Steps.Contains(bookedStep)) workflow.Steps.Add(bookedStep);
        }
        catch (Exception ex)
        {
            // The backend booking is authoritative — on failure the workflow
            // fails safely with no partial state written elsewhere.
            _logger?.LogWarning(ex,
                "Workflow {WorkflowId} approved booking failed", workflow.Id);
            workflow.Status = AgentWorkflowStatus.Failed;
            workflow.FailureReason = $"booking_failed:{ex.GetType().Name}";
            workflow.CompletedAt = DateTime.UtcNow;
            var failedStep = new AgentWorkflowStep
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                StepNumber = workflow.CurrentStep,
                Task = "backend:book_appointment",
                Status = "Failed",
                Error = $"booking_failed:{ex.GetType().Name}",
                CompletedAt = DateTime.UtcNow
            };
            _workflows.AddStep(failedStep);
            if (!workflow.Steps.Contains(failedStep)) workflow.Steps.Add(failedStep);
        }
    }

    private async Task<AgentWorkflowDto> RecordAgenticFailureAsync(
        AgentWorkflow workflow, string? error, CancellationToken cancellationToken)
    {
        workflow.FailureReason = $"agentic_unavailable:{error ?? "unknown"}";
        AppendEvent(workflow, new AgentWorkflowEvent
        {
            Node = "backend",
            EventType = "agentic_unavailable",
            DetailJson = JsonSerializer.Serialize(new { error })
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(workflow);
    }

    /// <summary>
    /// Merges the supervisor's response into the persisted aggregate: latest
    /// status/counters/plan/proposal, upserted step rows by list position,
    /// and appended trajectory events (python returns only the new ones).
    /// </summary>
    private void ApplyRunResult(AgentWorkflow workflow, AgenticWorkflowRunResult run)
    {
        if (!string.IsNullOrWhiteSpace(run.Status))
            workflow.Status = run.Status;
        if (run.Plan is { } plan)
            workflow.PlanJson = plan.GetRawText();
        if (run.Proposal is { } proposal)
            workflow.ProposalJson = proposal.GetRawText();
        workflow.DelegationCount = run.DelegationCount;
        workflow.RevisionCount = run.RevisionCount;
        workflow.FailureReason = run.FailureReason;
        workflow.CurrentStep = run.Steps.Count;
        if (run.ApprovedAction is { } approved)
            workflow.ApprovedActionJson = approved.GetRawText();

        // Match incoming steps to persisted rows by (StepNumber, Task), not raw
        // list position: the backend inserts its own rows (Task = "backend:*",
        // e.g. the executed booking) which shift positional pairing and would
        // otherwise let specialist metadata overwrite them. Backend-owned rows
        // are never merge targets — the graph cannot emit a "backend:" task.
        var persisted = workflow.Steps
            .OrderBy(s => s.StepNumber).ThenBy(s => s.StartedAt).ToList();
        var matched = new HashSet<AgentWorkflowStep>();
        foreach (var incoming in run.Steps)
        {
            var candidates = persisted
                .Where(s => s.StepNumber == incoming.StepNumber && !matched.Contains(s))
                .ToList();
            var row = candidates.FirstOrDefault(s => s.Task == incoming.Task)
                ?? candidates.FirstOrDefault(s =>
                    !s.Task.StartsWith("backend:", StringComparison.Ordinal));
            if (row is null)
            {
                row = new AgentWorkflowStep
                {
                    Id = Guid.NewGuid(),
                    WorkflowId = workflow.Id,
                    StepNumber = incoming.StepNumber,
                    Task = incoming.Task ?? string.Empty
                };
                _workflows.AddStep(row);
                if (!workflow.Steps.Contains(row)) workflow.Steps.Add(row);
                persisted.Add(row);
            }
            matched.Add(row);
            row.AgentName = incoming.AgentName;
            row.InputSummaryJson = incoming.InputSummary?.GetRawText();
            row.OutputJson = incoming.Output?.GetRawText();
            row.ToolCallsJson = incoming.ToolCalls?.GetRawText();
            row.ValidationJson = incoming.Validation?.GetRawText();
            row.Status = incoming.Status ?? row.Status;
            row.Error = incoming.Error;
            row.RetryCount = incoming.RetryCount;
            row.StartedAt = incoming.StartedAt is { } s ? ToUtc(s) : row.StartedAt;
            row.CompletedAt = incoming.CompletedAt is { } c ? ToUtc(c) : null;
        }

        var seq = workflow.Events.Select(e => e.Seq).DefaultIfEmpty(0).Max();
        foreach (var e in run.Trajectory)
        {
            var newEvent = new AgentWorkflowEvent
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                // Backend assigns seq — python's per-invocation numbering can
                // repeat (e.g. approval_gate emits both awaiting_decision and
                // decision_approved at the same index across run/resume).
                Seq = ++seq,
                Timestamp = e.Timestamp is { } t ? ToUtc(t) : DateTime.UtcNow,
                Node = e.Node,
                AgentName = e.Agent,
                EventType = e.EventType,
                DetailJson = e.Detail?.GetRawText()
            };
            _workflows.AddEvent(newEvent);
            if (!workflow.Events.Contains(newEvent)) workflow.Events.Add(newEvent);
            seq = workflow.Events.Max(x => x.Seq);
        }

        if (workflow.Status is AgentWorkflowStatus.PendingManagerApproval
            && run.Proposal is { } pendingProposal
            && !workflow.Approvals.Any(
                a => a.Status == AgentWorkflowApprovalStatus.Pending))
        {
            var pendingApproval = new AgentWorkflowApproval
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                StepNumber = workflow.CurrentStep,
                Status = AgentWorkflowApprovalStatus.Pending,
                ProposalJson = pendingProposal.GetRawText()
            };
            _workflows.AddApproval(pendingApproval);
            if (!workflow.Approvals.Contains(pendingApproval)) workflow.Approvals.Add(pendingApproval);
        }

        if (workflow.Status is AgentWorkflowStatus.Completed
            or AgentWorkflowStatus.Rejected or AgentWorkflowStatus.Failed)
        {
            workflow.CompletedAt ??= DateTime.UtcNow;
        }
    }

    /// <summary>
    /// The Python service emits ISO-8601 offsets (e.g. "+00:00"), which
    /// System.Text.Json materialises as Kind=Local — Npgsql only accepts UTC
    /// for timestamptz. Treat naive values as UTC and convert local ones.
    /// </summary>
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private void AppendEvent(AgentWorkflow workflow, AgentWorkflowEvent e)
    {
        e.Id = Guid.NewGuid();
        e.WorkflowId = workflow.Id;
        e.Seq = workflow.Events.Select(x => x.Seq).DefaultIfEmpty(0).Max() + 1;
        e.Timestamp = DateTime.UtcNow;
        _workflows.AddEvent(e);
        if (!workflow.Events.Contains(e)) workflow.Events.Add(e);
    }

    /// <summary>
    /// Snapshot the backend persisted — exactly the shape the Python
    /// supervisor accepts for rehydration.
    /// </summary>
    private static JsonElement? BuildSnapshot(AgentWorkflow workflow)
    {
        JsonElement? plan = null;
        if (!string.IsNullOrWhiteSpace(workflow.PlanJson))
            plan = JsonDocument.Parse(workflow.PlanJson).RootElement.Clone();

        var latestDecision = workflow.Approvals
            .Where(a => a.Status != AgentWorkflowApprovalStatus.Pending)
            .OrderByDescending(a => a.DecidedAt)
            .Select(a => (object?)new
            {
                decision = a.Status,
                comments = a.Comments,
                approverId = a.DecidedByUserId?.ToString(),
                decidedAt = a.DecidedAt?.ToString("O")
            })
            .FirstOrDefault();

        var snapshot = new
        {
            plan,
            steps = workflow.Steps
                .OrderBy(s => s.StepNumber).ThenBy(s => s.StartedAt)
                .Select(s => new
                {
                    stepNumber = s.StepNumber,
                    agentName = s.AgentName,
                    task = s.Task,
                    status = s.Status,
                    error = s.Error,
                    retryCount = s.RetryCount
                })
                .ToList(),
            delegationCount = workflow.DelegationCount,
            revisionCount = workflow.RevisionCount,
            approval = latestDecision
        };

        return JsonSerializer.SerializeToElement(snapshot, JsonOptions);
    }

    /// <summary>
    /// Rebuilds asserted business events from persisted marker rows so a
    /// rehydrated python run sees the same gating facts.
    /// </summary>
    private static (Dictionary<string, object?>, Dictionary<string, string>) RebuildEvents(
        AgentWorkflow workflow)
    {
        var available = new Dictionary<string, object?>
        {
            [AgentWorkflowEventType.ConsultationSubmitted] = new
            {
                id = workflow.ConsultationRequestId
            }
        };
        var refs = new Dictionary<string, string>();

        foreach (var e in workflow.Events)
        {
            if (e.Node != "backend" || string.IsNullOrWhiteSpace(e.DetailJson))
                continue;
            try
            {
                var detail = JsonDocument.Parse(e.DetailJson).RootElement;
                if (detail.TryGetProperty("referenceId", out var refProp))
                {
                    var refId = refProp.GetString();
                    available[e.EventType] = new { id = refId };
                    if (refId is not null)
                        refs[e.EventType] = refId;
                }
            }
            catch (JsonException) { /* malformed detail — skip */ }
        }

        return (available, refs);
    }

    private static AgenticWorkflowRunResult ParseResult(string content)
    {
        var run = JsonSerializer.Deserialize<AgenticWorkflowRunResult>(content, JsonOptions);
        return run ?? new AgenticWorkflowRunResult { Status = "Unknown" };
    }

    // --------------------------------------------------------------
    // Mapping
    // --------------------------------------------------------------

    private static JsonElement? Parse(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonDocument.Parse(json).RootElement.Clone();

    private static AgentWorkflowDto MapToDto(AgentWorkflow workflow) => new()
    {
        Id = workflow.Id,
        ConsultationRequestId = workflow.ConsultationRequestId,
        OrganizationId = workflow.OrganizationId,
        Objective = workflow.Objective,
        Status = workflow.Status,
        CurrentStep = workflow.CurrentStep,
        Plan = Parse(workflow.PlanJson),
        Proposal = Parse(workflow.ProposalJson),
        ApprovedAction = Parse(workflow.ApprovedActionJson),
        DelegationCount = workflow.DelegationCount,
        RevisionCount = workflow.RevisionCount,
        FailureReason = workflow.FailureReason,
        CreatedAt = workflow.CreatedAt,
        UpdatedAt = workflow.UpdatedAt,
        CompletedAt = workflow.CompletedAt,
        Steps = workflow.Steps
            .OrderBy(s => s.StepNumber).ThenBy(s => s.StartedAt)
            .Select(MapStep).ToList(),
        Approvals = workflow.Approvals.OrderBy(a => a.CreatedAt)
            .Select(MapApproval).ToList()
    };

    private static AgentWorkflowStepDto MapStep(AgentWorkflowStep s) => new()
    {
        Id = s.Id,
        StepNumber = s.StepNumber,
        AgentName = s.AgentName,
        Task = s.Task,
        InputSummary = Parse(s.InputSummaryJson),
        Output = Parse(s.OutputJson),
        ToolCalls = Parse(s.ToolCallsJson),
        Status = s.Status,
        Validation = Parse(s.ValidationJson),
        Error = s.Error,
        RetryCount = s.RetryCount,
        StartedAt = s.StartedAt,
        CompletedAt = s.CompletedAt
    };

    private static AgentWorkflowApprovalDto MapApproval(AgentWorkflowApproval a) => new()
    {
        Id = a.Id,
        StepNumber = a.StepNumber,
        Status = a.Status,
        Proposal = Parse(a.ProposalJson),
        DecidedByUserId = a.DecidedByUserId,
        DecidedAt = a.DecidedAt,
        Comments = a.Comments,
        CreatedAt = a.CreatedAt
    };

    private static AgentWorkflowEventDto MapEvent(AgentWorkflowEvent e) => new()
    {
        Seq = e.Seq,
        Timestamp = e.Timestamp,
        Node = e.Node,
        AgentName = e.AgentName,
        EventType = e.EventType,
        Detail = Parse(e.DetailJson)
    };
}
