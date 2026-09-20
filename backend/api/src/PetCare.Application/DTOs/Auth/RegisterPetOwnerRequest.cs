namespace PetCare.Application.DTOs.Auth;

/// <summary>
/// Public PetOwner registration request. Role is always forced to PetOwner
/// server-side — never user-supplied.
/// </summary>
public sealed class RegisterPetOwnerRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}
