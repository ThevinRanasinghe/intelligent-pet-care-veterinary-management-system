using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

public class PetOwnerRepository : IPetOwnerRepository
{
    private readonly PetCareDbContext _context;

    public PetOwnerRepository(PetCareDbContext context)
    {
        _context = context;
    }

    public Task<PetOwner?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.PetOwners.FirstOrDefaultAsync(o => o.UserId == userId, cancellationToken);
    }

    public Task<PetOwner?> GetUnlinkedByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLower();
        return _context.PetOwners
            .FirstOrDefaultAsync(o => o.UserId == null && o.Email.ToLower() == normalized, cancellationToken);
    }

    public Task<PetOwner?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Case-insensitive comparison (Postgres '=' is case-sensitive) —
        // "Amal@X.lk" and "amal@x.lk" are the same owner for dedupe purposes.
        var normalized = email.Trim().ToLower();
        return _context.PetOwners
            .FirstOrDefaultAsync(o => o.Email.ToLower() == normalized, cancellationToken);
    }

    public async Task AddAsync(PetOwner owner, CancellationToken cancellationToken = default)
    {
        await _context.PetOwners.AddAsync(owner, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
