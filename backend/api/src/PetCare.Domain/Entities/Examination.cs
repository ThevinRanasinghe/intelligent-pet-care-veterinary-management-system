namespace PetCare.Domain.Entities;

public class Examination
{
    public Guid Id { get; set; }

    // FK to canonical Merge_1 Pet entity (string Id)
    public string PetId { get; set; } = string.Empty;
    public Pet? Pet { get; set; }

    // FK to the Veterinarian entity; carries the organization scope of the
    // examination transitively (Veterinarian.OrganizationId).
    public Guid VeterinarianId { get; set; }
    public Veterinarian? Veterinarian { get; set; }

    // FK to canonical Merge_1 ConsultationRequest entity (string Id, optional)
    public string? ConsultationRequestId { get; set; }
    public ConsultationRequest? ConsultationRequest { get; set; }

    // FK to the appointment this examination completed (optional, UNIQUE —
    // an appointment has at most one examination).
    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    /// <summary>The veterinarian's fee for this examination, billed onto the
    /// appointment's quotation. Zero for unbilled/legacy examinations.</summary>
    public decimal VeterinarianCharge { get; set; }

    public string Symptoms { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime ExaminationDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Diagnosis? Diagnosis { get; set; }
}
