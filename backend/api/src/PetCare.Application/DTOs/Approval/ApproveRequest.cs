namespace PetCare.Application.DTOs.Approval;

/// <summary>
/// ReviewedBy is accepted as a client-supplied value for now: no JWT/role
/// authorization exists yet in this layer (added later by the API security
/// layer), so the acting Clinic Manager's id must be passed explicitly.
/// </summary>
public class ApproveRequest
{
    public Guid ReviewedBy { get; set; }

    /// <summary>Optional note; not required for an approval decision.</summary>
    public string? Comment { get; set; }
}
