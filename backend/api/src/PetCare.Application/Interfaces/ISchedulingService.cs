using PetCare.Application.DTOs.Scheduling;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Scheduling use cases (appointments and slot availability). This is the
/// single business-rule surface shared by every client (React, Flutter, or
/// any future consumer) via ASP.NET Core; it never queries the database
/// directly, only through repository abstractions.
/// </summary>
public interface ISchedulingService
{
    Task<IReadOnlyList<AppointmentResponse>> GetAppointmentsAsync(CancellationToken cancellationToken = default);

    Task<AppointmentResponse?> GetAppointmentByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AppointmentResponse> CreateAppointmentAsync(CreateAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<AppointmentResponse> UpdateAppointmentAsync(Guid id, UpdateAppointmentRequest request, CancellationToken cancellationToken = default);

    Task CancelAppointmentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppointmentSlotResponse>> GetAvailableSlotsAsync(
        Guid? veterinarianId,
        DateOnly? date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the overlap rule (NewStart &lt; ExistingEnd AND NewEnd &gt; ExistingStart)
    /// against every non-cancelled appointment for the same veterinarian/date.
    /// Returns true if a conflict exists.
    /// </summary>
    Task<bool> CheckConflictAsync(ConflictCheckRequest request, CancellationToken cancellationToken = default);
}
