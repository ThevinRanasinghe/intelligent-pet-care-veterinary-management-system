namespace PetCare.Application.DTOs;

public class CreateConsultationRequestDto
{
    public Guid PetId { get; set; }
    public Guid OwnerId { get; set; }
    public string SymptomsDescription { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string PreferredBranch { get; set; } = string.Empty;
    public DateTime PreferredDate { get; set; }
    public decimal BudgetLimit { get; set; }
}

public class ConsultationResponseDto
{
    public Guid Id { get; set; }
    public Guid PetId { get; set; }
    public Guid OwnerId { get; set; }
    public string SymptomsDescription { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string PreferredBranch { get; set; } = string.Empty;
    public DateTime PreferredDate { get; set; }
    public decimal BudgetLimit { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}