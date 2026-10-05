using System.ComponentModel.DataAnnotations;

namespace PetCare.Application.DTOs;

public class CreateTreatmentRecordDto
{
    [Required]
    public Guid DiagnosisId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string ProcedureName { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string Notes { get; set; } = string.Empty;
}

public class TreatmentRecordResponseDto
{
    public Guid Id { get; set; }
    public Guid DiagnosisId { get; set; }
    public string ProcedureName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Planned" | "InProgress" | "Completed" | "Cancelled"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateTreatmentRecordDto
{
    [Required]
    [StringLength(200)]
    public string ProcedureName { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string Notes { get; set; } = string.Empty;
}

// business-specific operation: status change only
public class UpdateTreatmentStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;
}
