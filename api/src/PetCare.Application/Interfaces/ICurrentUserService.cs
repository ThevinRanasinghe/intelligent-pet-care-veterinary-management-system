namespace PetCare.Application.Interfaces;

/// <summary>
/// Abstraction for accessing details of the currently authenticated user
/// derived securely from claims/JWT. Crucial for multi-tenant data isolation.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Unique ID of the authenticated user (sub).</summary>
    string? UserId { get; }

    /// <summary>Email of the authenticated user.</summary>
    string? Email { get; }

    /// <summary>Role of the authenticated user.</summary>
    string? Role { get; }

    /// <summary>
    /// Organization ID if user belongs to an organization (ClinicManager, Veterinarian, InventoryOfficer).
    /// Null for SuperAdmin or PetOwner.
    /// </summary>
    Guid? OrganizationId { get; }

    /// <summary>Whether the current request has an authenticated user.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Checks whether the current user is in the specified role.</summary>
    bool IsInRole(string role);
}
