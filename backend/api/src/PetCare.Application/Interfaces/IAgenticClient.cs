using PetCare.Application.DTOs.Agentic;

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
}
