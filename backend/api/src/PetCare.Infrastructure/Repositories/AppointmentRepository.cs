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

    public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await _context.Appointments
            .ScopeToOrganizationAsync(_tenant, a => a.Veterinarian.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var query = await _context.Appointments
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

    public Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        _context.Appointments.Add(appointment);
        return Task.CompletedTask;
    }
}
