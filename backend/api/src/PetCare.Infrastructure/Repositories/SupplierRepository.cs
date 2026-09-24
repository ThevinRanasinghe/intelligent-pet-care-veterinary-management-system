using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public SupplierRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var query = await _context.Suppliers
            .ScopeToOrganizationAsync(_tenant, s => s.OrganizationId, ct);
        return await query.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken ct = default)
    {
        var query = await _context.Suppliers
            .ScopeToOrganizationAsync(_tenant, s => s.OrganizationId, ct);
        return await query.OrderBy(s => s.Name).ToListAsync(ct);
    }

    public async Task AddAsync(Supplier supplier, CancellationToken ct = default) =>
        await _context.Suppliers.AddAsync(supplier, ct);
}