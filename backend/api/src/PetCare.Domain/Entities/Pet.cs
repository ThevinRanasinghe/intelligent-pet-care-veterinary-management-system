namespace PetCare.Domain.Entities;

public class Pet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty; // e.g., Dog, Cat
    public string Breed { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? MedicalHistorySummary { get; set; }
    
    // Audit fields (Mandatory as per assignment guidelines)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<ConsultationRequest> ConsultationRequests { get; set; } = new List<ConsultationRequest>();
}