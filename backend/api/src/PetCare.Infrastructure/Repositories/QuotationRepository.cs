using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

public class QuotationRepository : IQuotationRepository
{
    private readonly PetCareDbContext _context;

    public QuotationRepository(PetCareDbContext context)
    {
        _context = context;
    }

    public Task<Quotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Quotations
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Quotation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Quotations
            .Include(q => q.Items)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsForAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        return _context.Quotations.AnyAsync(q => q.AppointmentId == appointmentId, cancellationToken);
    }

    public Task AddAsync(Quotation quotation, CancellationToken cancellationToken = default)
    {
        _context.Quotations.Add(quotation);
        return Task.CompletedTask;
    }
}
