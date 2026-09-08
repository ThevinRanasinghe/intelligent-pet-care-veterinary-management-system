namespace PetCare.Application.DTOs.Auth;

/// <summary>
/// Public PetOwner registration request.
/// Note: No 'Role' field — role is always forced to PetOwner server-side.
/// </summary>
public sealed record RegisterPetOwnerRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ConfirmPassword
);
