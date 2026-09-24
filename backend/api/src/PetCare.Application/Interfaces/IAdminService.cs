using PetCare.Application.DTOs.Admin;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Administrator (SuperAdmin) platform-management operations: user and
/// organization lifecycle administration plus system-level statistics.
/// </summary>
public interface IAdminService
{
    Task<List<AdminUserResponse>> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates or deactivates a user account. An administrator cannot
    /// deactivate their own account.
    /// </summary>
    Task<AdminUserResponse> SetUserActiveAsync(
        Guid userId, bool active, string callerEmail, CancellationToken cancellationToken = default);

    Task<List<AdminOrganizationResponse>> GetOrganizationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions an organization's lifecycle status (Active, Rejected,
    /// Suspended, Inactive). Rejected/Suspended require a reason.
    /// </summary>
    Task<AdminOrganizationResponse> SetOrganizationStatusAsync(
        Guid organizationId, string status, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Veterinarian account inside the selected (Active)
    /// organization. Role is fixed server-side; the account starts Active
    /// with MustChangePassword = true and a one-time temporary password.
    /// </summary>
    Task<CreateStaffUserResponse> CreateVeterinarianAsync(
        CreateStaffUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="CreateVeterinarianAsync"/> but for InventoryOfficer.</summary>
    Task<CreateStaffUserResponse> CreateInventoryOfficerAsync(
        CreateStaffUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Role catalog — the canonical role names used by the platform.</summary>
    IReadOnlyList<string> GetRoles();

    /// <summary>Aggregate user/organization statistics for dashboards and system info.</summary>
    Task<AdminSystemInfoResponse> GetSystemStatsAsync(CancellationToken cancellationToken = default);
}
