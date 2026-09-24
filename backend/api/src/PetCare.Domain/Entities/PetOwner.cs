namespace PetCare.Domain.Entities;

public class PetOwner
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    /// <summary>
    /// FK to the authentication account that owns this profile. Null for
    /// owner records created by staff before the owner registers — the link
    /// is established at registration time (or by data migration backfill).
    /// </summary>
    public Guid? UserId { get; set; }

    public User? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Pet> Pets { get; set; } = new List<Pet>();

    public ICollection<ConsultationRequest> ConsultationRequests { get; set; }
        = new List<ConsultationRequest>();
}