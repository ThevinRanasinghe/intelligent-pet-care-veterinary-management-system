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
    /// Optional — when supplied it must equal StartTime + (1 hour × the
    /// slot count); otherwise the server computes it. Clients should omit it.
    /// </summary>
    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// AI-workflow path only — the consecutive one-hour AppointmentSlot
    /// rows this booking must consume. When supplied, every listed slot
    /// must exist, still be Available, belong to the veterinarian and
    /// date, and together cover exactly [StartTime, StartTime + N hours).
    /// All slots are reserved atomically; any failure aborts the booking.
    /// Omitted for manual single-slot assignment.
    /// </summary>
    public List<Guid>? SlotIds { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
