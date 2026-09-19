namespace PetCare.Application.DTOs.PetOwners;

public class CreatePetOwnerDto
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Address { get; set; }
}