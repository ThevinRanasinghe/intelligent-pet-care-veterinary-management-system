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

    public async Task<AppointmentSlot?> GetByVeterinarianDateStartAsync(
        Guid veterinarianId, DateOnly date, TimeOnly startTime,
        CancellationToken cancellationToken = default)
    {
        return await _context.AppointmentSlots
            .FirstOrDefaultAsync(
                s => s.VeterinarianId == veterinarianId
                     && s.Date == date
                     && s.StartTime == startTime,
                cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentSlot>> GetManyByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var query = _context.AppointmentSlots.Where(s => ids.Contains(s.Id));
        query = await query.ScopeToOrganizationAsync(_tenant, s => s.Veterinarian.OrganizationId, cancellationToken);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<int> TryReserveAvailableAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        // Conditional bulk update: each listed slot flips only while its
        // status is still Available. Rows already taken by another writer
        // are simply not updated, and the caller detects the shortfall.
        // Org membership of the ids is validated by the caller beforehand.
        return await _context.AppointmentSlots
            .Where(s => ids.Contains(s.Id) && s.Status == AppointmentSlotStatus.Available)
            .ExecuteUpdateAsync(
                u => u.SetProperty(s => s.Status, AppointmentSlotStatus.Reserved),
                cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentSlot>> GetByStatusInWindowAsync(
        Guid veterinarianId, DateOnly date, TimeOnly start, TimeOnly end,
        AppointmentSlotStatus status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AppointmentSlots
            .Where(s => s.VeterinarianId == veterinarianId
                        && s.Date == date
                        && s.StartTime >= start
                        && s.StartTime < end
                        && s.Status == status);
        query = await query.ScopeToOrganizationAsync(_tenant, s => s.Veterinarian.OrganizationId, cancellationToken);
        return await query.ToListAsync(cancellationToken);
    }

    public Task AddAsync(AppointmentSlot slot, CancellationToken cancellationToken = default)
    {
        _context.AppointmentSlots.Add(slot);
        return Task.CompletedTask;
    }
}
