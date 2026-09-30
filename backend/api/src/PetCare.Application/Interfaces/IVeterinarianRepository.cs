using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for Veterinarian. Implemented in
/// PetCare.Infrastructure using EF Core; the Application layer never
/// references EF Core directly.
/// </summary>
public interface IVeterinarianRepository
{
    Task<Veterinarian?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the veterinarian profile linked to a user account via
    /// Veterinarian.UserId. Identity lookup — intentionally not
    /// organization-scoped (a user always resolves their own profile).
    /// </summary>
    Task<Veterinarian?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Active veterinarians belonging to the given organization.</summary>
    Task<IReadOnlyList<Veterinarian>> GetActiveByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task AddAsync(Veterinarian veterinarian, CancellationToken cancellationToken = default);
}
