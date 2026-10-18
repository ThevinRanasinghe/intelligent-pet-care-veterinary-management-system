using PetCare.Application.DTOs.Consultations;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Walk-in intake for clinic staff: resolves or registers the owner and pet,
/// then files, submits and assigns a consultation request so the resulting
/// records are identical to an online booking — only Source differs.
/// </summary>
public interface IWalkInBookingService
{
    /// <summary>
    /// One unit of work: owner resolution (email reuse) or registration, pet
    /// resolution or registration, Draft consultation → mark Source=WalkIn →
    /// Submit → veterinarian assignment (slot reservation + Confirmed
    /// appointment + AppointmentConfirmed transition).
    /// </summary>
    Task<WalkInBookingResponse> CreateAsync(
        CreateWalkInBookingRequest request,
        CancellationToken cancellationToken = default);
}
