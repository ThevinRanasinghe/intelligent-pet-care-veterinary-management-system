using System.ComponentModel.DataAnnotations;

namespace PetCare.Application.DTOs;

public class CreatePrescriptionDto
{
    [Required]
    public Guid TreatmentRecordId { get; set; }
    
    [Required]
    public Guid MedicineId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Dosage { get; set; } = string.Empty;
    
    [Required]
    [Range(1, 365)]
    public int DurationDays { get; set; }
}

public class PrescriptionResponseDto
{
    public Guid Id { get; set; }
    public Guid TreatmentRecordId { get; set; }
    public Guid MedicineId { get; set; }
    public string Dosage { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public DateTime CreatedAt { get; set; }
}
