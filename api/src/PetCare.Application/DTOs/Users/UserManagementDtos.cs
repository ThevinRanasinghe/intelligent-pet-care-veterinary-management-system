using PetCare.Application.DTOs.Auth;
using PetCare.Domain.Enums;

namespace PetCare.Application.DTOs.Users;

/// <summary>
/// Staff member representation for organization staff management.
/// </summary>
public sealed record StaffUserDto(
    string Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Role,
    string Status,
    Guid OrganizationId,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    bool MustChangePassword = false,
    string? OrganizationName = null
);

/// <summary>
/// Request to create a new staff account (Veterinarian or InventoryOfficer) by ClinicManager.
/// </summary>
public sealed record CreateStaffUserRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string Role,
    string Password,
    string? PhoneNumber = null
);

/// <summary>
/// Request to update staff details.
/// </summary>
public sealed record UpdateStaffUserRequestDto(
    string FirstName,
    string LastName,
    string? PhoneNumber = null
);

/// <summary>
/// Request to activate or disable a staff member.
/// </summary>
public sealed record UpdateStaffStatusRequestDto(
    UserAccountStatus Status
);

/// <summary>
/// Request by ClinicManager to reset a staff member's temporary password.
/// </summary>
public sealed record ResetStaffPasswordRequestDto(
    string TemporaryPassword
);

/// <summary>
/// Authenticated user's own profile.
/// </summary>
public sealed record UserProfileDto(
    string Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Role,
    string Status,
    OrganizationDto? Organization,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    bool MustChangePassword = false
);

/// <summary>
/// Request to update authenticated user's own profile.
/// Role, OrganizationId, and Status can NEVER be changed here.
/// </summary>
public sealed record UpdateProfileRequestDto(
    string FirstName,
    string LastName,
    string? PhoneNumber = null
);

/// <summary>
/// Request to change password for authenticated user.
/// </summary>
public sealed record ChangePasswordRequestDto(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
);

