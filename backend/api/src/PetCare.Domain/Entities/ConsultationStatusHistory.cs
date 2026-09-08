using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

public class ConsultationStatusHistory
{
    public int Id { get; set; }
    public string ConsultationRequestId { get; set; } = string.Empty;
    public ConsultationStatus Status { get; set; }
    public string? Comments { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ConsultationRequest? ConsultationRequest { get; set; }
}
