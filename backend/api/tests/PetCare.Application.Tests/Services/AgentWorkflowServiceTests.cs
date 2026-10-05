using System.Text.Json;
using Moq;
using PetCare.Application.DTOs.Agentic;
using PetCare.Application.DTOs.Agentic.Workflows;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// Unit tests for AgentWorkflowService: run/decide/advance behaviour with a
/// mocked Python supervisor and mocked repositories. Key guarantees: the
/// authoritative assignment runs exactly once and only after an approval;
/// rejected or unavailable runs never book anything; tenant isolation holds.
/// </summary>
public class AgentWorkflowServiceTests
{
    private readonly Mock<IAgentWorkflowRepository> _workflows = new();
    private readonly Mock<IConsultationRequestRepository> _consultations = new();
    private readonly Mock<IAgenticClient> _agentic = new();
    private readonly Mock<IConsultationWorkflowService> _consultationWorkflow = new();
    private readonly Mock<IOwnerAccessService> _ownerAccess = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static readonly Guid ManagerUserId = Guid.NewGuid();

    public AgentWorkflowServiceTests()
    {
        _tenant.Setup(t => t.UserId).Returns(ManagerUserId);
        _tenant.Setup(t => t.IsInRole(Roles.ClinicManager)).Returns(true);
        _tenant.Setup(t => t.IsOrganizationScoped).Returns(false);
    }

    private AgentWorkflowService CreateService() => new(
        _workflows.Object,
        _consultations.Object,
        _agentic.Object,
        _consultationWorkflow.Object,
        _ownerAccess.Object,
        _tenant.Object,
        _unitOfWork.Object);

    private AgentWorkflow Workflow(
        string status = AgentWorkflowStatus.Created,
        bool withPendingApproval = false,
        Guid? organizationId = null)
    {
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            ConsultationRequestId = "CON-1",
            OrganizationId = organizationId,
            Objective = "Plan and schedule consultation CON-1",
            Status = status
        };
        if (withPendingApproval)
        {
            workflow.Approvals.Add(new AgentWorkflowApproval
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                StepNumber = 2,
                Status = AgentWorkflowApprovalStatus.Pending,
                ProposalJson = "{}",
                CreatedAt = DateTime.UtcNow
            });
        }
        return workflow;
    }

    private void Track(AgentWorkflow workflow)
        => _workflows.Setup(r => r.GetWithDetailsAsync(workflow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

    private static string ResultJson(object payload)
        => JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static AgenticServiceResult OkResult(object payload)
        => AgenticServiceResult.Ok(200, ResultJson(payload));

    // --------------------------------------------------------------

    [Fact]
    public async Task Run_creates_pending_approval_when_python_asks_for_decision()
    {
        var workflow = Workflow();
        Track(workflow);
        _agentic
            .Setup(c => c.RunWorkflowAsync(
                workflow.Id.ToString(), It.IsAny<WorkflowRunPayload>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(new
            {
                status = AgentWorkflowStatus.PendingManagerApproval,
                proposal = new { veterinarianId = Guid.NewGuid(), date = "2025-06-05" },
                steps = Array.Empty<object>(),
                trajectory = Array.Empty<object>(),
                trajectoryTotal = 0
            }));

        var dto = await CreateService().RunAsync(workflow.Id, "bearer");

        Assert.Equal(AgentWorkflowStatus.PendingManagerApproval, dto.Status);
        var approval = Assert.Single(dto.Approvals);
        Assert.Equal(AgentWorkflowApprovalStatus.Pending, approval.Status);
        _consultationWorkflow.Verify(c => c.AssignConsultationAsync(
            It.IsAny<string>(), It.IsAny<AssignVeterinarianRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_on_pending_workflow_throws_conflict_and_never_calls_python()
    {
        var workflow = Workflow(AgentWorkflowStatus.PendingManagerApproval, withPendingApproval: true);
        Track(workflow);

        var ex = await Assert.ThrowsAsync<ApprovalConflictException>(
            () => CreateService().RunAsync(workflow.Id, "bearer"));

        Assert.Contains("awaiting", ex.Message, StringComparison.OrdinalIgnoreCase);
        _agentic.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Approve_executes_assignment_exactly_once()
    {
        var vetId = Guid.NewGuid();
        var workflow = Workflow(AgentWorkflowStatus.PendingManagerApproval, withPendingApproval: true);
        Track(workflow);
        _agentic
            .Setup(c => c.ResumeWorkflowAsync(
                workflow.Id.ToString(), It.IsAny<WorkflowResumePayload>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(new
            {
                status = AgentWorkflowStatus.Approved,
                approvedAction = new
                {
                    action = "book_appointment",
                    veterinarianId = vetId.ToString(),
                    date = "2025-06-05",
                    startTime = "10:00",
                    endTime = "11:00"
                },
                steps = Array.Empty<object>(),
                trajectory = Array.Empty<object>(),
                trajectoryTotal = 0
            }));
        _consultationWorkflow
            .Setup(c => c.AssignConsultationAsync(
                "CON-1", It.IsAny<AssignVeterinarianRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppointmentResponse { Id = Guid.NewGuid(), Status = "Confirmed" });

        var dto = await CreateService().DecideAsync(
            workflow.Id, AgentWorkflowDecision.Approved, "Looks good", "bearer");

        _consultationWorkflow.Verify(c => c.AssignConsultationAsync(
            "CON-1",
            It.Is<AssignVeterinarianRequest>(r =>
                r.VeterinarianId == vetId &&
                r.Date == new DateOnly(2025, 6, 5) &&
                r.StartTime == new TimeOnly(10, 0) &&
                r.EndTime == new TimeOnly(11, 0)),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(AgentWorkflowStatus.AwaitingExamination, dto.Status);
        Assert.Contains(dto.Steps, s =>
            s.Task == "backend:book_appointment" && s.Status == "Completed");
        Assert.Equal(AgentWorkflowDecision.Approved,
            workflow.Approvals.Single().Status);
    }

    [Fact]
    public async Task Reject_never_invokes_assignment()
    {
        var workflow = Workflow(AgentWorkflowStatus.PendingManagerApproval, withPendingApproval: true);
        Track(workflow);
        _agentic
            .Setup(c => c.ResumeWorkflowAsync(
                workflow.Id.ToString(), It.IsAny<WorkflowResumePayload>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(new
            {
                status = AgentWorkflowStatus.Rejected,
                steps = Array.Empty<object>(),
                trajectory = Array.Empty<object>(),
                trajectoryTotal = 0
            }));

        var dto = await CreateService().DecideAsync(
            workflow.Id, AgentWorkflowDecision.Rejected, "Not suitable", "bearer");

        Assert.Equal(AgentWorkflowStatus.Rejected, dto.Status);
        _consultationWorkflow.Verify(c => c.AssignConsultationAsync(
            It.IsAny<string>(), It.IsAny<AssignVeterinarianRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Revision_creates_new_pending_approval_when_python_reproposes()
    {
        var workflow = Workflow(AgentWorkflowStatus.PendingManagerApproval, withPendingApproval: true);
        Track(workflow);
        _agentic
            .Setup(c => c.ResumeWorkflowAsync(
                workflow.Id.ToString(), It.IsAny<WorkflowResumePayload>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(new
            {
                status = AgentWorkflowStatus.PendingManagerApproval,
                proposal = new { veterinarianId = Guid.NewGuid(), date = "2025-06-06" },
                steps = Array.Empty<object>(),
                trajectory = Array.Empty<object>(),
                trajectoryTotal = 0
            }));

        var dto = await CreateService().DecideAsync(
            workflow.Id, AgentWorkflowDecision.RevisionRequested, "Pick a later slot", "bearer");

        Assert.Equal(2, dto.Approvals.Count);
        Assert.Equal(1, dto.Approvals.Count(a => a.Status == AgentWorkflowApprovalStatus.Pending));
        Assert.Equal(AgentWorkflowDecision.RevisionRequested, dto.Approvals.First().Status);
        _consultationWorkflow.Verify(c => c.AssignConsultationAsync(
            It.IsAny<string>(), It.IsAny<AssignVeterinarianRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Duplicate_decision_throws_conflict()
    {
        // Approval already decided — a second call must not re-decide or re-book.
        var workflow = Workflow(AgentWorkflowStatus.AwaitingExamination);
        workflow.Approvals.Add(new AgentWorkflowApproval
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            StepNumber = 2,
            Status = AgentWorkflowDecision.Approved,
            ProposalJson = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            DecidedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        Track(workflow);

        await Assert.ThrowsAsync<ApprovalConflictException>(() =>
            CreateService().DecideAsync(
                workflow.Id, AgentWorkflowDecision.Approved, null, "bearer"));
        _agentic.VerifyNoOtherCalls();
        _consultationWorkflow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Non_manager_decision_is_forbidden()
    {
        _tenant.Setup(t => t.IsInRole(It.IsAny<string>())).Returns(false);
        var workflow = Workflow(AgentWorkflowStatus.PendingManagerApproval, withPendingApproval: true);
        Track(workflow);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateService().DecideAsync(
                workflow.Id, AgentWorkflowDecision.Approved, null, "bearer"));
        _agentic.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Foreign_organization_workflow_is_not_found()
    {
        _tenant.Setup(t => t.IsOrganizationScoped).Returns(true);
        _tenant.Setup(t => t.GetOrganizationIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var workflow = Workflow(organizationId: Guid.NewGuid());
        Track(workflow);
        _workflows
            .Setup(r => r.GetByConsultationIdAsync("CON-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService().RunAsync(workflow.Id, "bearer"));
        Assert.Null(await CreateService().GetByConsultationAsync("CON-1"));
    }

    [Fact]
    public async Task Agentic_unavailable_keeps_status_and_records_event()
    {
        var workflow = Workflow();
        Track(workflow);
        _agentic
            .Setup(c => c.RunWorkflowAsync(
                workflow.Id.ToString(), It.IsAny<WorkflowRunPayload>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgenticServiceResult.Failed("connection refused"));

        var dto = await CreateService().RunAsync(workflow.Id, "bearer");

        Assert.Equal(AgentWorkflowStatus.Created, dto.Status);
        Assert.StartsWith("agentic_unavailable:", dto.FailureReason);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Advance_with_mismatched_reference_throws_not_found()
    {
        var workflow = Workflow(AgentWorkflowStatus.AwaitingExamination);
        Track(workflow);
        _workflows
            .Setup(r => r.ResolveConsultationIdAsync(
                AgentWorkflowEventType.ExaminationRecorded, "EX-OTHER",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("CON-OTHER");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService().AdvanceAsync(
                workflow.Id, AgentWorkflowEventType.ExaminationRecorded,
                "EX-OTHER", "bearer"));
        _agentic.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Trajectory_seq_continues_across_calls()
    {
        var workflow = Workflow(AgentWorkflowStatus.AwaitingExamination);
        workflow.Events.Add(new AgentWorkflowEvent
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            Seq = 5,
            Node = "finalize",
            EventType = "route",
            Timestamp = DateTime.UtcNow
        });
        Track(workflow);
        _workflows
            .Setup(r => r.ResolveConsultationIdAsync(
                AgentWorkflowEventType.ExaminationRecorded, "EX-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("CON-1");
        _agentic
            .Setup(c => c.AdvanceWorkflowAsync(
                workflow.Id.ToString(), It.IsAny<WorkflowAdvancePayload>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(new
            {
                status = AgentWorkflowStatus.AwaitingPrescription,
                steps = Array.Empty<object>(),
                trajectory = new[]
                {
                    new { seq = 0, node = "run_specialist", eventType = "step_done" }
                },
                trajectoryTotal = 6
            }));

        var dto = await CreateService().AdvanceAsync(
            workflow.Id, AgentWorkflowEventType.ExaminationRecorded, "EX-1", "bearer");

        var seqs = workflow.Events.Select(e => e.Seq).OrderBy(s => s).ToList();
        Assert.Equal(seqs, Enumerable.Range(1, 0).Concat(new[] { 5, 6, 7 }).ToList());
        Assert.Equal(AgentWorkflowStatus.AwaitingPrescription, dto.Status);
    }

    [Fact]
    public async Task Owner_status_requires_consultation_ownership()
    {
        _ownerAccess.Setup(o => o.IsPetOwner).Returns(true);
        _ownerAccess
            .Setup(o => o.OwnsConsultationAsync("CON-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var status = await CreateService().GetStatusForOwnerAsync("CON-1");

        Assert.Null(status);
        _workflows.Verify(r => r.GetByConsultationIdAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Start_is_idempotent_second_call_returns_same_workflow_id()
    {
        // One consultation -> one workflow for its whole lifecycle. A second
        // start must resolve the persisted row, not insert a duplicate.
        var consultation = new ConsultationRequest
        {
            Id = "CON-1",
            OrganizationId = Guid.NewGuid()
        };
        _consultations
            .Setup(r => r.GetByIdAsync("CON-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);

        AgentWorkflow? persisted = null;
        _workflows
            .Setup(r => r.GetByConsultationIdAsync("CON-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => persisted);
        _workflows
            .Setup(r => r.AddAsync(It.IsAny<AgentWorkflow>(), It.IsAny<CancellationToken>()))
            .Callback<AgentWorkflow, CancellationToken>((w, _) => persisted = w)
            .Returns(Task.CompletedTask);

        var first = await CreateService().StartAsync("CON-1");
        var second = await CreateService().StartAsync("CON-1");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("CON-1", second.ConsultationRequestId);
        _workflows.Verify(r => r.AddAsync(
            It.IsAny<AgentWorkflow>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Advance_resolves_reference_to_same_workflow()
    {
        // A later business event (examination_recorded) resolves through the
        // clinical chain back to THIS workflow's consultation — continuing
        // the same workflow id rather than starting a new one.
        var workflow = Workflow(AgentWorkflowStatus.AwaitingExamination);
        Track(workflow);
        _workflows
            .Setup(r => r.ResolveConsultationIdAsync(
                AgentWorkflowEventType.ExaminationRecorded, "EX-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("CON-1");
        _agentic
            .Setup(c => c.AdvanceWorkflowAsync(
                workflow.Id.ToString(), It.IsAny<WorkflowAdvancePayload>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(new
            {
                status = AgentWorkflowStatus.AwaitingPrescription,
                steps = Array.Empty<object>(),
                trajectory = Array.Empty<object>(),
                trajectoryTotal = 0
            }));

        var dto = await CreateService().AdvanceAsync(
            workflow.Id, AgentWorkflowEventType.ExaminationRecorded, "EX-1", "bearer");

        Assert.Equal(workflow.Id, dto.Id);
        Assert.Equal(AgentWorkflowStatus.AwaitingPrescription, dto.Status);
        _agentic.Verify(c => c.AdvanceWorkflowAsync(
            workflow.Id.ToString(),
            It.Is<WorkflowAdvancePayload>(p =>
                p.Event.Type == AgentWorkflowEventType.ExaminationRecorded &&
                p.Event.ReferenceId == "EX-1" &&
                p.Snapshot != null),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Advance_never_merges_specialist_steps_into_backend_owned_rows()
    {
        // Regression: ApplyRunResult used to pair incoming steps with persisted
        // rows by raw list index. The backend inserts its own row
        // (Task="backend:book_appointment") at the same StepNumber as the
        // graph's backend_action step, which shifted pairing so the next
        // specialist's metadata overwrote the backend row
        // (observed live as "diagnosis_agent/backend:book_appointment").
        var workflow = Workflow(AgentWorkflowStatus.AwaitingExamination);
        var now = DateTime.UtcNow;
        var steps = new[]
        {
            new AgentWorkflowStep { Id = Guid.NewGuid(), WorkflowId = workflow.Id,
                StepNumber = 1, AgentName = "consultation_agent", Task = "Triage",
                Status = "Completed", StartedAt = now },
            new AgentWorkflowStep { Id = Guid.NewGuid(), WorkflowId = workflow.Id,
                StepNumber = 2, AgentName = "scheduling_agent", Task = "Propose slot",
                Status = "Completed", StartedAt = now },
            new AgentWorkflowStep { Id = Guid.NewGuid(), WorkflowId = workflow.Id,
                StepNumber = 3, Task = "human_approval",
                Status = "Completed", StartedAt = now },
            new AgentWorkflowStep { Id = Guid.NewGuid(), WorkflowId = workflow.Id,
                StepNumber = 4, Task = "backend_action",
                Status = "Completed", StartedAt = now },
            // Backend-owned row at the SAME step number — the positional
            // merge used to hand this row to the next incoming specialist.
            new AgentWorkflowStep { Id = Guid.NewGuid(), WorkflowId = workflow.Id,
                StepNumber = 4, Task = "backend:book_appointment",
                Status = "Completed",
                OutputJson = "{\"appointmentId\":\"11111111-1111-1111-1111-111111111111\"}",
                StartedAt = now },
        };
        foreach (var s in steps) workflow.Steps.Add(s);
        Track(workflow);
        _workflows
            .Setup(r => r.ResolveConsultationIdAsync(
                AgentWorkflowEventType.ExaminationRecorded, "EX-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("CON-1");
        _agentic
            .Setup(c => c.AdvanceWorkflowAsync(
                workflow.Id.ToString(), It.IsAny<WorkflowAdvancePayload>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(new
            {
                status = AgentWorkflowStatus.AwaitingPrescription,
                steps = new object[]
                {
                    new { stepNumber = 1, agentName = "consultation_agent",
                          task = "Triage", status = "Completed" },
                    new { stepNumber = 2, agentName = "scheduling_agent",
                          task = "Propose slot", status = "Completed" },
                    new { stepNumber = 3, agentName = (string?)null,
                          task = "human_approval", status = "Completed" },
                    new { stepNumber = 4, agentName = (string?)null,
                          task = "backend_action", status = "Completed" },
                    new { stepNumber = 5, agentName = "diagnosis_agent",
                          task = "Diagnose from examination", status = "Completed" },
                },
                trajectory = Array.Empty<object>(),
                trajectoryTotal = 0
            }));

        var dto = await CreateService().AdvanceAsync(
            workflow.Id, AgentWorkflowEventType.ExaminationRecorded, "EX-1", "bearer");

        // The backend-owned booking row must be untouched.
        var booked = workflow.Steps.Single(s => s.Task == "backend:book_appointment");
        Assert.Null(booked.AgentName);
        Assert.Equal("Completed", booked.Status);
        Assert.Contains("appointmentId", booked.OutputJson);

        // The specialist landed on its own row, not the backend row.
        var diagnosis = Assert.Single(
            workflow.Steps.Where(s => s.AgentName == "diagnosis_agent"));
        Assert.Equal(5, diagnosis.StepNumber);
        Assert.Equal("Diagnose from examination", diagnosis.Task);
        Assert.Equal("Completed", diagnosis.Status);

        // No duplicate specialist rows; trajectory events still append.
        Assert.Equal(6, workflow.Steps.Count);
        Assert.Equal(AgentWorkflowStatus.AwaitingPrescription, dto.Status);
    }
}
