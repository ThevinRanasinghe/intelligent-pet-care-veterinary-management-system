public interface IInventoryTransactionRepository
{
    Task AddAsync(InventoryTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryTransaction>> GetByMedicineAsync(Guid medicineId, CancellationToken ct = default);
}