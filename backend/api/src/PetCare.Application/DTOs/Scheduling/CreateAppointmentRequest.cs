namespace PetCare.Application.DTOs.Scheduling;

public class CreateAppointmentRequest
{
    public string PetId { get; set; } = string.Empty;

    public Guid VeterinarianId { get; set; }

    public Guid AppointmentSlotId { get; set; }

    public DateTime ScheduledStart { get; set; }

    public DateTime ScheduledEnd { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Required consultation link. Every appointment is driven by a
    /// consultation request (owner-filed Initial or vet-requested
    /// FollowUp) — there is no standalone appointment use case, and
    /// link-less creates previously produced orphaned Reserved rows that
    /// bypassed the workflow. The service validates the request (pet
    /// match, organization, eligible status) and confirms it, producing
    /// the same linked records the consultation assign path produces.
    /// New bookings normally use POST /api/consultations/walk-in or
    /// POST /api/consultations/{id}/assign; this endpoint covers the
    /// explicit-slot + multi-hour variant of the same operation.
    /// </summary>
    public string? ConsultationRequestId { get; set; }
}
