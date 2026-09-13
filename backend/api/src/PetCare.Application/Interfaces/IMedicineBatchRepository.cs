public interface IMedicineBatchRepository
{
    Task<MedicineBatch?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MedicineBatch>> GetUsableByMedicineOrderedByExpiryAsync(
        Guid medicineId, CancellationToken ct = default); // FEFO order
    Task<IReadOnlyList<MedicineBatch>> GetExpiringSoonAsync(int withinDays, CancellationToken ct = default);
    Task AddAsync(MedicineBatch batch, CancellationToken ct = default);
}