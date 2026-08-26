using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly PetCareDbContext _context;

    public UserRepository(PetCareDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }
}
