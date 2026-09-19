using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Repositories;

public class ApprovalRepository : IApprovalRepository
{
    private readonly PetCareDbContext _context;

    public ApprovalRepository(PetCareDbContext context)
    {
        _context = context;
    }

    public Task<Approval?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Approvals
            .Include(a => a.Quotation)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public Task<Approval?> GetByQuotationIdAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        return _context.Approvals
            .FirstOrDefaultAsync(a => a.QuotationId == quotationId, cancellationToken);
    }

    public async Task<IReadOnlyList<Approval>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Approvals
            .Include(a => a.Quotation)
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
        return await _context.ApprovalHistories
            .Where(h => h.ApprovalId == approvalId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(cancellationToken);
    }
}
