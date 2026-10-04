using PetCare.Application.DTOs.Consultations;

namespace PetCare.Application.Interfaces;

public interface IConsultationRequestService
{
    Task<ConsultationRequestDto> CreateAsync(
        CreateConsultationRequestDto dto);

    Task<List<ConsultationRequestDto>> GetAllAsync();

    Task<List<ConsultationRequestDto>> GetByOwnerIdAsync(
        string ownerId);

    Task<ConsultationRequestDto?> GetByIdAsync(
        string id);

    Task<ConsultationRequestDto?> UpdateAsync(
        string id,
        UpdateConsultationRequestDto dto);

    Task<ConsultationRequestDto?> SubmitAsync(
        string id);

    Task<bool> CancelAsync(
        string id);

    Task<List<ConsultationStatusHistoryDto>> GetStatusHistoryAsync(
        string consultationId);

    /// <summary>
    /// Advisory AI triage of a consultation request via the agentic
    /// consultation agent. Read-only: the caller's bearer token is
    /// forwarded so the agent's backend reads keep the caller's
    /// organization scope. Never returns null — failures produce a safe
    /// DTO marked Source="unavailable" so the review workflow is never
    /// blocked.
    /// </summary>
    Task<ConsultationAnalysisDto> GetAnalysisAsync(
        string id,
        string? bearerToken = null);

    Task<bool> ValidateOwnershipAsync(
        string petId,
        string ownerId);
}