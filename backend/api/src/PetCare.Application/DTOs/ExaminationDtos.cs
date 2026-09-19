using System.ComponentModel.DataAnnotations;

namespace PetCare.Application.DTOs;

// Client eken API ekata evana data (create karadi)
public class CreateExaminationDto
{
    [Required]
    public string PetId { get; set; } = string.Empty;

    [Required]
    public Guid VeterinarianId { get; set; }

    public string? ConsultationRequestId { get; set; }

    [Required]
    [StringLength(1000)]
    public string Symptoms { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Notes { get; set; } = string.Empty;

    [Required]
    public DateTime ExaminationDate { get; set; }
}

// API eken client ekata yawana data (response ekak)
public class ExaminationResponseDto
{
    public Guid Id { get; set; }
    public string PetId { get; set; } = string.Empty;
    public Guid VeterinarianId { get; set; }
    public string? ConsultationRequestId { get; set; }
    public string Symptoms { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime ExaminationDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Update karadi
public class UpdateExaminationDto
{
    [Required]
    [StringLength(1000)]
    public string Symptoms { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Notes { get; set; } = string.Empty;

    [Required]
    public DateTime ExaminationDate { get; set; }
}
