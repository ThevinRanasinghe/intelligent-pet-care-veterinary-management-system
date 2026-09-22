using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

public interface IMedicineReservationRepository
{
    Task<MedicineReservation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MedicineReservation>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(MedicineReservation reservation, CancellationToken ct = default);
}