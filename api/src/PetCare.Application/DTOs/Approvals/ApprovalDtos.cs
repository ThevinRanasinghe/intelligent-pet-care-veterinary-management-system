namespace PetCare.Application.DTOs.Approvals;

public record ValidationCheckItemDto(
    string Key,
    string Label,
    bool Passed,
    string Detail
);

public record ExecutionStepItemDto(
    string Id,
    string Name,
    string Responsibility,
    string Status,
    string? StartedAt,
    string? CompletedAt
);

public record AIProposalSummaryDto(
    Guid Id,
    Guid OrganizationId,
    string? ConsultationRequestId,
    Guid? AppointmentId,
    Guid? QuotationId,
    string Status,
    string Urgency,
    string PetName,
    string OwnerName,
    string SymptomsSummary,
    string ProposedVeterinarianName,
    string ProposedDate,
    string ProposedTime,
    decimal QuotationTotal,
    decimal BudgetLimit,
    DateTime SubmittedAt
);

public record AIProposalDetailsDto(
    Guid Id,
    Guid OrganizationId,
    string? ConsultationRequestId,
    Guid? AppointmentId,
    Guid? QuotationId,
    string Status,
    string Urgency,
    string PetName,
    string OwnerName,
    string SymptomsSummary,
    string PreliminaryRecommendation,
    string ProposedTreatment,
    string MedicineAvailabilityStatus,
    string ProposedVeterinarianName,
    string ProposedDate,
    string ProposedTime,
    decimal QuotationTotal,
    decimal BudgetLimit,
    IReadOnlyList<ValidationCheckItemDto> ValidationChecks,
    IReadOnlyList<ExecutionStepItemDto> ExecutionSteps,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewedBy,
    string? DecisionNote
);

public record ApproveProposalDto(
    string? Comment
);

public record RejectProposalDto(
    string Reason
);

public record RequestRevisionDto(
    string Reason
);

public record ApprovalHistoryDto(
    Guid Id,
    Guid ApprovalId,
    string PreviousStatus,
    string NewStatus,
    Guid ChangedBy,
    string? Reason,
    DateTimeOffset ChangedAt
);
