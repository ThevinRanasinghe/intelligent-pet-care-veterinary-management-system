using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

/// <summary>
/// Represents an independent veterinary organization registered on the Beacon platform.
/// Each organization maintains its own staff, inventory, patients, and operational data.
/// </summary>
public sealed class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Official organization / clinic name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Business or veterinary registration number (optional).</summary>
    public string? RegistrationNumber { get; set; }

    /// <summary>Official contact email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Official contact phone number.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Physical street address.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>City where the clinic/hospital operates.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Country where the clinic/hospital operates.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Current lifecycle status of the organization.</summary>
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;

    /// <summary>Whether the organization is actively operating on Beacon.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the organization registered.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last details update.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
