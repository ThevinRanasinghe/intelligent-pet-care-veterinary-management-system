using System.Security.Cryptography;
using FluentValidation;
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
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<CreateStaffUserRequest> _createStaffValidator;

    public AdminService(
        IUserRepository users,
        IOrganizationRepository organizations,
        IPasswordHasher passwordHasher,
        IValidator<CreateStaffUserRequest> createStaffValidator)
    {
        _users = users;
        _organizations = organizations;
        _passwordHasher = passwordHasher;
        _createStaffValidator = createStaffValidator;
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

    public Task<CreateStaffUserResponse> CreateVeterinarianAsync(
        CreateStaffUserRequest request, CancellationToken cancellationToken = default)
        => CreateStaffAccountAsync(request, Roles.Veterinarian, cancellationToken);

    public Task<CreateStaffUserResponse> CreateInventoryOfficerAsync(
        CreateStaffUserRequest request, CancellationToken cancellationToken = default)
        => CreateStaffAccountAsync(request, Roles.InventoryOfficer, cancellationToken);

    /// <summary>
    /// Shared staff-creation path — the role is fixed by the calling
    /// endpoint, never taken from the request. The organization must exist
    /// and be Active; OrganizationId is always the validated server-side
    /// value. Returns the generated temporary password once.
    /// </summary>
    private async Task<CreateStaffUserResponse> CreateStaffAccountAsync(
        CreateStaffUserRequest request, string role, CancellationToken cancellationToken)
    {
        await _createStaffValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = request.Email.Trim();
        if (await _users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ArgumentException("An account with this email already exists.");
        }

        var organization = await _organizations.GetByIdAsync(request.OrganizationId, cancellationToken)
            ?? throw new NotFoundException("Organization not found.");

        if (organization.Status != OrganizationStatus.Active)
        {
            throw new ArgumentException("Staff accounts can only be created for an Active organization.");
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(temporaryPassword),
            Name = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Role = role,
            Active = true,
            MustChangePassword = true,
            OrganizationId = organization.Id,
        };

        await _users.AddAsync(user, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return new CreateStaffUserResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Active = user.Active,
            MustChangePassword = user.MustChangePassword,
            OrganizationId = user.OrganizationId,
            OrganizationName = organization.Name,
            CreatedAt = user.CreatedAt,
            TemporaryPassword = temporaryPassword,
        };
    }

    /// <summary>
    /// Cryptographically random temporary password satisfying the platform's
    /// complexity policy (upper/lower/digit/special, 16+ chars).
    /// </summary>
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;

        var chars = new char[16];
        for (var i = 0; i < chars.Length - 1; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Guarantee at least one of each required class.
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[^1] = '!';

        return new string(chars);
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
