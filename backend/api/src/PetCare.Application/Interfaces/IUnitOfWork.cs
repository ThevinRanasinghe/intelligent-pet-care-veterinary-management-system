using Microsoft.EntityFrameworkCore.Storage;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Wraps the persistence layer's SaveChanges so services can commit changes
/// made through multiple repositories without depending on EF Core/DbContext
/// directly.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an explicit database transaction on the shared context so a
    /// conditional write (e.g. atomic slot reservation) and the
    /// subsequent SaveChanges commit or roll back together.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
