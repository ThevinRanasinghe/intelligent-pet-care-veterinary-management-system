using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Repositories;

public class ApprovalRepository : IApprovalRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public ApprovalRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<Approval?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = _context.Approvals
            .Include(a => a.Quotation)
            .Include(a => a.History)
            .AsQueryable();
        query = await query.ScopeToOrganizationAsync(_tenant, a => a.Quotation.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Approval?> GetByQuotationIdAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        var query = await _context.Approvals
            .ScopeToOrganizationAsync(_tenant, a => a.Quotation.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(a => a.QuotationId == quotationId, cancellationToken);
    }

    public async Task<IReadOnlyList<Approval>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var query = _context.Approvals
            .Include(a => a.Quotation)
            .AsQueryable();
        query = await query.ScopeToOrganizationAsync(_tenant, a => a.Quotation.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query
            .Where(a => a.Status == ApprovalStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Approval approval, CancellationToken cancellationToken = default)
    {
        _context.Approvals.Add(approval);
        return Task.CompletedTask;
    }

    public Task AddHistoryAsync(ApprovalHistory history, CancellationToken cancellationToken = default)
    {
        _context.ApprovalHistories.Add(history);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ApprovalHistory>> GetHistoryAsync(Guid approvalId, CancellationToken cancellationToken = default)
    {
        var query = await _context.ApprovalHistories
            .ScopeToOrganizationAsync(_tenant, h => h.Approval.Quotation.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query
            .Where(h => h.ApprovalId == approvalId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(cancellationToken);
    }
}
