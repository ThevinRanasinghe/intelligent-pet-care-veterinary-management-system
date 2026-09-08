using Microsoft.AspNetCore.Identity;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Infrastructure.Entities;

namespace PetCare.Infrastructure.Services;

/// <summary>
/// Implements authentication operations using ASP.NET Core Identity.
/// All password hashing and verification is delegated to Identity — no manual hashing here.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService               _tokenService;

    public AuthService(UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _userManager  = userManager;
        _tokenService = tokenService;
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

        var (token, expiresAt) = _tokenService.GenerateToken(
            user.Id, user.Email!, role, user.FirstName, user.LastName);

        return new LoginResponseDto(
            AccessToken: token,
            ExpiresAt:   expiresAt,
            User:        MapToCurrentUserDto(user, role)
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
    public async Task<CurrentUserDto?> GetCurrentUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive) return null;

        var roles = await _userManager.GetRolesAsync(user);
        var role  = roles.FirstOrDefault() ?? Roles.PetOwner;

        return MapToCurrentUserDto(user, role);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static CurrentUserDto MapToCurrentUserDto(ApplicationUser user, string role) =>
        new(
            Id:        user.Id,
            FirstName: user.FirstName,
            LastName:  user.LastName,
            Email:     user.Email!,
            Role:      role
        );
}
