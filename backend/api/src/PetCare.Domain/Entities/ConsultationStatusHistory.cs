namespace PetCare.Domain.Entities;

public class ConsultationStatusHistory
{
    public int Id { get; set; }

    public string ConsultationRequestId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Comments { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public ConsultationRequest ConsultationRequest { get; set; } = null!;
}