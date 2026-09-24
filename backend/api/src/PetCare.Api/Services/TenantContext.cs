using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Infrastructure;

namespace PetCare.Api.Services;

/// <summary>
/// Resolves the authenticated caller's organization from the JWT sub claim
/// and the Users table. Scoped per request; the organization lookup is done
/// once and cached.
/// </summary>
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PetCareDbContext _db;

    private bool _organizationIdResolved;
    private Guid? _organizationId;

    public TenantContext(IHttpContextAccessor httpContextAccessor, PetCareDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public Guid? UserId
    {
        get
        {
            var idValue = _httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier)
                ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

            return Guid.TryParse(idValue, out var id) ? id : null;
        }
    }

    public bool IsPlatformAdmin =>
        _httpContextAccessor.HttpContext?.User.IsInRole(Roles.SuperAdmin) == true;

    public bool IsOrganizationScoped =>
        UserId is not null
        && !IsPlatformAdmin
        && _httpContextAccessor.HttpContext?.User.IsInRole(Roles.PetOwner) != true;

    public async Task<Guid?> GetOrganizationIdAsync(CancellationToken cancellationToken = default)
    {
        if (_organizationIdResolved)
        {
            return _organizationId;
        }

        var userId = UserId;
        _organizationId = userId is null
            ? null
            : await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.OrganizationId)
                .FirstOrDefaultAsync(cancellationToken);

        _organizationIdResolved = true;
        return _organizationId;
    }
}
