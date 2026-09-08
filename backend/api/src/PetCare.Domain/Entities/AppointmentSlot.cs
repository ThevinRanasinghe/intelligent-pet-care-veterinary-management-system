using PetCare.Domain.Common;
using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

/// <summary>
/// A bookable time slot owned by a veterinarian.
/// See docs/database/scheduling-billing-approval-domain-model.md#appointmentslot.
/// UNIQUE (VeterinarianId, Date, StartTime) is enforced via EF Core configuration.
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

    /// <summary>
    /// Set when this slot is consumed by an appointment (1:1).
    /// </summary>
    public Appointment? Appointment { get; set; }
}
