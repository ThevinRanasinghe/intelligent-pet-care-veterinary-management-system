using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Repositories;

public class MedicineReservationRepository : IMedicineReservationRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public MedicineReservationRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<MedicineReservation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var query = _context.MedicineReservations
            .Include(r => r.Medicine)
            .AsQueryable();
        query = await query.ScopeToOrganizationAsync(_tenant, r => r.Medicine.OrganizationId, ct);
        return await query.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<MedicineReservation>> GetAllAsync(CancellationToken ct = default)
    {
        var query = _context.MedicineReservations
            .AsNoTracking()
            .Include(r => r.Medicine)
            .AsQueryable();
        query = await query.ScopeToOrganizationAsync(_tenant, r => r.Medicine.OrganizationId, ct);
        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    public async Task AddAsync(MedicineReservation reservation, CancellationToken ct = default) =>
        await _context.MedicineReservations.AddAsync(reservation, ct);

    public async Task<int> TryTransitionAsync(
        Guid reservationId, ReservationStatus from, ReservationStatus to, CancellationToken ct = default)
    {
        var query = _context.MedicineReservations.AsQueryable();
        query = await query.ScopeToOrganizationAsync(_tenant, r => r.Medicine.OrganizationId, ct);
        return await query
            .Where(r => r.Id == reservationId && r.Status == from)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.Status, to), ct);
    }
}