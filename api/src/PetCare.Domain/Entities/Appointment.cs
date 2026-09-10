using PetCare.Domain.Common;
using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

/// <summary>
/// A booked appointment linking a pet, veterinarian and consumed slot.
/// </summary>
public class Appointment : AuditableEntity
{
    public Guid? OrganizationId { get; set; }

    public Organization? Organization { get; set; }

    public Guid PetId { get; set; }

    public Guid VeterinarianId { get; set; }

    public Veterinarian Veterinarian { get; set; } = null!;

    public Guid AppointmentSlotId { get; set; }

    public AppointmentSlot AppointmentSlot { get; set; } = null!;

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Reserved;

    public string? Notes { get; set; }

    public Quotation? Quotation { get; set; }
}
