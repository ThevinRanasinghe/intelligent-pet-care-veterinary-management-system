namespace PetCare.Domain.Entities;

public class Examination
{
    public Guid Id { get; set; }
    public Guid PetId { get; set; }
    public Pet? Pet { get; set; }

    public Guid VeterinarianId { get; set; }

    public Guid? ConsultationRequestId { get; set; }
    public ConsultationRequest? ConsultationRequest { get; set; }

    public string Symptoms { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime ExaminationDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Diagnosis? Diagnosis { get; set; }
}