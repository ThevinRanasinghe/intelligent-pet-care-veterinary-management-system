public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Supplier supplier, CancellationToken ct = default);
}