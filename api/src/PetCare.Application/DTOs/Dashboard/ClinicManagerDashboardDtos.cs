namespace PetCare.Application.DTOs.Dashboard;

public record StatusCountDto(
    string Status,
    int Count
);

public record RecentActivityDto(
    Guid Id,
    string Action,
    string EntityType,
    string EntityId,
    string Details,
    DateTime Timestamp,
    string UserEmail
);

public record VetWorkloadItemDto(
    Guid VeterinarianId,
    string VeterinarianName,
    string Specialisation,
    int TotalAppointments,
    int CompletedAppointments,
    int UpcomingAppointments,
    int CancelledAppointments
);

public record ClinicDashboardSummaryDto(
    int TodayAppointmentsCount,
    int UpcomingAppointmentsCount,
    int PendingProposalsCount,
    int ActiveVeterinariansCount,
    int TotalStaffCount,
    decimal TotalRevenue,
    IReadOnlyList<StatusCountDto> AppointmentStatusCounts,
    IReadOnlyList<VetWorkloadItemDto> VeterinarianWorkloads,
    IReadOnlyList<RecentActivityDto> RecentActivities
);

public record RevenueCategoryItemDto(
    string Category,
    decimal Amount,
    double Percentage
);

public record RevenuePeriodItemDto(
    string Period,
    decimal Amount,
    int QuotationCount
);

public record RevenueReportDto(
    decimal TotalRevenue,
    int TotalQuotations,
    decimal AverageQuotationValue,
    IReadOnlyList<RevenueCategoryItemDto> BreakdownByCategory,
    IReadOnlyList<RevenuePeriodItemDto> PeriodicTrends
);

public record ConditionItemDto(
    string Diagnosis,
    int CaseCount,
    double Percentage,
    IReadOnlyList<string> CommonTreatments,
    IReadOnlyList<string> AffectedSpecies
);

public record CommonConditionReportDto(
    int TotalRecordsAnalysed,
    IReadOnlyList<ConditionItemDto> TopConditions
);

public record VeterinarianWorkloadReportDto(
    IReadOnlyList<VetWorkloadItemDto> Veterinarians
);

public record AuditLogItemDto(
    Guid Id,
    Guid OrganizationId,
    string UserId,
    string UserEmail,
    string Action,
    string EntityType,
    string EntityId,
    string Details,
    DateTime Timestamp
);
