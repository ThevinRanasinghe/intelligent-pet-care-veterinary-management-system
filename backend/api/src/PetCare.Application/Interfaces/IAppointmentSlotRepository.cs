using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for AppointmentSlot. Implemented in
/// PetCare.Infrastructure using EF Core; the Application layer never
/// references EF Core directly.
/// </summary>
public interface IAppointmentSlotRepository
{
    Task<AppointmentSlot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns Available slots, optionally filtered by veterinarian and/or date.
    /// </summary>
    Task<IReadOnlyList<AppointmentSlot>> GetAvailableAsync(
        Guid? veterinarianId,
        DateOnly? date,
        CancellationToken cancellationToken = default);
}
