using System.Text.Json;
using System.Text.Json.Serialization;

namespace PetCare.Application.DTOs.Agentic.Workflows;

/// <summary>
/// Parsed mirror of the Python supervisor's WorkflowRunResponse. Only the
/// fields the backend persists are mapped; extra fields are ignored.
/// </summary>
public class AgenticWorkflowRunResult
{
    public string? WorkflowId { get; set; }
    public string Status { get; set; } = string.Empty;
    public JsonElement? Plan { get; set; }
    public List<AgenticStepRecord> Steps { get; set; } = new();
    public List<AgenticTrajectoryEvent> Trajectory { get; set; } = new();
    public int TrajectoryTotal { get; set; }
    public JsonElement? Proposal { get; set; }
    public JsonElement? ApprovedAction { get; set; }
    public int DelegationCount { get; set; }
    public int RevisionCount { get; set; }
    public string? FailureReason { get; set; }
    public bool Interrupted { get; set; }
}

public class AgenticStepRecord
{
    public int StepNumber { get; set; }
    public string? AgentName { get; set; }
    public string? Task { get; set; }
    public JsonElement? InputSummary { get; set; }
    public JsonElement? Output { get; set; }
    public JsonElement? ToolCalls { get; set; }
    public JsonElement? Validation { get; set; }
    public string? Status { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class AgenticTrajectoryEvent
{
    public int Seq { get; set; }
    public DateTime? Timestamp { get; set; }
    public string Node { get; set; } = string.Empty;
    public string? Agent { get; set; }
    [JsonPropertyName("event")]
    public string EventType { get; set; } = string.Empty;
    public JsonElement? Detail { get; set; }
}
