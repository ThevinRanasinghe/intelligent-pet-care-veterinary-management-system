namespace PetCare.Application.Exceptions;

/// <summary>
/// Thrown when a scheduling business rule is violated (inactive veterinarian,
/// slot mismatch, appointment doesn't fit in slot, overlapping appointment,
/// ...). The API layer should map this to HTTP 409 Conflict.
/// </summary>
public class SchedulingConflictException : Exception
{
    public SchedulingConflictException(string message) : base(message)
    {
    }
}
