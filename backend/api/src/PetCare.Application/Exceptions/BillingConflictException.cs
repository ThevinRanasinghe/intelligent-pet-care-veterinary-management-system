namespace PetCare.Application.Exceptions;

/// <summary>
/// Thrown when a billing/quotation business rule is violated (quotation
/// already exists for the appointment, invalid status transition, editing a
/// locked/approved quotation, submitting over budget, ...). The API layer
/// should map this to HTTP 409 Conflict, mirroring SchedulingConflictException.
/// </summary>
public class BillingConflictException : Exception
{
    public BillingConflictException(string message) : base(message)
    {
    }
}
