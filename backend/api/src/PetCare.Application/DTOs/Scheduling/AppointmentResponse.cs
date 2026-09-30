namespace PetCare.Application.DTOs.Scheduling;

public class AppointmentResponse
{
    public Guid Id { get; set; }

    public string PetId { get; set; } = string.Empty;

    public Guid VeterinarianId { get; set; }

    public Guid AppointmentSlotId { get; set; }

    public DateTime ScheduledStart { get; set; }

    public DateTime ScheduledEnd { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    /// <summary>The consultation request this appointment was scheduled from, if any.</summary>
    public string? ConsultationRequestId { get; set; }

    /// <summary>"Initial" | "FollowUp".</summary>
    public string Type { get; set; } = string.Empty;

    // Denormalised display fields (populated via repository includes).
    public string? PetName { get; set; }

    public string? OwnerName { get; set; }

    public string? VeterinarianName { get; set; }

    /// <summary>Symptoms from the linked consultation request, if any.</summary>
    public string? Symptoms { get; set; }

    /// <summary>The examination recorded for this appointment, if completed.</summary>
    public Guid? ExaminationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
