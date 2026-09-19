using System.ComponentModel.DataAnnotations;

namespace PetCare.Application.DTOs;

public class CreateDiagnosisDto
{
    [Required]
    public Guid ExaminationId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string ConditionName { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string Severity { get; set; } = string.Empty; // "Low" | "Moderate" | "High" | "Critical"
}

public class DiagnosisResponseDto
{
    public Guid Id { get; set; }
    public Guid ExaminationId { get; set; }
    public string ConditionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class UpdateDiagnosisDto
{
    [Required]
    [StringLength(200)]
    public string ConditionName { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string Severity { get; set; } = string.Empty;
}
