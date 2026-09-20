using PetCare.Domain.Common;
using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

/// <summary>
/// A veterinary organization (clinic/hospital) registered on the platform.
/// Each organization owns its own staff (ClinicManager, Veterinarian,
/// InventoryOfficer) and inventory. PetOwner and SuperAdmin accounts are
/// not affiliated with an organization.
/// </summary>
public class Organization : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? RegistrationNumber { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// Lifecycle state. New registrations start as
    /// <see cref="OrganizationStatus.Pending"/> and must be verified by a
    /// SuperAdmin before staff can log in.
    /// </summary>
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Pending;

    /// <summary>
    /// Convenience flag mirroring <see cref="Status"/> ==
    /// <see cref="OrganizationStatus.Active"/>. Kept as a separate column
    /// so queries can filter active organizations without casting the enum.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Free-text reason recorded when a registration is rejected or
    /// suspended. Null while the organization is pending or active.
    /// </summary>
    public string? RejectionReason { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}
