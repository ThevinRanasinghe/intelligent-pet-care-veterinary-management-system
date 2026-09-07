namespace PetCare.Application.Exceptions;

/// <summary>
/// Thrown when an approval/review business rule is violated (reviewing a
/// quotation that isn't PendingApproval, re-reviewing an already-decided
/// approval, an invalid status transition, ...). The API layer should map
/// this to HTTP 409 Conflict, mirroring SchedulingConflictException and
/// BillingConflictException.
/// </summary>
public class ApprovalConflictException : Exception
{
    public ApprovalConflictException(string message) : base(message)
    {
    }
}
