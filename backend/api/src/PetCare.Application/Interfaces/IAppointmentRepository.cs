using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for Appointment. Implemented in
/// PetCare.Infrastructure using EF Core; the Application layer never
/// references EF Core directly.
/// </summary>
public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Appointment>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Non-cancelled appointments for a veterinarian on a given date, used by
    /// the overlap/conflict check (SchedulingService.CheckConflictAsync).
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetActiveByVeterinarianAndDateAsync(
        Guid veterinarianId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);
}
