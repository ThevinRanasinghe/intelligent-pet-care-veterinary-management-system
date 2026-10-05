using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly PetCareDbContext _context;

    public OrganizationRepository(PetCareDbContext context)
    {
        _context = context;
    }

    public Task<List<Organization>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _context.Organizations
            .Include(o => o.Users)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Organizations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public Task<bool> NameOrEmailExistsAsync(string name, string email, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return _context.Organizations.AnyAsync(
            o => o.Name.ToLower() == normalizedName || o.Email.ToLower() == normalizedEmail,
            cancellationToken);
    }

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        await _context.Organizations.AddAsync(organization, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
