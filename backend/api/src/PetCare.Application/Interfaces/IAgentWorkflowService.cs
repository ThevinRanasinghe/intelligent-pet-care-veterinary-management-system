using PetCare.Application.DTOs.Agentic.Workflows;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Backend orchestration around the Python supervisor: persists workflow
/// state, forwards calls with the caller's bearer token, enforces tenant
/// isolation and the ClinicManager-only approval gate, and executes the
/// approved action through the authoritative consultation workflow.
/// The Python service is advisory — every mutation here goes through the
/// same services a human uses.
/// </summary>
public interface IAgentWorkflowService
{
    /// <summary>
    /// Idempotently creates a workflow for a submitted consultation
    /// (called from SubmitAsync — must never break the submit path).
    /// </summary>
    Task<AgentWorkflowDto?> EnsureCreatedForConsultationAsync(
        string consultationId, CancellationToken cancellationToken = default);

    /// <summary>Manual (idempotent) workflow creation for a consultation.</summary>
    Task<AgentWorkflowDto> StartAsync(
        string consultationId, CancellationToken cancellationToken = default);

    /// <summary>Runs/resumes the supervisor graph for a workflow.</summary>
    Task<AgentWorkflowDto> RunAsync(
        Guid workflowId, string? bearerToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a ClinicManager decision (Approved | Rejected |
    /// RevisionRequested) to the workflow's pending approval.
    /// </summary>
    Task<AgentWorkflowDto> DecideAsync(
        Guid workflowId, string decision, string? comments,
        string? bearerToken, CancellationToken cancellationToken = default);

    /// <summary>Asserts a business event (examination/prescription) into the workflow.</summary>
    Task<AgentWorkflowDto> AdvanceAsync(
        Guid workflowId, string eventType, string referenceId,
        string? bearerToken, CancellationToken cancellationToken = default);

    Task<AgentWorkflowDto?> GetAsync(Guid workflowId, CancellationToken cancellationToken = default);

    Task<AgentWorkflowDto?> GetByConsultationAsync(
        string consultationId, CancellationToken cancellationToken = default);

    /// <summary>Reduced status view — PetOwner must own the consultation.</summary>
    Task<AgentWorkflowStatusDto?> GetStatusForOwnerAsync(
        string consultationId, CancellationToken cancellationToken = default);

    Task<AgentWorkflowHistoryDto?> GetHistoryAsync(
        Guid workflowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The workflow whose consultation owns the given clinical reference
    /// (examination_recorded → examination id; prescription_created →
    /// treatment record id). Null when the reference does not resolve to an
    /// in-scope consultation or no workflow exists for it.
    /// </summary>
    Task<AgentWorkflowDto?> GetByClinicalEventReferenceAsync(
        string eventType, string referenceId, CancellationToken cancellationToken = default);
}
