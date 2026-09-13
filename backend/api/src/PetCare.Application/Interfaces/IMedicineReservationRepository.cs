public interface IMedicineReservationRepository
{
    Task<MedicineReservation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(MedicineReservation reservation, CancellationToken ct = default);
}