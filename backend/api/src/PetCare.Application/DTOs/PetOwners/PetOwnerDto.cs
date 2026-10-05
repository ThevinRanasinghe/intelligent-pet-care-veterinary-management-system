namespace PetCare.Application.DTOs.PetOwners;

public class PetOwnerDto
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    /// <summary>Id of the linked user account, when the profile has been claimed.</summary>
    public Guid? UserId { get; set; }
}