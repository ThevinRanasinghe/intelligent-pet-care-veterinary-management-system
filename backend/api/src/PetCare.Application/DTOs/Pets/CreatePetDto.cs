namespace PetCare.Application.DTOs.Pets;

public class CreatePetDto
{
    public string OwnerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Species { get; set; } = string.Empty;

    public string? Breed { get; set; }

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public decimal? Weight { get; set; }

    public string? PhotoUrl { get; set; }

    public string? Notes { get; set; }
}