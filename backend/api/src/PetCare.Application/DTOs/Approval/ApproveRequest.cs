namespace PetCare.Application.DTOs.Approval;

/// <summary>
/// ReviewedBy remains in the contract for compatibility, but the reviewer
/// of record is bound server-side to the authenticated user (JWT sub) —
/// the client-supplied value is ignored for authenticated callers.
/// </summary>
public class ApproveRequest
{
    public Guid ReviewedBy { get; set; }

    /// <summary>Optional note; not required for an approval decision.</summary>
    public string? Comment { get; set; }
}
