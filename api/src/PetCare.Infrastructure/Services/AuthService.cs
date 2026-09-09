using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Entities;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Infrastructure.Services;

/// <summary>
/// Implements authentication operations using ASP.NET Core Identity and PetCareDbContext.
/// Manages multi-tenant organization onboarding, role assignment, and organization validation.
/// All password hashing and verification is delegated to Identity — no manual hashing here.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService               _tokenService;
    private readonly PetCareDbContext            _dbContext;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        PetCareDbContext dbContext)
    {
        _userManager  = userManager;
        _tokenService = tokenService;
        _dbContext    = dbContext;
    }

    /// <inheritdoc />
    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        // Generic error — do not reveal whether the email exists
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("Invalid email or password.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
            throw new UnauthorizedAccessException("Invalid email or password.");

        var roles = await _userManager.GetRolesAsync(user);
        var role  = roles.FirstOrDefault() ?? Roles.PetOwner;

        OrganizationDto? organizationDto = null;

        // Verify organization status for organization-affiliated staff
        if (role is Roles.ClinicManager or Roles.Veterinarian or Roles.InventoryOfficer)
        {
            if (!user.OrganizationId.HasValue)
                throw new UnauthorizedAccessException("Account is not affiliated with any veterinary organization.");

            var organization = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == user.OrganizationId.Value, ct);

            if (organization is null || !organization.IsActive || organization.Status == OrganizationStatus.Inactive)
                throw new UnauthorizedAccessException("Your veterinary organization account is inactive.");

            if (organization.Status == OrganizationStatus.Suspended)
                throw new UnauthorizedAccessException("Your veterinary organization has been suspended. Please contact platform support.");

            if (organization.Status == OrganizationStatus.Pending)
                throw new UnauthorizedAccessException("Your veterinary organization registration is pending approval.");

            organizationDto = MapToOrganizationDto(organization);
        }

        var (token, expiresAt) = _tokenService.GenerateToken(
            user.Id, user.Email!, role, user.FirstName, user.LastName, user.OrganizationId);

        return new LoginResponseDto(
            AccessToken: token,
            ExpiresAt:   expiresAt,
            User:        MapToCurrentUserDto(user, role, organizationDto)
        );
    }

    /// <inheritdoc />
    public async Task<CurrentUserDto> RegisterPetOwnerAsync(
        RegisterPetOwnerRequestDto request, CancellationToken ct = default)
    {
        // Check for duplicate email before attempting to create
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
            throw new InvalidOperationException("An account with this email already exists.");

        var user = new ApplicationUser
        {
            UserName  = request.Email,
            Email     = request.Email,
            FirstName = request.FirstName.Trim(),
            LastName  = request.LastName.Trim(),
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Identity hashes the password — never stored as plain text
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        // Role is ALWAYS PetOwner for public registration — never user-supplied
        var roleResult = await _userManager.AddToRoleAsync(user, Roles.PetOwner);
        if (!roleResult.Succeeded)
        {
            // Rollback the user creation to keep state consistent
            await _userManager.DeleteAsync(user);
            throw new InvalidOperationException("Failed to assign role. Registration rolled back.");
        }

        return MapToCurrentUserDto(user, Roles.PetOwner);
    }

    /// <inheritdoc />
    public async Task<CurrentUserDto> RegisterOrganizationAsync(
        RegisterOrganizationRequestDto request, CancellationToken ct = default)
    {
        // 1. Verify manager email uniqueness
        var existingManager = await _userManager.FindByEmailAsync(request.ManagerEmail);
        if (existingManager is not null)
            throw new InvalidOperationException("An account with this manager email already exists.");

        // 2. Verify organization email / name uniqueness
        var existingOrg = await _dbContext.Organizations
            .AnyAsync(o => o.Email.ToLower() == request.OrganizationEmail.ToLower()
                        || o.Name.ToLower() == request.OrganizationName.ToLower(), ct);

        if (existingOrg)
            throw new InvalidOperationException("An organization with this name or email already exists.");

        // 3. Transactional creation of Organization and initial ClinicManager
        IDbContextTransaction? transaction = null;
        if (_dbContext.Database.IsRelational())
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        try
        {
            var organization = new Organization
            {
                Id                 = Guid.NewGuid(),
                Name               = request.OrganizationName.Trim(),
                RegistrationNumber = string.IsNullOrWhiteSpace(request.RegistrationNumber) ? null : request.RegistrationNumber.Trim(),
                Email              = request.OrganizationEmail.Trim(),
                Phone              = request.OrganizationPhone.Trim(),
                Address            = request.Address.Trim(),
                City               = request.City.Trim(),
                Country            = request.Country.Trim(),
                Status             = OrganizationStatus.Active,
                IsActive           = true,
                CreatedAt          = DateTime.UtcNow,
                UpdatedAt          = DateTime.UtcNow
            };

            _dbContext.Organizations.Add(organization);
            await _dbContext.SaveChangesAsync(ct);

            var manager = new ApplicationUser
            {
                UserName       = request.ManagerEmail.Trim(),
                Email          = request.ManagerEmail.Trim(),
                FirstName      = request.ManagerFirstName.Trim(),
                LastName       = request.ManagerLastName.Trim(),
                OrganizationId = organization.Id,
                IsActive       = true,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow
            };

            var userResult = await _userManager.CreateAsync(manager, request.Password);
            if (!userResult.Succeeded)
            {
                if (transaction is not null) await transaction.RollbackAsync(ct);
                var errors = string.Join("; ", userResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException(errors);
            }

            var roleResult = await _userManager.AddToRoleAsync(manager, Roles.ClinicManager);
            if (!roleResult.Succeeded)
            {
                if (transaction is not null) await transaction.RollbackAsync(ct);
                await _userManager.DeleteAsync(manager);
                throw new InvalidOperationException("Failed to assign ClinicManager role. Registration rolled back.");
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            var orgDto = MapToOrganizationDto(organization);
            return MapToCurrentUserDto(manager, Roles.ClinicManager, orgDto);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task<CurrentUserDto?> GetCurrentUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive) return null;

        var roles = await _userManager.GetRolesAsync(user);
        var role  = roles.FirstOrDefault() ?? Roles.PetOwner;

        OrganizationDto? orgDto = null;
        if (user.OrganizationId.HasValue)
        {
            var org = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == user.OrganizationId.Value, ct);

            if (org is not null)
            {
                orgDto = MapToOrganizationDto(org);
            }
        }

        return MapToCurrentUserDto(user, role, orgDto);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static CurrentUserDto MapToCurrentUserDto(
        ApplicationUser user,
        string role,
        OrganizationDto? organization = null) =>
        new(
            Id:           user.Id,
            FirstName:    user.FirstName,
            LastName:     user.LastName,
            Email:        user.Email!,
            Role:         role,
            Organization: organization,
            FullName:     user.FullName
        );

    private static OrganizationDto MapToOrganizationDto(Organization org) =>
        new(
            Id:     org.Id,
            Name:   org.Name,
            Email:  org.Email,
            Phone:  org.Phone,
            Status: org.Status.ToString()
        );
}
