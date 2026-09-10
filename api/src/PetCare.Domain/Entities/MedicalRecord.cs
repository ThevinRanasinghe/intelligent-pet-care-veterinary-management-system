namespace PetCare.Domain.Entities;

/// <summary>
/// A clinical medical record for a pet.
/// </summary>
public class MedicalRecord
{
    public string Id { get; set; } = string.Empty;

    public string PetId { get; set; } = string.Empty;

    public Pet? Pet { get; set; }

    public DateTime RecordDate { get; set; }

    public string Diagnosis { get; set; } = string.Empty;

    public string Treatment { get; set; } = string.Empty;

    public string? VeterinarianName { get; set; }

    public string? ClinicalNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
