using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Application.Services;

/// <summary>
/// Resolves the caller's Veterinarian profile through
/// ITenantContext.UserId -&gt; Veterinarian.UserId. Services use this instead
/// of trusting a client-supplied VeterinarianId on veterinarian-gated
/// workflows.
/// </summary>
public class CurrentVeterinarianResolver : ICurrentVeterinarianResolver
{
    private readonly ITenantContext _tenant;
    private readonly IVeterinarianRepository _veterinarians;

    public CurrentVeterinarianResolver(ITenantContext tenant, IVeterinarianRepository veterinarians)
    {
        _tenant = tenant;
        _veterinarians = veterinarians;
    }

    public async Task<Veterinarian?> TryResolveAsync(CancellationToken cancellationToken = default)
    {
        var userId = _tenant.UserId;
        return userId is null
            ? null
            : await _veterinarians.GetByUserIdAsync(userId.Value, cancellationToken);
    }

    public async Task<Veterinarian> ResolveRequiredAsync(CancellationToken cancellationToken = default)
        => await TryResolveAsync(cancellationToken)
            ?? throw new ForbiddenException("Your account is not linked to a veterinarian profile.");
}
