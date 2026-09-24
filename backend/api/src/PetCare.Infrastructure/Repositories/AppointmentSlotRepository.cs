using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Repositories;

public class AppointmentSlotRepository : IAppointmentSlotRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public AppointmentSlotRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<AppointmentSlot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await _context.AppointmentSlots
            .ScopeToOrganizationAsync(_tenant, s => s.Veterinarian.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentSlot>> GetAvailableAsync(
        Guid? veterinarianId,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AppointmentSlots
            .Where(s => s.Status == AppointmentSlotStatus.Available);

        query = await query.ScopeToOrganizationAsync(_tenant, s => s.Veterinarian.OrganizationId, cancellationToken);

        if (veterinarianId is not null)
        {
            query = query.Where(s => s.VeterinarianId == veterinarianId);
        }

        if (date is not null)
        {
            query = query.Where(s => s.Date == date);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
