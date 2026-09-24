using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Application.Interfaces;

public interface IMedicineReservationRepository
{
    Task<MedicineReservation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MedicineReservation>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(MedicineReservation reservation, CancellationToken ct = default);

    /// <summary>
    /// Atomically moves a reservation from <paramref name="from"/> to
    /// <paramref name="to"/> via a single conditional UPDATE. Returns the
    /// number of rows affected (0 = the reservation is not currently in
    /// <paramref name="from"/>, i.e. a concurrent transition already won).
    /// This is the concurrency guard for cancel/dispense — the equivalent
    /// of <c>IMedicineRepository.TryReserveAsync</c> for status changes.
    /// </summary>
    Task<int> TryTransitionAsync(
        Guid reservationId, ReservationStatus from, ReservationStatus to, CancellationToken ct = default);
}