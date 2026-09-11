namespace PetCare.Domain.Entities;

/// <summary>
/// AI-generated proposal awaiting Clinic Manager review and approval.
/// Acts as clinical decision support prior to high-impact transactional operations.
/// </summary>
public class AIProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Organization? Organization { get; set; }

    public string? ConsultationRequestId { get; set; }

    public ConsultationRequest? ConsultationRequest { get; set; }

    public Guid? AppointmentId { get; set; }

    public Appointment? Appointment { get; set; }

    public Guid? QuotationId { get; set; }

    public Quotation? Quotation { get; set; }

    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, RevisionRequested

    public string Urgency { get; set; } = "Routine"; // Routine, Priority, Urgent

    public string PetName { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string SymptomsSummary { get; set; } = string.Empty;

    public string PreliminaryRecommendation { get; set; } = string.Empty;

    public string ProposedTreatment { get; set; } = string.Empty;

    public string MedicineAvailabilityStatus { get; set; } = "InStock";

    public string ProposedVeterinarianName { get; set; } = string.Empty;

    public string ProposedDate { get; set; } = string.Empty;

    public string ProposedTime { get; set; } = string.Empty;

    public decimal QuotationTotal { get; set; }

    public decimal BudgetLimit { get; set; }

    public string ValidationChecksJson { get; set; } = "[]";

    public string ExecutionStepsJson { get; set; } = "[]";

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedBy { get; set; }

    public string? DecisionNote { get; set; }
}
