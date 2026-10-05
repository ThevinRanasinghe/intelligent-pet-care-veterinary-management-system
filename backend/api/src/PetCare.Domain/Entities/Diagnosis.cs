namespace PetCare.Domain.Entities;

public enum DiagnosisSeverity { Low, Moderate, High, Critical }

public class Diagnosis
{
    public Guid Id { get; set; }
    public Guid ExaminationId { get; set; }
    public Examination? Examination { get; set; }

    public string ConditionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DiagnosisSeverity Severity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TreatmentRecord> TreatmentRecords { get; set; } = new();
}