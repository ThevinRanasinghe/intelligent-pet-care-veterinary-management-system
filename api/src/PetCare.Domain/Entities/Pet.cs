namespace PetCare.Domain.Entities;

/// <summary>
/// A registered pet belonging to a Pet Owner.
/// </summary>
public class Pet
{
    public string Id { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;

    public PetOwner? Owner { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Species { get; set; } = string.Empty;

    public string Breed { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public int Age { get; set; }

    public string? Notes { get; set; }

    public string? PhotoUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
}
