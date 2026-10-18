namespace PetCare.Application.DTOs.Scheduling;

public class UpdateAppointmentRequest
{
    public DateTime ScheduledStart { get; set; }

    public DateTime ScheduledEnd { get; set; }

    /// <summary>
    /// Patch semantics: omit (null) to leave the existing note untouched;
    /// send an empty string to clear it; send a value to replace it.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Reassign the appointment to a different veterinarian. The previous
    /// slot is released and a slot for the new veterinarian/date/time is
    /// reserved — same rules as the consultation assign path.
    /// </summary>
    public Guid? VeterinarianId { get; set; }

    /// <summary>
    /// Move the appointment onto a specific published slot. Must belong to
    /// the (target) veterinarian, be on the requested date and start at the
    /// requested start time, and still be Available. Optional — when omitted
    /// the service adopts or materializes the matching slot.
    /// </summary>
    public Guid? AppointmentSlotId { get; set; }
}
