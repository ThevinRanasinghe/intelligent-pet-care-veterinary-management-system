using System.Text.Json;

namespace PetCare.Application.DTOs.Agentic.Workflows;

// ------------------------------------------------------------------
// Response DTOs — persisted JSON columns surface as parsed JsonElement
// so clients see structured data rather than escaped strings.
// ------------------------------------------------------------------

public class AgentWorkflowStepDto
{
    public Guid Id { get; set; }
    public int StepNumber { get; set; }
    public string? AgentName { get; set; }
    public string Task { get; set; } = string.Empty;
    public JsonElement? InputSummary { get; set; }
    public JsonElement? Output { get; set; }
    public JsonElement? ToolCalls { get; set; }
    public string Status { get; set; } = string.Empty;
    public JsonElement? Validation { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class AgentWorkflowApprovalDto
{
    public Guid Id { get; set; }
    public int StepNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public JsonElement? Proposal { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AgentWorkflowEventDto
{
    public int Seq { get; set; }
    public DateTime Timestamp { get; set; }
    public string Node { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public string EventType { get; set; } = string.Empty;
    public JsonElement? Detail { get; set; }
}

public class AgentWorkflowDto
{
    public Guid Id { get; set; }
    public string ConsultationRequestId { get; set; } = string.Empty;
    public Guid? OrganizationId { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentStep { get; set; }
    public JsonElement? Plan { get; set; }
    public JsonElement? Proposal { get; set; }
    public JsonElement? ApprovedAction { get; set; }
    public int DelegationCount { get; set; }
    public int RevisionCount { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<AgentWorkflowStepDto> Steps { get; set; } = new();
    public List<AgentWorkflowApprovalDto> Approvals { get; set; } = new();
}

public class AgentWorkflowHistoryDto
{
    public AgentWorkflowDto Workflow { get; set; } = new();
    public List<AgentWorkflowStepDto> Steps { get; set; } = new();
    public List<AgentWorkflowApprovalDto> Approvals { get; set; } = new();
    public List<AgentWorkflowEventDto> Events { get; set; } = new();
}

/// <summary>Reduced view for PetOwner callers — status only, no internals.</summary>
public class AgentWorkflowStatusDto
{
    public Guid WorkflowId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

// ------------------------------------------------------------------
// Request DTOs
// ------------------------------------------------------------------

public class StartAgentWorkflowRequest
{
    public string ConsultationRequestId { get; set; } = string.Empty;
}

public class WorkflowDecisionRequest
{
    public string? Comments { get; set; }
}

public class WorkflowEventRequest
{
    public string EventType { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
}
