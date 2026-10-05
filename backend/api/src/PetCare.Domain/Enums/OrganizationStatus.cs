namespace PetCare.Domain.Enums;

/// <summary>
/// Lifecycle state of a veterinary organization on the platform.
/// </summary>
public enum OrganizationStatus
{
    /// <summary>Registration submitted, awaiting SuperAdmin verification.</summary>
    Pending = 0,

    /// <summary>Verified and operational.</summary>
    Active = 1,

    /// <summary>Registration rejected (see Organization.RejectionReason).</summary>
    Rejected = 2,

    /// <summary>Temporarily deactivated by a SuperAdmin.</summary>
    Suspended = 3,

    /// <summary>Permanently deactivated.</summary>
    Inactive = 4,
}
