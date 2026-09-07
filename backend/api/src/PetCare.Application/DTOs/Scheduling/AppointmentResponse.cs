namespace PetCare.Application.DTOs.Scheduling;

public class AppointmentResponse
{
    public Guid Id { get; set; }

    public Guid PetId { get; set; }

    public Guid VeterinarianId { get; set; }

    public Guid AppointmentSlotId { get; set; }

    public DateTime ScheduledStart { get; set; }

    public DateTime ScheduledEnd { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
