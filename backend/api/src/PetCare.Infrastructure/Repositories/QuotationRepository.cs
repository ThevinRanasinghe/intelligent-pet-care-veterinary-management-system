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

    /// <summary>
    /// Loads the line items plus the appointment graph needed for the
    /// denormalised QuotationResponse fields (pet/owner, veterinarian,
    /// clinic, examination) and the prescription medication chain
    /// (Examination -> Diagnosis -> TreatmentRecords -> Prescriptions ->
    /// Medicine) used for the owner-facing medication instructions.
    /// </summary>
    private static IQueryable<Quotation> WithDetails(IQueryable<Quotation> query) =>
        query
            .Include(q => q.Items)
            .Include(q => q.Appointment).ThenInclude(a => a.Pet).ThenInclude(p => p.Owner)
            .Include(q => q.Appointment).ThenInclude(a => a.Veterinarian).ThenInclude(v => v.Organization)
            .Include(q => q.Appointment).ThenInclude(a => a.Examination).ThenInclude(e => e.Veterinarian)
            .Include(q => q.Appointment).ThenInclude(a => a.Examination).ThenInclude(e => e.Pet).ThenInclude(p => p!.Owner)
            .Include(q => q.Appointment).ThenInclude(a => a.Examination).ThenInclude(e => e.Diagnosis).ThenInclude(d => d!.TreatmentRecords).ThenInclude(t => t.Prescriptions).ThenInclude(p => p.Medicine);

    public async Task<Quotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await WithDetails(_context.Quotations)
            .ScopeToOrganizationAsync(_tenant, q => q.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Quotation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var query = await WithDetails(_context.Quotations)
            .ScopeToOrganizationAsync(_tenant, q => q.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        var query = await _context.Quotations
            .ScopeToOrganizationAsync(_tenant, q => q.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query.AnyAsync(q => q.AppointmentId == appointmentId, cancellationToken);
    }

    public async Task<Quotation?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        var query = await WithDetails(_context.Quotations)
            .ScopeToOrganizationAsync(_tenant, q => q.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(q => q.AppointmentId == appointmentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Quotation>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        // Owner path: PetOwner callers are not organization-scoped, so this
        // query filters purely on pet ownership.
        return await WithDetails(_context.Quotations)
            .Where(q => q.Appointment.Pet.OwnerId == ownerId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Quotation>> GetByVeterinarianAsync(Guid veterinarianId, CancellationToken cancellationToken = default)
    {
        var query = await WithDetails(_context.Quotations)
            .ScopeToOrganizationAsync(_tenant, q => q.Appointment.Veterinarian.OrganizationId, cancellationToken);
        return await query
            .Where(q => q.Appointment.VeterinarianId == veterinarianId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Quotation quotation, CancellationToken cancellationToken = default)
    {
        _context.Quotations.Add(quotation);
        return Task.CompletedTask;
    }
}
