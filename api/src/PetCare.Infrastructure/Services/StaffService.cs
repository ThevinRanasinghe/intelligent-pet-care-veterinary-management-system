using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Users;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Entities;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Infrastructure.Services;

/// <summary>
/// Implements staff account management for Clinic Managers.
/// Strictly enforces organization multi-tenant data isolation:
/// - OrganizationId is derived solely from the authenticated JWT claims via ICurrentUserService.
/// - Client-supplied OrganizationIds are never trusted.
/// - Cross-organization queries and mutations are strictly blocked.
/// </summary>
public sealed class StaffService : IStaffService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PetCareDbContext            _dbContext;
    private readonly ICurrentUserService         _currentUser;

    public StaffService(
        UserManager<ApplicationUser> userManager,
        PetCareDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _dbContext   = dbContext;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StaffUserDto>> GetOrganizationStaffAsync(
        string? search,
        string? role,
        string? status,
        CancellationToken ct = default)
    {
        var orgId = await GetScopeOrganizationIdAsync(ct);

        var query = _dbContext.Users.AsNoTracking();
        if (orgId.HasValue)
        {
            query = query.Where(u => u.OrganizationId == orgId.Value);
        }
        else
        {
            query = query.Where(u => u.OrganizationId != null);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => u.FirstName.ToLower().Contains(term)
                                  || u.LastName.ToLower().Contains(term)
                                  || (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<UserAccountStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(u => u.AccountStatus == parsedStatus);
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(ct);

        var staffDtos = new List<StaffUserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var userRole = roles?.FirstOrDefault() ?? "Staff";

            if (!string.IsNullOrWhiteSpace(role) && !string.Equals(userRole, role, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            staffDtos.Add(MapToStaffDto(user, userRole));
        }

        return staffDtos;
    }

    /// <inheritdoc />
    public async Task<StaffUserDto> GetStaffMemberByIdAsync(
        string staffId,
        CancellationToken ct = default)
    {
        var orgId = await GetScopeOrganizationIdAsync(ct);

        var query = _dbContext.Users.AsNoTracking();
        if (orgId.HasValue)
        {
            query = query.Where(u => u.OrganizationId == orgId.Value);
        }

        var user = await query.FirstOrDefaultAsync(u => u.Id == staffId, ct);
        if (user is null)
        {
            throw new KeyNotFoundException("Staff member not found in your organization.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userRole = roles?.FirstOrDefault() ?? "Staff";

        return MapToStaffDto(user, userRole);
    }

    /// <inheritdoc />
    public async Task<StaffUserDto> CreateStaffMemberAsync(
        CreateStaffUserRequestDto request,
        CancellationToken ct = default)
    {
        var orgId = await GetScopeOrganizationIdAsync(ct);

        // Strict role allow-list
        if (request.Role is not (Roles.Veterinarian or Roles.InventoryOfficer))
        {
            throw new InvalidOperationException(
                $"Clinic Managers can only create '{Roles.Veterinarian}' or '{Roles.InventoryOfficer}' accounts.");
        }

        var assignedOrgId = orgId ?? _currentUser.OrganizationId;
        if (!assignedOrgId.HasValue)
        {
            throw new InvalidOperationException("An organization must be specified to create staff.");
        }

        // Check for duplicate email
        var existingUser = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (existingUser is not null)
        {
            throw new InvalidOperationException("An account with this email address already exists.");
        }

        var staffUser = new ApplicationUser
        {
            UserName           = request.Email.Trim(),
            Email              = request.Email.Trim(),
            FirstName          = request.FirstName.Trim(),
            LastName           = request.LastName.Trim(),
            PhoneNumber        = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            OrganizationId     = assignedOrgId.Value,
            AccountStatus      = UserAccountStatus.Active,
            IsActive           = true,
            MustChangePassword = true,
            CreatedAt          = DateTime.UtcNow,
            UpdatedAt          = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(staffUser, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        var roleResult = await _userManager.AddToRoleAsync(staffUser, request.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(staffUser);
            throw new InvalidOperationException("Failed to assign staff role. Account creation rolled back.");
        }

        return MapToStaffDto(staffUser, request.Role);
    }

    /// <inheritdoc />
    public async Task<StaffUserDto> UpdateStaffMemberAsync(
        string staffId,
        UpdateStaffUserRequestDto request,
        CancellationToken ct = default)
    {
        var orgId = await GetScopeOrganizationIdAsync(ct);

        var query = _dbContext.Users.AsQueryable();
        if (orgId.HasValue)
        {
            query = query.Where(u => u.OrganizationId == orgId.Value);
        }

        var user = await query.FirstOrDefaultAsync(u => u.Id == staffId, ct);
        if (user is null)
        {
            throw new KeyNotFoundException("Staff member not found in your organization.");
        }

        user.FirstName   = request.FirstName.Trim();
        user.LastName    = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.UpdatedAt   = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userRole = roles?.FirstOrDefault() ?? "Staff";
        return MapToStaffDto(user, userRole);
    }

    /// <inheritdoc />
    public async Task<StaffUserDto> UpdateStaffStatusAsync(
        string staffId,
        UpdateStaffStatusRequestDto request,
        CancellationToken ct = default)
    {
        var orgId = await GetScopeOrganizationIdAsync(ct);

        var query = _dbContext.Users.AsQueryable();
        if (orgId.HasValue)
        {
            query = query.Where(u => u.OrganizationId == orgId.Value);
        }

        var user = await query.FirstOrDefaultAsync(u => u.Id == staffId, ct);
        if (user is null)
        {
            throw new KeyNotFoundException("Staff member not found in your organization.");
        }

        // Prevent manager from disabling their own account
        if (string.Equals(user.Id, _currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("You cannot alter the status of your own administrator account.");
        }

        user.AccountStatus = request.Status;
        user.IsActive      = request.Status == UserAccountStatus.Active;
        user.UpdatedAt     = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userRole = roles?.FirstOrDefault() ?? "Staff";
        return MapToStaffDto(user, userRole);
    }

    /// <inheritdoc />
    public async Task<StaffUserDto> VerifyStaffMemberAsync(
        string staffId,
        CancellationToken ct = default)
    {
        var orgId = await GetScopeOrganizationIdAsync(ct);

        var query = _dbContext.Users.AsQueryable();
        if (orgId.HasValue)
        {
            query = query.Where(u => u.OrganizationId == orgId.Value);
        }

        var user = await query.FirstOrDefaultAsync(u => u.Id == staffId, ct);
        if (user is null)
        {
            throw new KeyNotFoundException("Staff member not found in your organization.");
        }

        user.AccountStatus = UserAccountStatus.Active;
        user.IsActive      = true;
        user.UpdatedAt     = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userRole = roles?.FirstOrDefault() ?? "Staff";
        return MapToStaffDto(user, userRole);
    }

    /// <inheritdoc />
    public async Task<StaffUserDto> ResetStaffPasswordAsync(
        string staffId,
        ResetStaffPasswordRequestDto request,
        CancellationToken ct = default)
    {
        var orgId = await GetScopeOrganizationIdAsync(ct);

        var query = _dbContext.Users.AsQueryable();
        if (orgId.HasValue)
        {
            query = query.Where(u => u.OrganizationId == orgId.Value);
        }

        var user = await query.FirstOrDefaultAsync(u => u.Id == staffId, ct);
        if (user is null)
        {
            throw new KeyNotFoundException("Staff member not found in your organization.");
        }

        if (string.Equals(user.Id, _currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Use the account settings page to change your own password.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.TemporaryPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        user.MustChangePassword = true;
        user.UpdatedAt          = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var userRole = roles?.FirstOrDefault() ?? "Staff";
        return MapToStaffDto(user, userRole);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper methods
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Guid?> GetScopeOrganizationIdAsync(CancellationToken ct = default)
    {
        if (string.Equals(_currentUser.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return null; // SuperAdmin oversees all organizations
        }

        if (!_currentUser.OrganizationId.HasValue)
        {
            throw new UnauthorizedAccessException("Access denied: You must be affiliated with an organization to manage staff.");
        }

        var orgId = _currentUser.OrganizationId.Value;
        var org = await _dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orgId, ct);

        if (org is null || org.Status != OrganizationStatus.Active || !org.IsActive)
        {
            throw new UnauthorizedAccessException("Staff management is only available for active, approved veterinary organizations.");
        }

        return orgId;
    }

    private static StaffUserDto MapToStaffDto(ApplicationUser user, string role) =>
        new(
            Id:                 user.Id,
            FirstName:          user.FirstName,
            LastName:           user.LastName,
            FullName:           user.FullName,
            Email:              user.Email ?? string.Empty,
            PhoneNumber:        user.PhoneNumber,
            Role:               role,
            Status:             user.AccountStatus.ToString(),
            OrganizationId:     user.OrganizationId ?? Guid.Empty,
            CreatedAt:          user.CreatedAt,
            LastLoginAt:        user.LastLoginAt,
            MustChangePassword: user.MustChangePassword
        );
}
