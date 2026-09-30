using System.ComponentModel.DataAnnotations;

namespace PetCare.Application.DTOs.Consultations;

/// <summary>
/// ClinicManager/Admin request to assign a veterinarian to a consultation
/// request: books the given window as a Confirmed appointment.
/// </summary>
public class AssignVeterinarianRequest
{
    [Required]
    public Guid VeterinarianId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    /// <summary>Hour-aligned slot start within operating hours (09:00–17:00).</summary>
    [Required]
    public TimeOnly StartTime { get; set; }

    /// <summary>
    /// Optional — when supplied it must equal StartTime + 1 hour; otherwise
    /// the server computes it. Clients should omit it.
    /// </summary>
    public TimeOnly? EndTime { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
