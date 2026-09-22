namespace PetCare.Application.DTOs.Auth;

/// <summary>Editable account details for the currently authenticated user.</summary>
public sealed class UpdateProfileRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
}
