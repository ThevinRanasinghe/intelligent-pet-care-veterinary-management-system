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
}
