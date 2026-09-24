using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for PetOwner. Implemented in PetCare.Infrastructure
/// using EF Core; the Application layer never references EF Core directly.
/// </summary>
public interface IPetOwnerRepository
{
    /// <summary>The owner profile linked to the given user account (PetOwner.UserId FK).</summary>
    Task<PetOwner?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A staff-created owner profile with this email that is not yet linked
    /// to any user account (UserId IS NULL). Used at registration to attach
    /// pre-existing owner records to the newly created account.
    /// </summary>
    Task<PetOwner?> GetUnlinkedByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task AddAsync(PetOwner owner, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
