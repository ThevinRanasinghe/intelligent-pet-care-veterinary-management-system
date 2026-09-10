using PetCare.Domain.Common;

namespace PetCare.Domain.Entities;

/// <summary>
/// A veterinarian who owns appointment slots and performs appointments for an organization.
/// </summary>
public class Veterinarian : AuditableEntity
{
    public Guid? OrganizationId { get; set; }

    public Organization? Organization { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Specialisation { get; set; } = string.Empty;

    public string Branch { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public ICollection<AppointmentSlot> AppointmentSlots { get; set; } = new List<AppointmentSlot>();

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
