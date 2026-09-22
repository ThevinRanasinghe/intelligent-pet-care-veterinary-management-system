using PetCare.Application.DTOs.Admin;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Application.Services;

public class AdminService : IAdminService
{
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;

    public AdminService(IUserRepository users, IOrganizationRepository organizations)
    {
        _users = users;
        _organizations = organizations;
    }

    public async Task<List<AdminUserResponse>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _users.GetAllAsync(cancellationToken);
        return users.Select(ToUserResponse).ToList();
    }

    public async Task<AdminUserResponse> SetUserActiveAsync(
        Guid userId, bool active, string callerEmail, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        if (!active && string.Equals(user.Email, callerEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("You cannot deactivate your own account.");
        }

        user.Active = active;
        await _users.SaveChangesAsync(cancellationToken);

        // Re-load so the response includes the organization name.
        var users = await _users.GetAllAsync(cancellationToken);
        return ToUserResponse(users.First(u => u.Id == userId));
    }

    public async Task<List<AdminOrganizationResponse>> GetOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await _organizations.GetAllAsync(cancellationToken);
        return organizations.Select(ToOrganizationResponse).ToList();
    }

    public async Task<AdminOrganizationResponse> SetOrganizationStatusAsync(
        Guid organizationId, string status, string? reason, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<OrganizationStatus>(status, ignoreCase: true, out var target)
            || target == OrganizationStatus.Pending)
        {
            throw new ArgumentException(
                "Status must be one of: Active, Rejected, Suspended, Inactive.");
        }

        if ((target == OrganizationStatus.Rejected || target == OrganizationStatus.Suspended)
            && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required when rejecting or suspending an organization.");
        }

        var organization = await _organizations.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new NotFoundException("Organization not found.");

        organization.Status = target;
        organization.IsActive = target == OrganizationStatus.Active;
        organization.RejectionReason = target == OrganizationStatus.Active
            ? null
            : reason?.Trim() ?? organization.RejectionReason;

        await _organizations.SaveChangesAsync(cancellationToken);

        var organizations = await _organizations.GetAllAsync(cancellationToken);
        return ToOrganizationResponse(organizations.First(o => o.Id == organizationId));
    }

    public IReadOnlyList<string> GetRoles() => Roles.All;

    public async Task<AdminSystemInfoResponse> GetSystemStatsAsync(CancellationToken cancellationToken = default)
    {
        var users = await _users.GetAllAsync(cancellationToken);
        var organizations = await _organizations.GetAllAsync(cancellationToken);

        return new AdminSystemInfoResponse
        {
            ServerTimeUtc = DateTimeOffset.UtcNow,
            TotalUsers = users.Count,
            ActiveUsers = users.Count(u => u.Active),
            InactiveUsers = users.Count(u => !u.Active),
            TotalOrganizations = organizations.Count,
            PendingOrganizations = organizations.Count(o => o.Status == OrganizationStatus.Pending),
            ActiveOrganizations = organizations.Count(o => o.Status == OrganizationStatus.Active),
            UsersByRole = users.GroupBy(u => u.Role).ToDictionary(g => g.Key, g => g.Count()),
        };
    }

    private static AdminUserResponse ToUserResponse(User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Name = string.IsNullOrWhiteSpace(user.Name)
            ? $"{user.FirstName} {user.LastName}".Trim()
            : user.Name,
        Email = user.Email,
        Role = user.Role,
        Active = user.Active,
        MustChangePassword = user.MustChangePassword,
        OrganizationId = user.OrganizationId,
        OrganizationName = user.Organization?.Name,
        CreatedAt = user.CreatedAt,
    };

    private static AdminOrganizationResponse ToOrganizationResponse(Organization organization) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        RegistrationNumber = organization.RegistrationNumber,
        Email = organization.Email,
        Phone = organization.Phone,
        Address = organization.Address,
        City = organization.City,
        Country = organization.Country,
        Status = organization.Status.ToString(),
        IsActive = organization.IsActive,
        RejectionReason = organization.RejectionReason,
        UserCount = organization.Users.Count,
        CreatedAt = organization.CreatedAt,
    };
}
