namespace PetCare.Domain.Enums;

public enum AIWorkflowStatus
{
    Processing,
    Validation,
    PendingManagerApproval,
    RevisionRequired,
    Approved,
    Rejected,
    Executing,
    Completed,
    Failed
}
