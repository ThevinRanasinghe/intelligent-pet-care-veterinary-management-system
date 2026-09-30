using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

public class VeterinarianRepository : IVeterinarianRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public VeterinarianRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<Veterinarian?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await _context.Veterinarians
            .ScopeToOrganizationAsync(_tenant, v => v.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<Veterinarian?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Identity resolution, not data access: a user must always find
        // their own veterinarian profile regardless of organization scope.
        return await _context.Veterinarians
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Veterinarian>> GetActiveByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _context.Veterinarians
            .Where(v => v.OrganizationId == organizationId && v.Active)
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Veterinarian veterinarian, CancellationToken cancellationToken = default)
    {
        _context.Veterinarians.Add(veterinarian);
        return Task.CompletedTask;
    }
}
