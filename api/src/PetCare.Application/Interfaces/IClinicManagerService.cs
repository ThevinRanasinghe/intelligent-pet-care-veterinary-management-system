using PetCare.Application.DTOs.Dashboard;

namespace PetCare.Application.Interfaces;

public interface IClinicManagerService
{
    Task<ClinicDashboardSummaryDto> GetDashboardSummaryAsync(
        Guid organizationId,
        CancellationToken ct = default);

    Task<RevenueReportDto> GetRevenueReportAsync(
        Guid organizationId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? period = null,
        CancellationToken ct = default);

    Task<CommonConditionReportDto> GetCommonConditionsReportAsync(
        Guid organizationId,
        CancellationToken ct = default);

    Task<VeterinarianWorkloadReportDto> GetVeterinarianWorkloadReportAsync(
        Guid organizationId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogItemDto>> GetAuditLogsAsync(
        Guid organizationId,
        string? search = null,
        string? action = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task LogAuditAsync(
        Guid organizationId,
        string userId,
        string userEmail,
        string action,
        string entityType,
        string entityId,
        string details,
        CancellationToken ct = default);
}
