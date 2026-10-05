using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

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

    /// <summary>
    /// Tracked slot for one vet/date/start, if a row already exists — used to
    /// adopt a pre-posted Available slot instead of colliding with the unique
    /// (VeterinarianId, Date, StartTime) index.
    /// </summary>
    Task<AppointmentSlot?> GetByVeterinarianDateStartAsync(
        Guid veterinarianId, DateOnly date, TimeOnly startTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the given slots (any status), org-scoped — used by the
    /// multi-slot booking path to validate consecutive availability.
    /// </summary>
    Task<IReadOnlyList<AppointmentSlot>> GetManyByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically flips the given slots from Available to Reserved
    /// (WHERE Status = Available). Returns the number of rows updated;
    /// fewer than requested means another writer claimed a slot — the
    /// caller must treat that as a conflict.
    /// </summary>
    Task<int> TryReserveAvailableAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Slots in [start, end) for one vet/date with the given status —
    /// used to release or complete every slot a multi-slot appointment
    /// occupied, not just the anchor.
    /// </summary>
    Task<IReadOnlyList<AppointmentSlot>> GetByStatusInWindowAsync(
        Guid veterinarianId, DateOnly date, TimeOnly start, TimeOnly end,
        AppointmentSlotStatus status,
        CancellationToken cancellationToken = default);

    Task AddAsync(AppointmentSlot slot, CancellationToken cancellationToken = default);
}
