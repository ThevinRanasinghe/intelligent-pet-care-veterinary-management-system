using PetCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Repositories;

public class MedicineBatchRepository : IMedicineBatchRepository
{
    private readonly PetCareDbContext _context;

    public MedicineBatchRepository(PetCareDbContext context) => _context = context;

    public Task<MedicineBatch?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.MedicineBatches.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<MedicineBatch>> GetUsableByMedicineOrderedByExpiryAsync(
        Guid medicineId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await _context.MedicineBatches
            .Where(b => b.MedicineId == medicineId
                     && b.Status == BatchStatus.Active
                     && b.Quantity > 0
                     && b.ExpiryDate > today)
            .OrderBy(b => b.ExpiryDate) // FEFO
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MedicineBatch>> GetExpiringSoonAsync(int withinDays, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var threshold = today.AddDays(withinDays);

        return await _context.MedicineBatches
            .Where(b => b.Status == BatchStatus.Active
                     && b.Quantity > 0
                     && b.ExpiryDate > today
                     && b.ExpiryDate <= threshold)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MedicineBatch>> GetExpiredAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await _context.MedicineBatches
            .Where(b => b.Status == BatchStatus.Active
                     && b.Quantity > 0
                     && b.ExpiryDate <= today) // already expired
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MedicineBatch>> GetByMedicineAsync(Guid medicineId, CancellationToken ct = default) =>
        await _context.MedicineBatches
            .Where(b => b.MedicineId == medicineId)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(ct);

    public async Task AddAsync(MedicineBatch batch, CancellationToken ct = default) =>
        await _context.MedicineBatches.AddAsync(batch, ct);
}