using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Repositories;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public AppointmentRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    /// <summary>
    /// Loads the navigations needed for the denormalised AppointmentResponse
    /// fields (pet/owner/veterinarian names, consultation symptoms,
    /// examination id).
    /// </summary>
    private static IQueryable<Appointment> WithDetails(IQueryable<Appointment> query) =>
        query
            .Include(a => a.Pet).ThenInclude(p => p.Owner)
            .Include(a => a.Veterinarian)
            .Include(a => a.ConsultationRequest)
            .Include(a => a.Examination);

    public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await WithDetails(_context.Appointments)
            .ScopeToOrganizationAsync(_tenant, a => a.Veterinarian.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var query = await WithDetails(_context.Appointments)
            .ScopeToOrganizationAsync(_tenant, a => a.Veterinarian.OrganizationId, cancellationToken);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetActiveByVeterinarianAndDateAsync(
        Guid veterinarianId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var query = await _context.Appointments
            .ScopeToOrganizationAsync(_tenant, a => a.Veterinarian.OrganizationId, cancellationToken);
        return await query
            .Where(a => a.VeterinarianId == veterinarianId
                && a.Date == date
                && a.Status != AppointmentStatus.Cancelled)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetActiveByVeterinariansAndRangeAsync(
        IReadOnlyCollection<Guid> veterinarianIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (veterinarianIds.Count == 0)
        {
            return Array.Empty<Appointment>();
        }

        // No tenant scoping — the caller already constrained the vet set, and
        // PetOwner callers (unscoped) need availability for any clinic.
        return await _context.Appointments
            .AsNoTracking()
            .Where(a => veterinarianIds.Contains(a.VeterinarianId)
                && a.Date >= from
                && a.Date <= to
                && a.Status != AppointmentStatus.Cancelled)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetByVeterinarianAsync(
        Guid veterinarianId,
        CancellationToken cancellationToken = default)
    {
        var query = await WithDetails(_context.Appointments)
            .ScopeToOrganizationAsync(_tenant, a => a.Veterinarian.OrganizationId, cancellationToken);
        return await query
            .Where(a => a.VeterinarianId == veterinarianId)
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.StartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetByOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        // Owner path: PetOwner callers are not organization-scoped, so this
        // query filters purely on pet ownership.
        return await WithDetails(_context.Appointments)
            .Where(a => a.Pet.OwnerId == ownerId)
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.StartTime)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        _context.Appointments.Add(appointment);
        return Task.CompletedTask;
    }
}
