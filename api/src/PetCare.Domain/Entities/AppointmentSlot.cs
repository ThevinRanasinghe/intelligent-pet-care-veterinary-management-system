using PetCare.Domain.Common;
using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

/// <summary>
/// A bookable time slot owned by a veterinarian.
/// </summary>
public class AppointmentSlot : AuditableEntity
{
    public Guid VeterinarianId { get; set; }

    public Veterinarian Veterinarian { get; set; } = null!;

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Branch { get; set; } = string.Empty;

    public AppointmentSlotStatus Status { get; set; } = AppointmentSlotStatus.Available;

    public Appointment? Appointment { get; set; }
}
