namespace PetCare.Domain.Entities;

public class PetOwner
{
    public string Id { get; set; } = string.Empty; // e.g. OWN-2001
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Pet> Pets { get; set; } = new List<Pet>();
    public ICollection<ConsultationRequest> ConsultationRequests { get; set; } = new List<ConsultationRequest>();
}
