using PetCare.Application.Interfaces;

namespace PetCare.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly PetCareDbContext _context;

    public UnitOfWork(PetCareDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
