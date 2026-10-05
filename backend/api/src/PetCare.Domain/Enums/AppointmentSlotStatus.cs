namespace PetCare.Domain.Enums;

/// <summary>
/// Status of an <see cref="Entities.AppointmentSlot"/>.
/// Mirrors the frontend AppointmentStatus type (frontend/web/src/types/domain.ts);
/// slots and appointments share the same status vocabulary.
/// </summary>
public enum AppointmentSlotStatus
{
    Available,
    Reserved,
    Confirmed,
    Completed,
    Cancelled
}
