using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

public class QuotationRepository : IQuotationRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public QuotationRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<Quotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = _context.Quotations
            .Include(q => q.Items)
            .AsQueryable();
        query = await query.ScopeToOrganizationAsync(_tenant, q => q.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Quotation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var query = _context.Quotations
            .Include(q => q.Items)
            .AsQueryable();
        query = await query.ScopeToOrganizationAsync(_tenant, q => q.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        var query = await _context.Quotations
            .ScopeToOrganizationAsync(_tenant, q => q.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query.AnyAsync(q => q.AppointmentId == appointmentId, cancellationToken);
    }

    public Task AddAsync(Quotation quotation, CancellationToken cancellationToken = default)
    {
        _context.Quotations.Add(quotation);
        return Task.CompletedTask;
    }
}
