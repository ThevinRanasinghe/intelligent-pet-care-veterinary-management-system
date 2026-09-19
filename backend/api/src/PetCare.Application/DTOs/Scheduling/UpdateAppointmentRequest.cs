namespace PetCare.Application.DTOs.Scheduling;

public class UpdateAppointmentRequest
{
    public DateTime ScheduledStart { get; set; }

    public DateTime ScheduledEnd { get; set; }

    public string? Notes { get; set; }
}
