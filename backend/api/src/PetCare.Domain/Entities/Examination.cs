namespace PetCare.Domain.Entities;

public class Examination
{
    public Guid Id { get; set; }

    // FK to canonical Merge_1 Pet entity (string Id)
    public string PetId { get; set; } = string.Empty;
    public Pet? Pet { get; set; }

    public Guid VeterinarianId { get; set; }

    // FK to canonical Merge_1 ConsultationRequest entity (string Id, optional)
    public string? ConsultationRequestId { get; set; }
    public ConsultationRequest? ConsultationRequest { get; set; }

    public string Symptoms { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime ExaminationDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Diagnosis? Diagnosis { get; set; }
}
