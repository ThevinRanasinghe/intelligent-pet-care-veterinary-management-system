namespace PetCare.Domain.Entities;

public class Pet
{
    public string Id { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Species { get; set; } = string.Empty;

    public string? Breed { get; set; }

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public decimal? Weight { get; set; }

    public int? Age { get; set; }

    public string? Notes { get; set; }

    public string? PhotoUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public PetOwner Owner { get; set; } = null!;

    // Consultation requests related to this pet
    public ICollection<ConsultationRequest> ConsultationRequests { get; set; }
        = new List<ConsultationRequest>();
}