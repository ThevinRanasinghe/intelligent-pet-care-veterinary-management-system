namespace PetCare.Application.Interfaces;

/// <summary>
/// Wraps the persistence layer's SaveChanges so services can commit changes
/// made through multiple repositories without depending on EF Core/DbContext
/// directly.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
