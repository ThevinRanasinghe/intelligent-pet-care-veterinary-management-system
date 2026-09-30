using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Resolves the Veterinarian profile linked to the current caller's user
/// account (ITenantContext.UserId -&gt; Veterinarian.UserId).
/// </summary>
public interface ICurrentVeterinarianResolver
{
    /// <summary>
    /// The caller's veterinarian profile, or null when the caller has no
    /// linked profile (non-veterinarian staff, unlinked accounts).
    /// </summary>
    Task<Veterinarian?> TryResolveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The caller's veterinarian profile. Throws ForbiddenException when the
    /// caller has no linked profile — used on veterinarian-gated workflows.
    /// </summary>
    Task<Veterinarian> ResolveRequiredAsync(CancellationToken cancellationToken = default);
}
