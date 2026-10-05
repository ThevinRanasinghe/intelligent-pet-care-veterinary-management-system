namespace PetCare.Application.DTOs.Consultations;

public class ConsultationStatusHistoryDto
{
    public int Id { get; set; }

    public string ConsultationRequestId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Comments { get; set; }

    public DateTime ChangedAt { get; set; }
}