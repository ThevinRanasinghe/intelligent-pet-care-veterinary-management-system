namespace PetCare.Application.DTOs.Admin;

/// <summary>User account as shown to an Administrator.</summary>
public record AdminUserResponse
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool Active { get; init; }
    public bool MustChangePassword { get; init; }
    public Guid? OrganizationId { get; init; }
    public string? OrganizationName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Organization with lifecycle state and member count.</summary>
public record AdminOrganizationResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? RegistrationNumber { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string? RejectionReason { get; init; }
    public int UserCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Request to activate/deactivate a user account.</summary>
public record UpdateUserStatusRequest
{
    public bool Active { get; init; }
}

/// <summary>Request to transition an organization's lifecycle status.</summary>
public record UpdateOrganizationStatusRequest
{
    /// <summary>Target status: Active, Rejected, Suspended, or Inactive.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Required when rejecting or suspending; recorded on the organization.</summary>
    public string? Reason { get; init; }
}

/// <summary>Platform-level system information for the admin System Settings page.</summary>
public record AdminSystemInfoResponse
{
    public string Environment { get; init; } = string.Empty;
    public string DatabaseProvider { get; init; } = string.Empty;
    public DateTimeOffset ServerTimeUtc { get; init; }
    public int TotalUsers { get; init; }
    public int ActiveUsers { get; init; }
    public int InactiveUsers { get; init; }
    public int TotalOrganizations { get; init; }
    public int PendingOrganizations { get; init; }
    public int ActiveOrganizations { get; init; }
    public Dictionary<string, int> UsersByRole { get; init; } = new();
}
