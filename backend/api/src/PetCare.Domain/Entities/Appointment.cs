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
    /// FK to the canonical Pet entity (Pets.Id is a string identifier such as "PET-...").
    /// </summary>
    public string PetId { get; set; } = string.Empty;

    public Pet Pet { get; set; } = null!;

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
    /// FK to the ConsultationRequest this appointment was scheduled from
    /// (string id, optional — walk-in appointments have no request).
    /// </summary>
    public string? ConsultationRequestId { get; set; }

    public ConsultationRequest? ConsultationRequest { get; set; }

    /// <summary>Initial visit or veterinarian-requested follow-up.</summary>
    public AppointmentType Type { get; set; } = AppointmentType.Initial;

    /// <summary>
    /// 1:1 with Quotation (Quotation.AppointmentId is the UNIQUE FK owner side).
    /// </summary>
    public Quotation? Quotation { get; set; }

    /// <summary>
    /// The examination recorded when this appointment was completed
    /// (at most one per appointment; Examination.AppointmentId is UNIQUE).
    /// </summary>
    public Examination? Examination { get; set; }
}
