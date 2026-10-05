namespace PetCare.Domain.Entities;

public enum TreatmentStatus { Planned, InProgress, Completed, Cancelled }

public class TreatmentRecord
{
    public Guid Id { get; set; }
    public Guid DiagnosisId { get; set; }
    public Diagnosis? Diagnosis { get; set; }

    public string ProcedureName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public TreatmentStatus Status { get; set; } = TreatmentStatus.Planned;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Prescription> Prescriptions { get; set; } = new();
}