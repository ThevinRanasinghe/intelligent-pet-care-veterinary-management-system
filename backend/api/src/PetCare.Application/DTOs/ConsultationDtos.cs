using PetCare.Domain.Enums;

namespace PetCare.Application.DTOs;

public class CreateConsultationRequestDto
{
    public string? Id { get; set; } // Optional short ID override (e.g., REQ-5001)
    public string PetId { get; set; } = string.Empty; // e.g., PET-1001
    public string OwnerId { get; set; } = string.Empty; // e.g., OWN-2001
    public string SymptomsDescription { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public DateTime PreferredDate { get; set; }
    public decimal BudgetLimit { get; set; }
    
    // Coordinates & Preferred Clinic Location (Lat/Long)
    public double? PreferredClinicLocationLat { get; set; }
    public double? PreferredClinicLocationLong { get; set; }
    public string? PreferredBranch { get; set; }
}

public class ConsultationResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string PetId { get; set; } = string.Empty;
    public string? PetName { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string SymptomsDescription { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public DateTime PreferredDate { get; set; }
    public decimal BudgetLimit { get; set; }
    public double? PreferredClinicLocationLat { get; set; }
    public double? PreferredClinicLocationLong { get; set; }
    public string? PreferredBranch { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? StatusNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ConsultationStatusTrackingDto
{
    public string ConsultationId { get; set; } = string.Empty;
    public string PetId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StatusNotes { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ConsultationHistoryItemDto> History { get; set; } = new();
}

public class UpdateConsultationStatusDto
{
    public ConsultationStatus Status { get; set; }
    public string? Comments { get; set; }
}

public class ConsultationHistoryItemDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime ChangedAt { get; set; }
}
