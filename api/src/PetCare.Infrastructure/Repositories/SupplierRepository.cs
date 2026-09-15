using PetCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly PetCareDbContext _context;

    public SupplierRepository(PetCareDbContext context) => _context = context;

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Suppliers.OrderBy(s => s.Name).ToListAsync(ct);

    public async Task AddAsync(Supplier supplier, CancellationToken ct = default) =>
        await _context.Suppliers.AddAsync(supplier, ct);
}