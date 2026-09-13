using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Infrastructure.Repositories;

public class InventoryTransactionRepository : IInventoryTransactionRepository
{
    private readonly PetCareDbContext _context;

    public InventoryTransactionRepository(PetCareDbContext context) => _context = context;

    public async Task AddAsync(InventoryTransaction transaction, CancellationToken ct = default) =>
        await _context.InventoryTransactions.AddAsync(transaction, ct);

    public async Task<IReadOnlyList<InventoryTransaction>> GetByMedicineAsync(Guid medicineId, CancellationToken ct = default) =>
        await _context.InventoryTransactions
            .Where(t => t.MedicineId == medicineId)
            .OrderByDescending(t => t.OccurredAt)
            .ToListAsync(ct);
}