namespace PetCare.Domain.Entities;

/// <summary>
/// Persisted state of one AI-supervised consultation workflow (the Python
/// supervisor's durable mirror). One workflow per consultation request.
/// All AI output is advisory — the backend remains authoritative for every
/// decision and mutation.
/// </summary>
public class AgentWorkflow
{
    public Guid Id { get; set; }

    /// <summary>The consultation this workflow coordinates (unique).</summary>
    public string ConsultationRequestId { get; set; } = string.Empty;

    public ConsultationRequest? ConsultationRequest { get; set; }

    /// <summary>The clinic that owns the consultation (tenant scope key).</summary>
    public Guid? OrganizationId { get; set; }

    public Organization? Organization { get; set; }

    public string Objective { get; set; } = string.Empty;

    /// <summary>See <see cref="Constants.AgentWorkflowStatus"/>.</summary>
    public string Status { get; set; } = Constants.AgentWorkflowStatus.Created;

    public int CurrentStep { get; set; }

    public string? PlanJson { get; set; }

    public string? ProposalJson { get; set; }

    public string? ApprovedActionJson { get; set; }

    public int DelegationCount { get; set; }

    public int RevisionCount { get; set; }

    public string? FailureReason { get; set; }

    public Guid? InitiatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public ICollection<AgentWorkflowStep> Steps { get; set; }
        = new List<AgentWorkflowStep>();

    public ICollection<AgentWorkflowApproval> Approvals { get; set; }
        = new List<AgentWorkflowApproval>();

    public ICollection<AgentWorkflowEvent> Events { get; set; }
        = new List<AgentWorkflowEvent>();
}
