using System.ComponentModel.DataAnnotations;

namespace PetCare.Application.DTOs;

// Client eken API ekata evana data (create karadi)
public class CreateExaminationDto
{
    [Required]
    public string PetId { get; set; } = string.Empty;

    /// <summary>Ignored when the caller is a Veterinarian — the examination
    /// is always recorded under their own veterinarian profile.</summary>
    [Required]
    public Guid VeterinarianId { get; set; }

    public string? ConsultationRequestId { get; set; }

    /// <summary>
    /// The appointment this examination completes. When set, PetId and
    /// ConsultationRequestId are taken from the appointment and the
    /// appointment (and its slot) is marked Completed.
    /// </summary>
    public Guid? AppointmentId { get; set; }

    /// <summary>The veterinarian's fee for this examination (billed onto the
    /// appointment's quotation when the appointment flow is used).</summary>
    [Range(0, 1000000)]
    public decimal VeterinarianCharge { get; set; }

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
    public Guid? AppointmentId { get; set; }
    public decimal VeterinarianCharge { get; set; }
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

    [Range(0, 1000000)]
    public decimal VeterinarianCharge { get; set; }
}
