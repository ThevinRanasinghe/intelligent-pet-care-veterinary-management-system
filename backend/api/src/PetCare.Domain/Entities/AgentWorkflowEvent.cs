namespace PetCare.Domain.Entities;

/// <summary>
/// One trajectory event emitted by the Python supervisor during a run /
/// resume / advance call. Seq continues from the highest persisted value
/// so events stay strictly ordered across invocations.
/// </summary>
public class AgentWorkflowEvent
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public AgentWorkflow? Workflow { get; set; }

    public int Seq { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>The supervisor node that emitted the event.</summary>
    public string Node { get; set; } = string.Empty;

    public string? AgentName { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string? DetailJson { get; set; }
}
