public interface IMedicineRepository
{
    Task<Medicine?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Medicine> Items, int Total)> SearchAsync(
        string? search, string? category, bool? lowStockOnly,
        int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Medicine medicine, CancellationToken ct = default);
}