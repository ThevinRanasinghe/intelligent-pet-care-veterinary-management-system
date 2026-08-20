namespace PetCare.Domain.Enums;

/// <summary>
/// Status of an <see cref="Entities.Appointment"/>.
/// Mirrors the frontend AppointmentStatus type (frontend/web/src/types/domain.ts).
/// </summary>
public enum AppointmentStatus
{
    Available,
    Reserved,
    Confirmed,
    Completed,
    Cancelled
}
