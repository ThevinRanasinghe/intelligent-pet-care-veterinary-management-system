namespace PetCare.Application.DTOs.Scheduling;

public class AppointmentSlotResponse
{
    public Guid Id { get; set; }

    public Guid VeterinarianId { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Branch { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
