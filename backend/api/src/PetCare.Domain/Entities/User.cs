using PetCare.Domain.Common;

namespace PetCare.Domain.Entities;

/// <summary>
/// A user account for the shared authentication system. This is the "User"
/// entity referenced by <see cref="Approval.ReviewedBy"/> and
/// <see cref="ApprovalHistory.ChangedBy"/> (see
/// docs/database/scheduling-billing-approval-domain-model.md#approval,
/// "User referenced above ... owned by other modules"). Owned by the shared
/// authentication feature; other modules should only reference User.Id, not
/// duplicate this entity.
/// </summary>
public class User : AuditableEntity
{
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Full display name (e.g. "Jane Smith"). Kept for backwards
    /// compatibility with existing Scheduling/Billing/Approval code that
    /// reads <see cref="Name"/>; new code should prefer
    /// <see cref="FirstName"/>/<see cref="LastName"/>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Role name, e.g. <see cref="Roles.ClinicManager"/>. Stored as a plain
    /// string (not an enum) so additional roles can be introduced by other
    /// modules without a schema change.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    /// <summary>
    /// When true, the user must change their password on next login. Set
    /// for first-login accounts provisioned by an administrator.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Optional reference to the veterinary organization this user belongs
    /// to. Null for PetOwner and SuperAdmin accounts.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public Organization? Organization { get; set; }
}
