using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

public class InventoryTransactionRepository : IInventoryTransactionRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public InventoryTransactionRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task AddAsync(InventoryTransaction transaction, CancellationToken ct = default) =>
        await _context.InventoryTransactions.AddAsync(transaction, ct);

    public async Task<IReadOnlyList<InventoryTransaction>> GetByMedicineAsync(Guid medicineId, CancellationToken ct = default)
    {
        var query = await _context.InventoryTransactions
            .ScopeToOrganizationAsync(_tenant, t => t.Medicine.OrganizationId, ct);
        return await query
            .Where(t => t.MedicineId == medicineId)
            .OrderByDescending(t => t.OccurredAt)
            .ToListAsync(ct);
    }
}