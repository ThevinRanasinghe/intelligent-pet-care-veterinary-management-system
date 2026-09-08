namespace PetCare.Domain.Entities;

public class VaccinationRecord
{
    public string Id { get; set; } = string.Empty; // e.g. VAC-4001
    public string PetId { get; set; } = string.Empty;
    public string VaccineName { get; set; } = string.Empty;
    public DateTime DateAdministered { get; set; }
    public DateTime? NextDueDate { get; set; }
    public string? VeterinarianName { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Pet? Pet { get; set; }
}
