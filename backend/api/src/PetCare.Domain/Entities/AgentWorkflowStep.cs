namespace PetCare.Domain.Entities;

/// <summary>
/// One recorded specialist/human/backend step inside an AgentWorkflow —
/// the durable mirror of the Python graph's StepRecord.
/// </summary>
public class AgentWorkflowStep
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public AgentWorkflow? Workflow { get; set; }

    public int StepNumber { get; set; }

    /// <summary>Specialist agent name, or null for human/backend steps.</summary>
    public string? AgentName { get; set; }

    public string Task { get; set; } = string.Empty;

    public string? InputSummaryJson { get; set; }

    public string? OutputJson { get; set; }

    public string? ToolCallsJson { get; set; }

    /// <summary>Pending | Completed | Failed | NoProposal | Skipped.</summary>
    public string Status { get; set; } = "Pending";

    public string? ValidationJson { get; set; }

    public string? Error { get; set; }

    public int RetryCount { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}
