using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for Organization. Implemented in
/// PetCare.Infrastructure using EF Core.
/// </summary>
public interface IOrganizationRepository
{
    Task<List<Organization>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameOrEmailExistsAsync(string name, string email, CancellationToken cancellationToken = default);

    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
