using System.Text.Json;
using System.Text.Json.Serialization;

namespace PetCare.Application.DTOs.Agentic.Workflows;

// ------------------------------------------------------------------
// Payloads sent TO the Python service — property names are camelCase to
// match the FastAPI request models exactly.
// ------------------------------------------------------------------

public class WorkflowRunPayload
{
    [JsonPropertyName("consultationRequestId")]
    public string ConsultationRequestId { get; set; } = string.Empty;

    [JsonPropertyName("objective")]
    public string Objective { get; set; } = string.Empty;

    [JsonPropertyName("availableEvents")]
    public Dictionary<string, object?> AvailableEvents { get; set; } = new();

    [JsonPropertyName("snapshot")]
    public JsonElement? Snapshot { get; set; }

    [JsonPropertyName("eventRefs")]
    public Dictionary<string, string>? EventRefs { get; set; }
}

public class WorkflowResumePayload
{
    [JsonPropertyName("decision")]
    public WorkflowDecisionPayload Decision { get; set; } = new();

    [JsonPropertyName("snapshot")]
    public JsonElement? Snapshot { get; set; }
}

public class WorkflowDecisionPayload
{
    [JsonPropertyName("decision")]
    public string Decision { get; set; } = string.Empty;

    [JsonPropertyName("comments")]
    public string? Comments { get; set; }

    [JsonPropertyName("approverId")]
    public string? ApproverId { get; set; }

    [JsonPropertyName("decidedAt")]
    public string? DecidedAt { get; set; }
}

public class WorkflowAdvancePayload
{
    [JsonPropertyName("event")]
    public WorkflowEventPayload Event { get; set; } = new();

    [JsonPropertyName("availableEvents")]
    public Dictionary<string, object?> AvailableEvents { get; set; } = new();

    [JsonPropertyName("snapshot")]
    public JsonElement? Snapshot { get; set; }
}

public class WorkflowEventPayload
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("referenceId")]
    public string ReferenceId { get; set; } = string.Empty;
}
