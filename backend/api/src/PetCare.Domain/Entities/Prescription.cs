namespace PetCare.Domain.Entities;

public class Prescription
{
    public Guid Id { get; set; }
    public Guid TreatmentRecordId { get; set; }
    public TreatmentRecord? TreatmentRecord { get; set; }

    public Guid MedicineId { get; set; }
    public Medicine? Medicine { get; set; }

    public string Dosage { get; set; } = string.Empty;
    public int DurationDays { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}