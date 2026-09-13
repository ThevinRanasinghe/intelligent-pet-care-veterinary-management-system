using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Infrastructure.Repositories;

public class MedicineRepository : IMedicineRepository
{
    private readonly PetCareDbContext _context;

    public MedicineRepository(PetCareDbContext context) => _context = context;

    public Task<Medicine?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Medicines.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<(IReadOnlyList<Medicine> Items, int Total)> SearchAsync(
        string? search, string? category, bool? lowStockOnly,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Medicines.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => EF.Functions.ILike(m.Name, $"%{search}%"));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(m => m.Category == category);
        }

        if (lowStockOnly == true)
        {
            query = query.Where(m => (m.TotalQuantity - m.ReservedQuantity) < m.ReorderLevel);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(Medicine medicine, CancellationToken ct = default) =>
        await _context.Medicines.AddAsync(medicine, ct);

    public Task<int> TryReserveAsync(Guid medicineId, int quantity, CancellationToken ct = default) =>
        _context.Medicines
            .Where(m => m.Id == medicineId
                     && m.Status == MedicineStatus.Active
                     && (m.TotalQuantity - m.ReservedQuantity) >= quantity)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(m => m.ReservedQuantity, m => m.ReservedQuantity + quantity), ct);

    public Task<int> ReleaseReservedAsync(Guid medicineId, int quantity, CancellationToken ct = default) =>
        _context.Medicines
            .Where(m => m.Id == medicineId)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(m => m.ReservedQuantity, m => m.ReservedQuantity - quantity), ct);
}