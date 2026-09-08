namespace PetCare.Application.DTOs.Auth;

/// <summary>
/// Safe public representation of the authenticated user.
/// Never includes PasswordHash, SecurityStamp, ConcurrencyStamp,
/// or any other internal Identity field.
/// </summary>
public sealed record CurrentUserDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string Role
);
