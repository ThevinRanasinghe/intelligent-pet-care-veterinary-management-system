namespace PetCare.Domain.Enums;

/// <summary>
/// Status of an <see cref="Entities.Approval"/>.
/// Mirrors the frontend ApprovalStatus type (frontend/web/src/types/domain.ts).
/// </summary>
public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    RevisionRequested
}
