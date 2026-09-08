using Microsoft.AspNetCore.Identity;

namespace PetCare.Infrastructure.Entities;

/// <summary>
/// Extends ASP.NET Core Identity user with PetCare-specific profile fields.
/// Identity already stores: Email, UserName, PasswordHash, SecurityStamp,
/// ConcurrencyStamp, PhoneNumber, LockoutEnd, AccessFailedCount, etc.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>User's first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>User's last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this account is active. SuperAdmin can deactivate accounts.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last profile update.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Computed full name (not persisted).</summary>
    public string FullName => $"{FirstName} {LastName}".Trim();
}
