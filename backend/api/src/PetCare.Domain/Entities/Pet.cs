namespace PetCare.Domain.Entities;

public class Pet
{
    public string Id { get; set; } = string.Empty; // e.g., PET-1001
    public string OwnerId { get; set; } = string.Empty; // e.g., OWN-2001
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty; // e.g., Dog, Cat, Bird
    public string Breed { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public int Age { get; set; }
    public string? Notes { get; set; }
    public string? PhotoUrl { get; set; }

    // Audit fields (Mandatory as per assignment guidelines)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public PetOwner? Owner { get; set; }
    public ICollection<ConsultationRequest> ConsultationRequests { get; set; } = new List<ConsultationRequest>();
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    public ICollection<VaccinationRecord> VaccinationRecords { get; set; } = new List<VaccinationRecord>();
}