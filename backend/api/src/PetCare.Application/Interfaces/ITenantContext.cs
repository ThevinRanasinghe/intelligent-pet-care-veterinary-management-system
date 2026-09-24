namespace PetCare.Application.Interfaces;

/// <summary>
/// Resolved tenant (organization) context for the current call.
///
/// Organization scoping applies only to authenticated staff callers
/// (Veterinarian, InventoryOfficer, ClinicManager, Staff): queries against
/// organization-owned data are filtered to the caller's
/// <see cref="GetOrganizationIdAsync"/>. The platform Administrator is
/// unscoped (cross-organization visibility). PetOwner callers are not
/// organization-scoped — their access is controlled by owner-based checks
/// (<see cref="IOwnerAccessService"/>). Unauthenticated/system callers
/// (seeders, tests, background work) are unscoped; endpoint authorization
/// still guards who can reach the data.
/// </summary>
public interface ITenantContext
{
    /// <summary>The authenticated caller's user id (JWT sub), if present.</summary>
    Guid? UserId { get; }

    /// <summary>True when the caller is the platform Administrator.</summary>
    bool IsPlatformAdmin { get; }

    /// <summary>
    /// True when organization-owned queries must be filtered to the caller's
    /// organization (authenticated non-admin, non-PetOwner caller).
    /// </summary>
    bool IsOrganizationScoped { get; }

    /// <summary>
    /// The caller's organization id resolved from their user record, or null
    /// when the caller has no organization. Rows whose OrganizationId is
    /// null are "unassigned" and remain visible to scoped callers.
    /// </summary>
    Task<Guid?> GetOrganizationIdAsync(CancellationToken cancellationToken = default);
}
