using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Dashboard;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Infrastructure.Services;

public sealed class ClinicManagerService : IClinicManagerService
{
    private readonly PetCareDbContext _dbContext;

    public ClinicManagerService(PetCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<ClinicDashboardSummaryDto> GetDashboardSummaryAsync(
        Guid organizationId,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Appointments counts
        var appointmentsQuery = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId);

        var todayCount = await appointmentsQuery
            .CountAsync(a => a.Date == today && a.Status != AppointmentStatus.Cancelled, ct);

        var upcomingCount = await appointmentsQuery
            .CountAsync(a => a.Date >= today && a.Status != AppointmentStatus.Cancelled, ct);

        // 2. Pending proposals count
        var pendingProposals = await _dbContext.AIProposals
            .AsNoTracking()
            .CountAsync(p => p.OrganizationId == organizationId && p.Status == "Pending", ct);

        // 3. Active veterinarians
        var activeVets = await _dbContext.Veterinarians
            .AsNoTracking()
            .CountAsync(v => v.OrganizationId == organizationId && v.Active, ct);

        // 4. Total staff count
        var staffCount = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(u => u.OrganizationId == organizationId && u.AccountStatus == UserAccountStatus.Active, ct);

        // 5. Total revenue from approved quotations
        var totalRevenue = await _dbContext.Quotations
            .Include(q => q.Appointment)
            .AsNoTracking()
            .Where(q => q.Appointment.OrganizationId == organizationId && (q.Status == QuotationStatus.Approved || q.Status == QuotationStatus.Finalised))
            .SumAsync(q => q.Total, ct);

        // 6. Appointment status distribution
        var statusGroups = await appointmentsQuery
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        var statusCounts = statusGroups
            .Select(g => new StatusCountDto(g.Status, g.Count))
            .ToList();

        // 7. Veterinarian workloads
        var vets = await _dbContext.Veterinarians
            .Include(v => v.Appointments)
            .AsNoTracking()
            .Where(v => v.OrganizationId == organizationId && v.Active)
            .ToListAsync(ct);

        var vetWorkloads = vets.Select(v => new VetWorkloadItemDto(
            VeterinarianId: v.Id,
            VeterinarianName: v.Name,
            Specialisation: v.Specialisation,
            TotalAppointments: v.Appointments.Count,
            CompletedAppointments: v.Appointments.Count(a => a.Status == AppointmentStatus.Completed),
            UpcomingAppointments: v.Appointments.Count(a => a.Date >= today && a.Status != AppointmentStatus.Cancelled),
            CancelledAppointments: v.Appointments.Count(a => a.Status == AppointmentStatus.Cancelled)
        )).ToList();

        // 8. Recent Activities
        var recentLogs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(l => l.OrganizationId == organizationId)
            .OrderByDescending(l => l.Timestamp)
            .Take(6)
            .ToListAsync(ct);

        var recentActivities = recentLogs.Select(l => new RecentActivityDto(
            Id: l.Id,
            Action: l.Action,
            EntityType: l.EntityType,
            EntityId: l.EntityId,
            Details: l.Details,
            Timestamp: l.Timestamp,
            UserEmail: l.UserEmail
        )).ToList();

        return new ClinicDashboardSummaryDto(
            TodayAppointmentsCount: todayCount,
            UpcomingAppointmentsCount: upcomingCount,
            PendingProposalsCount: pendingProposals,
            ActiveVeterinariansCount: activeVets,
            TotalStaffCount: staffCount,
            TotalRevenue: totalRevenue,
            AppointmentStatusCounts: statusCounts,
            VeterinarianWorkloads: vetWorkloads,
            RecentActivities: recentActivities
        );
    }

    /// <inheritdoc />
    public async Task<RevenueReportDto> GetRevenueReportAsync(
        Guid organizationId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? period = null,
        CancellationToken ct = default)
    {
        var start = startDate ?? DateTime.UtcNow.AddMonths(-6);
        var end = endDate ?? DateTime.UtcNow;

        var quotations = await _dbContext.Quotations
            .Include(q => q.Appointment)
            .Include(q => q.Items)
            .AsNoTracking()
            .Where(q => q.Appointment.OrganizationId == organizationId
                     && (q.Status == QuotationStatus.Approved || q.Status == QuotationStatus.Finalised)
                     && q.CreatedAt >= start
                     && q.CreatedAt <= end)
            .OrderBy(q => q.CreatedAt)
            .ToListAsync(ct);

        decimal totalRevenue = quotations.Sum(q => q.Total);
        int totalQuotations = quotations.Count;
        decimal avgValue = totalQuotations > 0 ? Math.Round(totalRevenue / totalQuotations, 2) : 0m;

        // Breakdown by Category
        var allItems = quotations.SelectMany(q => q.Items).ToList();
        var categoryGroups = allItems
            .GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? "General" : i.Category)
            .Select(g => new
            {
                Category = g.Key,
                Amount = g.Sum(i => i.TotalPrice)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var breakdown = categoryGroups.Select(cg => new RevenueCategoryItemDto(
            Category: cg.Category,
            Amount: cg.Amount,
            Percentage: totalRevenue > 0 ? Math.Round((double)(cg.Amount / totalRevenue) * 100, 1) : 0
        )).ToList();

        // Periodic trends (group by Month or Date)
        var periodGroups = quotations
            .GroupBy(q => q.CreatedAt.ToString(period == "day" ? "yyyy-MM-dd" : "yyyy-MM"))
            .Select(g => new RevenuePeriodItemDto(
                Period: g.Key,
                Amount: g.Sum(q => q.Total),
                QuotationCount: g.Count()
            ))
            .OrderBy(p => p.Period)
            .ToList();

        return new RevenueReportDto(
            TotalRevenue: totalRevenue,
            TotalQuotations: totalQuotations,
            AverageQuotationValue: avgValue,
            BreakdownByCategory: breakdown,
            PeriodicTrends: periodGroups
        );
    }

    /// <inheritdoc />
    public async Task<CommonConditionReportDto> GetCommonConditionsReportAsync(
        Guid organizationId,
        CancellationToken ct = default)
    {
        // Query medical records
        var records = await _dbContext.MedicalRecords
            .Include(m => m.Pet)
            .AsNoTracking()
            .ToListAsync(ct);

        int totalCount = records.Count;

        var conditionGroups = records
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Diagnosis) ? "General Health Checkup" : r.Diagnosis.Trim())
            .Select(g => new ConditionItemDto(
                Diagnosis: g.Key,
                CaseCount: g.Count(),
                Percentage: totalCount > 0 ? Math.Round((double)g.Count() / totalCount * 100, 1) : 0,
                CommonTreatments: g.Select(r => r.Treatment).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().Take(3).ToList(),
                AffectedSpecies: g.Select(r => r.Pet?.Species ?? "Canine/Feline").Distinct().Take(3).ToList()
            ))
            .OrderByDescending(c => c.CaseCount)
            .Take(10)
            .ToList();

        return new CommonConditionReportDto(
            TotalRecordsAnalysed: totalCount,
            TopConditions: conditionGroups
        );
    }

    /// <inheritdoc />
    public async Task<VeterinarianWorkloadReportDto> GetVeterinarianWorkloadReportAsync(
        Guid organizationId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : DateOnly.MinValue;
        var end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : DateOnly.MaxValue;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var vets = await _dbContext.Veterinarians
            .Include(v => v.Appointments)
            .AsNoTracking()
            .Where(v => v.OrganizationId == organizationId)
            .ToListAsync(ct);

        var workloads = vets.Select(v =>
        {
            var appts = v.Appointments.Where(a => a.Date >= start && a.Date <= end).ToList();
            return new VetWorkloadItemDto(
                VeterinarianId: v.Id,
                VeterinarianName: v.Name,
                Specialisation: v.Specialisation,
                TotalAppointments: appts.Count,
                CompletedAppointments: appts.Count(a => a.Status == AppointmentStatus.Completed),
                UpcomingAppointments: appts.Count(a => a.Date >= today && a.Status != AppointmentStatus.Cancelled),
                CancelledAppointments: appts.Count(a => a.Status == AppointmentStatus.Cancelled)
            );
        }).OrderByDescending(w => w.TotalAppointments).ToList();

        return new VeterinarianWorkloadReportDto(workloads);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLogItemDto>> GetAuditLogsAsync(
        Guid organizationId,
        string? search = null,
        string? action = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .Where(l => l.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(l => l.Action.ToLower() == action.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(l => l.Details.ToLower().Contains(term)
                                  || l.UserEmail.ToLower().Contains(term)
                                  || l.EntityType.ToLower().Contains(term)
                                  || l.EntityId.ToLower().Contains(term));
        }

        var skip = Math.Max(0, (page - 1) * pageSize);
        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return logs.Select(l => new AuditLogItemDto(
            Id: l.Id,
            OrganizationId: l.OrganizationId,
            UserId: l.UserId,
            UserEmail: l.UserEmail,
            Action: l.Action,
            EntityType: l.EntityType,
            EntityId: l.EntityId,
            Details: l.Details,
            Timestamp: l.Timestamp
        )).ToList();
    }

    /// <inheritdoc />
    public async Task LogAuditAsync(
        Guid organizationId,
        string userId,
        string userEmail,
        string action,
        string entityType,
        string entityId,
        string details,
        CancellationToken ct = default)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            UserEmail = userEmail,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(ct);
    }
}
