using System.ComponentModel.DataAnnotations;

namespace PetCare.Application.DTOs.Consultations;

/// <summary>
/// Veterinarian-requested follow-up consultation for a pet — the request is
/// filed by the vet (RequestType = FollowUp) rather than by the owner.
/// The organization is resolved from the source examination's appointment
/// (its consultation's OrganizationId), falling back to the requesting
/// veterinarian's own organization.
/// </summary>
public class CreateFollowUpRequest
{
    [Required]
    [StringLength(30)]
    public string PetId { get; set; } = string.Empty;

    /// <summary>The examination that prompted the follow-up (required — it
    /// anchors the consultation to the treating clinic).</summary>
    [Required]
    public Guid ExaminationId { get; set; }

    /// <summary>Requested date for the follow-up (not in the past).</summary>
    [Required]
    public DateTime PreferredDate { get; set; }

    /// <summary>
    /// One-hour slot start (required, hour-aligned, 09:00–17:00). The end
    /// time is always start + 1h — it is never client-entered.
    /// </summary>
    [Required]
    public TimeOnly PreferredTime { get; set; }

    [Required]
    [StringLength(4000)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }
}
