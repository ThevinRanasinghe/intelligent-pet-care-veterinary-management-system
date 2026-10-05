using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for the AgentWorkflow aggregate (workflow +
/// steps + approvals + events). Implemented in PetCare.Infrastructure
/// using EF Core. Reads are organization-scoped for staff callers: a
/// foreign-organization workflow resolves to null.
/// </summary>
public interface IAgentWorkflowRepository
{
    /// <summary>Tracked workflow without children — for status updates.</summary>
    Task<AgentWorkflow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked workflow with Steps + Approvals + Events loaded.</summary>
    Task<AgentWorkflow?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked workflow (with details) for a consultation, if one exists.</summary>
    Task<AgentWorkflow?> GetByConsultationIdAsync(string consultationRequestId, CancellationToken cancellationToken = default);

    /// <summary>Read-only detail load for history views (no tracking).</summary>
    Task<AgentWorkflow?> GetDetailsNoTrackingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Latest workflow status per consultation id — a single grouped query
    /// for list endpoints.
    /// </summary>
    Task<Dictionary<string, string>> GetStatusesAsync(
        IEnumerable<string> consultationRequestIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the consultation a clinical reference belongs to, or null:
    /// examination_recorded → Examination.ConsultationRequestId;
    /// prescription_created → TreatmentRecord → Diagnosis → Examination →
    /// ConsultationRequestId. Scoped to the caller's organization.
    /// </summary>
    Task<string?> ResolveConsultationIdAsync(
        string eventType, string referenceId, CancellationToken cancellationToken = default);

    Task AddAsync(AgentWorkflow workflow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks a new child row in the Added state. Required because
    /// navigation-fixup alone treats a child with a pre-set key as an
    /// existing row (Modified), which fails on the first save.
    /// </summary>
    void AddStep(AgentWorkflowStep step);

    /// <summary>Tracks a new approval row in the Added state.</summary>
    void AddApproval(AgentWorkflowApproval approval);

    /// <summary>Tracks a new trajectory event row in the Added state.</summary>
    void AddEvent(AgentWorkflowEvent workflowEvent);
}
