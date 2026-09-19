namespace PetCare.Application.DTOs.Scheduling;

public class ConflictCheckRequest
{
    public Guid VeterinarianId { get; set; }

    public DateTime ScheduledStart { get; set; }

    public DateTime ScheduledEnd { get; set; }

    /// <summary>
    /// Optional; set when checking conflicts for an update so the
    /// appointment being edited is excluded from the overlap check
    /// against itself.
    /// </summary>
    public Guid? AppointmentId { get; set; }
}
