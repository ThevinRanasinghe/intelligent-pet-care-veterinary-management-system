using PetCare.Domain.Common;
using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

/// <summary>
/// A booked appointment linking a pet, a veterinarian and the consumed slot.
/// See docs/database/scheduling-billing-approval-domain-model.md#appointment.
/// </summary>
public class Appointment : AuditableEntity
{
    /// <summary>
    /// FK to the Pet entity owned by another module; not modeled in this project.
    /// </summary>
    public Guid PetId { get; set; }

    public Guid VeterinarianId { get; set; }

    public Veterinarian Veterinarian { get; set; } = null!;

    /// <summary>
    /// UNIQUE FK: one confirmed appointment consumes exactly one slot.
    /// </summary>
    public Guid AppointmentSlotId { get; set; }

    public AppointmentSlot AppointmentSlot { get; set; } = null!;

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Reserved;

    public string? Notes { get; set; }

    /// <summary>
    /// 1:1 with Quotation (Quotation.AppointmentId is the UNIQUE FK owner side).
    /// </summary>
    public Quotation? Quotation { get; set; }
}
