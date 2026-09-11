using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Api.DTOs;
using PetCare.Application.DTOs.Dashboard;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

/// <summary>
/// Clinic Manager Dashboard, Reporting, and Audit Trail endpoints.
/// All metrics and reports are strictly scoped to the manager's organization.
/// </summary>
[ApiController]
[Route("api/clinic-manager")]
[Produces("application/json")]
[Authorize(Roles = $"{Roles.ClinicManager},{Roles.SuperAdmin}")]
public sealed class ClinicManagerDashboardController : ControllerBase
{
    private readonly IClinicManagerService _clinicManagerService;
    private readonly ICurrentUserService _currentUser;

    public ClinicManagerDashboardController(
        IClinicManagerService clinicManagerService,
        ICurrentUserService currentUser)
    {
        _clinicManagerService = clinicManagerService;
        _currentUser = currentUser;
    }

    private Guid? GetOrganizationId() => _currentUser.OrganizationId;

    /// <summary>
    /// DASHBOARD: Real-time operational KPI metrics, charts, pending proposals, and today's schedule.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<ClinicDashboardSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var dashboard = await _clinicManagerService.GetDashboardSummaryAsync(orgId.Value, ct);
        return Ok(ApiResponse<ClinicDashboardSummaryDto>.Ok(dashboard));
    }

    /// <summary>
    /// REVENUE REPORT: Detailed revenue analytics with daily/monthly breakdowns and service categorization.
    /// </summary>
    [HttpGet("reports/revenue")]
    [ProducesResponseType(typeof(ApiResponse<RevenueReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenueReport(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? period,
        CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var report = await _clinicManagerService.GetRevenueReportAsync(orgId.Value, fromDate, toDate, period, ct);
        return Ok(ApiResponse<RevenueReportDto>.Ok(report));
    }

    /// <summary>
    /// CONDITIONS REPORT: Common veterinary conditions frequency and species breakdowns.
    /// </summary>
    [HttpGet("reports/common-conditions")]
    [ProducesResponseType(typeof(ApiResponse<CommonConditionReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCommonConditionsReport(CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var report = await _clinicManagerService.GetCommonConditionsReportAsync(orgId.Value, ct);
        return Ok(ApiResponse<CommonConditionReportDto>.Ok(report));
    }

    /// <summary>
    /// WORKLOAD REPORT: Veterinarian workload, utilization, and performance metrics.
    /// </summary>
    [HttpGet("reports/veterinarian-workload")]
    [ProducesResponseType(typeof(ApiResponse<VeterinarianWorkloadReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVeterinarianWorkloadReport(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var report = await _clinicManagerService.GetVeterinarianWorkloadReportAsync(orgId.Value, fromDate, toDate, ct);
        return Ok(ApiResponse<VeterinarianWorkloadReportDto>.Ok(report));
    }

    /// <summary>
    /// AUDIT LOGS: Complete audit trail of administrative actions for the clinic.
    /// </summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditLogItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? search,
        [FromQuery] string? action,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var logs = await _clinicManagerService.GetAuditLogsAsync(orgId.Value, search, action, pageNumber, pageSize, ct);
        return Ok(ApiResponse<IReadOnlyList<AuditLogItemDto>>.Ok(logs));
    }
}
