using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPrescriptionRepository"/> for the
/// medicine-request workflow. Prescriptions inherit organization scope
/// through TreatmentRecord -&gt; Diagnosis -&gt; Examination -&gt;
/// Veterinarian -&gt; Organization.
/// </summary>
public class PrescriptionRepository : IPrescriptionRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public PrescriptionRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    /// <summary>
    /// Loads the navigation chain needed for the denormalised
    /// PrescriptionResponseDto fields.
    /// </summary>
    private static IQueryable<Prescription> WithDetails(IQueryable<Prescription> query) =>
        query
            .Include(p => p.Medicine)
            .Include(p => p.TreatmentRecord).ThenInclude(t => t!.Diagnosis).ThenInclude(d => d!.Examination).ThenInclude(e => e!.Veterinarian)
            .Include(p => p.TreatmentRecord).ThenInclude(t => t!.Diagnosis).ThenInclude(d => d!.Examination).ThenInclude(e => e!.Pet).ThenInclude(pet => pet!.Owner);

    private async Task<IQueryable<Prescription>> ScopedAsync(CancellationToken cancellationToken = default) =>
        await WithDetails(_context.Prescriptions)
            .ScopeToOrganizationAsync(
                _tenant,
                p => p.TreatmentRecord!.Diagnosis!.Examination!.Veterinarian!.OrganizationId,
                cancellationToken);

    public async Task<Prescription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Tracked (no AsNoTracking): issue/unavailable mutate the entity.
        return await (await ScopedAsync(cancellationToken))
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Prescription>> GetRequestsAsync(
        MedicineRequestStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = await ScopedAsync(cancellationToken);

        if (status is not null)
        {
            query = query.Where(p => p.RequestStatus == status.Value);
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Prescription>> GetByVeterinarianAsync(Guid veterinarianId, CancellationToken cancellationToken = default)
    {
        return await (await ScopedAsync(cancellationToken))
            .Where(p => p.TreatmentRecord!.Diagnosis!.Examination!.VeterinarianId == veterinarianId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForTreatmentRecordAsync(Guid treatmentRecordId, CancellationToken cancellationToken = default)
    {
        // No detail includes needed for an existence check — the scope
        // expression itself carries the organization join.
        var query = await _context.Prescriptions
            .ScopeToOrganizationAsync(
                _tenant,
                p => p.TreatmentRecord!.Diagnosis!.Examination!.Veterinarian!.OrganizationId,
                cancellationToken);

        return await query.AnyAsync(p => p.TreatmentRecordId == treatmentRecordId, cancellationToken);
    }
}
