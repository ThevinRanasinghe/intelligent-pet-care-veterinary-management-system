namespace PetCare.Application.DTOs.Scheduling;

public class CreateAppointmentRequest
{
    public Guid PetId { get; set; }

    public Guid VeterinarianId { get; set; }

    public Guid AppointmentSlotId { get; set; }

    public DateTime ScheduledStart { get; set; }

    public DateTime ScheduledEnd { get; set; }

    public string? Notes { get; set; }
}
