namespace PetCare.Domain.Entities;

/// <summary>
/// One manager review gate of an AgentWorkflow. Created Pending with the
/// supervisor's proposal snapshot; exactly one decision may be applied —
/// a second decision on a decided approval is a conflict.
/// </summary>
public class AgentWorkflowApproval
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public AgentWorkflow? Workflow { get; set; }

    public int StepNumber { get; set; }

    /// <summary>Pending | Approved | Rejected | RevisionRequested.</summary>
    public string Status { get; set; } = Constants.AgentWorkflowApprovalStatus.Pending;

    /// <summary>Snapshot of the supervisor proposal under review.</summary>
    public string ProposalJson { get; set; } = string.Empty;

    public Guid? DecidedByUserId { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? Comments { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
