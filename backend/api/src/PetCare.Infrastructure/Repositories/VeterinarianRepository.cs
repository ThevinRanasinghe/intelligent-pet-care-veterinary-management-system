using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

public class VeterinarianRepository : IVeterinarianRepository
{
    private readonly PetCareDbContext _context;

    public VeterinarianRepository(PetCareDbContext context)
    {
        _context = context;
    }

    public Task<Veterinarian?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Veterinarians.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }
}
