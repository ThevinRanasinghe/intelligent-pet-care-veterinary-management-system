using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IExaminationRepository"/>.
/// Examinations inherit organization scope from the attending veterinarian.
/// </summary>
public class ExaminationRepository : IExaminationRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public ExaminationRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    /// <summary>Full graph needed for billing: the prescription chain down to
    /// Medicine, plus Appointment → ConsultationRequest so follow-up requests
    /// can inherit the booking organization.</summary>
    private static IQueryable<Examination> WithBillingDetails(IQueryable<Examination> query) =>
        query
            .Include(e => e.Veterinarian)
            .Include(e => e.Pet).ThenInclude(p => p!.Owner)
            .Include(e => e.Appointment).ThenInclude(a => a!.ConsultationRequest)
            .Include(e => e.Diagnosis).ThenInclude(d => d!.TreatmentRecords).ThenInclude(t => t.Prescriptions).ThenInclude(p => p.Medicine);

    public async Task<Examination?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await WithBillingDetails(_context.Examinations)
            .ScopeToOrganizationAsync(_tenant, e => e.Veterinarian!.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Examination>> GetByVeterinarianAsync(Guid veterinarianId, CancellationToken cancellationToken = default)
    {
        var query = await _context.Examinations
            .Include(e => e.Appointment)
            .ScopeToOrganizationAsync(_tenant, e => e.Veterinarian!.OrganizationId, cancellationToken);
        return await query
            .Where(e => e.VeterinarianId == veterinarianId)
            .OrderByDescending(e => e.ExaminationDate)
            .ToListAsync(cancellationToken);
    }
}
