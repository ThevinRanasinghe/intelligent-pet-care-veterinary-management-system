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

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Role name, e.g. <see cref="Roles.ClinicManager"/>. Stored as a plain
    /// string (not an enum) so additional roles can be introduced by other
    /// modules without a schema change.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public bool Active { get; set; } = true;
}
