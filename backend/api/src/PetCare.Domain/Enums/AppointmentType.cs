namespace PetCare.Domain.Enums;

/// <summary>
/// Whether an appointment is the first visit for a consultation
/// (<see cref="Initial"/>) or a follow-up requested by a veterinarian.
/// Stored as a string in the database.
/// </summary>
public enum AppointmentType
{
    Initial,
    FollowUp
}
