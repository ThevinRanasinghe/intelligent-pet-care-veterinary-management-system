using PetCare.Application.DTOs.Consultations;
using PetCare.Application.DTOs.Scheduling;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Consultation-request workflow operations that span scheduling: assigning
/// a veterinarian (creates the slot + appointment) and veterinarian-requested
/// follow-up consultations.
/// </summary>
public interface IConsultationWorkflowService
{
    /// <summary>
    /// Assigns a veterinarian to a Submitted/Processing consultation request:
    /// creates a Reserved slot + Confirmed appointment, then moves the
    /// request to AppointmentConfirmed. One unit of work.
    /// </summary>
    Task<AppointmentResponse> AssignConsultationAsync(
        string consultationId,
        AssignVeterinarianRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a veterinarian-requested follow-up consultation request
    /// (RequestType = FollowUp, Status = Submitted) for a pet.
    /// </summary>
    Task<ConsultationRequestDto> CreateFollowUpAsync(
        CreateFollowUpRequest request,
        CancellationToken cancellationToken = default);
}
