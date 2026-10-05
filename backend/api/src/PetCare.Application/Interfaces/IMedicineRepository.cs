using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

public interface IMedicineRepository
{
    Task<Medicine?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Medicine> Items, int Total)> SearchAsync(
        string? search, string? category, bool? lowStockOnly,
        int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Medicine medicine, CancellationToken ct = default);
    Task<int> TryReserveAsync(Guid medicineId, int quantity, CancellationToken ct = default);
    Task<int> ReleaseReservedAsync(Guid medicineId, int quantity, CancellationToken ct = default);
    /// <summary>
    /// Re-reads a tracked medicine's current values from the database. Needed
    /// when an atomic UPDATE (TryReserveAsync/ReleaseReservedAsync) changed the
    /// row after the entity was loaded in the same context.
    /// </summary>
    Task RefreshCurrentValuesAsync(Medicine medicine, CancellationToken ct = default);
}