using PetCare.Application.DTOs.Agentic;
using PetCare.Application.DTOs.Agentic.Workflows;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Client for the internal Agentic AI service (FastAPI/LangGraph/Gemini).
/// The service is advisory and read-only: it analyses PetCare data and
/// returns recommendations — it never writes business state.
///
/// The caller's bearer token is forwarded so the agent's backend reads
/// keep the caller's role and organisation scope. This seam is wired in
/// Phase 1; endpoints that expose agents are added in later phases.
/// </summary>
public interface IAgenticClient
{
    /// <summary>Advisory triage of a consultation request.</summary>
    Task<AgenticServiceResult> AnalyzeConsultationAsync(
        string consultationId, string? bearerToken, CancellationToken cancellationToken = default);

    /// <summary>Advisory diagnosis assistance for an examination.</summary>
    Task<AgenticServiceResult> AnalyzeDiagnosisAsync(
        string examinationId, string? bearerToken, CancellationToken cancellationToken = default);

    /// <summary>Advisory appointment + quotation proposal for a consultation request.</summary>
    Task<AgenticServiceResult> PlanSchedulingAsync(
        string requestId, string? bearerToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advisory inventory/medicine recommendation. The identifier is a
    /// treatment record id — a medicine request is the batch of
    /// prescription items created under that treatment record.
    /// </summary>
    Task<AgenticServiceResult> PlanInventoryAsync(
        string treatmentRecordId, string? bearerToken, CancellationToken cancellationToken = default);

    /// <summary>Starts (or continues) a supervisor workflow run.</summary>
    Task<AgenticServiceResult> RunWorkflowAsync(
        string workflowId, WorkflowRunPayload payload, string? bearerToken,
        CancellationToken cancellationToken = default);

    /// <summary>Applies the manager's approval decision to a paused workflow.</summary>
    Task<AgenticServiceResult> ResumeWorkflowAsync(
        string workflowId, WorkflowResumePayload payload, string? bearerToken,
        CancellationToken cancellationToken = default);

    /// <summary>Asserts a business event (examination/prescription) into a workflow.</summary>
    Task<AgenticServiceResult> AdvanceWorkflowAsync(
        string workflowId, WorkflowAdvancePayload payload, string? bearerToken,
        CancellationToken cancellationToken = default);
}
