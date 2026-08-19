namespace PetCare.Application.DTOs;

public class CreatePetDto
{
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? MedicalHistorySummary { get; set; }
}

public class PetResponseDto
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? MedicalHistorySummary { get; set; }
    public DateTime CreatedAt { get; set; }
}