namespace PetCare.Domain.Entities;

public class MedicalRecord
{
    public string Id { get; set; } = string.Empty; // e.g. MED-3001
    public string PetId { get; set; } = string.Empty;
    public DateTime RecordDate { get; set; } = DateTime.UtcNow;
    public string Diagnosis { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public string? VeterinarianName { get; set; }
    public string? ClinicalNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Pet? Pet { get; set; }
}
