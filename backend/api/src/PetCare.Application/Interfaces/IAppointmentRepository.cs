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

    /// <summary>
    /// Non-cancelled appointments for a set of veterinarians over a date
    /// range — powers the org-level availability lookup (one query for a
    /// whole month). Deliberately NOT organization-scoped: the veterinarian
    /// id set already constrains the rows, and PetOwner callers (who have no
    /// organization) use it to book at any clinic.
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetActiveByVeterinariansAndRangeAsync(
        IReadOnlyCollection<Guid> veterinarianIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>All appointments attended by the given veterinarian.</summary>
    Task<IReadOnlyList<Appointment>> GetByVeterinarianAsync(
        Guid veterinarianId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// All appointments for pets owned by the given owner id. Deliberately
    /// NOT organization-scoped — PetOwner callers have no organization.
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetByOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);
}
