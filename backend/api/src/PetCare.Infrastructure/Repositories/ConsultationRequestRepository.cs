using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IConsultationRequestRepository"/>.
/// Reads are organization-scoped for staff callers — a foreign-organization
/// request resolves to null (404) rather than being reachable across
/// clinics. Unscoped callers (PetOwner — owner checks happen elsewhere —
/// and the platform Administrator) see all rows.
/// </summary>
public class ConsultationRequestRepository : IConsultationRequestRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public ConsultationRequestRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<ConsultationRequest?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        // Tracked on purpose: workflow callers mutate Status/StatusNotes and
        // append StatusHistories, persisted by the shared SaveChanges.
        var query = await _context.ConsultationRequests
            .ScopeToOrganizationAsync(_tenant, x => x.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task AddAsync(ConsultationRequest request, CancellationToken cancellationToken = default)
    {
        _context.ConsultationRequests.Add(request);
        return Task.CompletedTask;
    }
}
