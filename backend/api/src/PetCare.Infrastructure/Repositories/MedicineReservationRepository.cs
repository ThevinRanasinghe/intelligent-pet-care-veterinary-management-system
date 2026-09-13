using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Infrastructure.Repositories;

public class MedicineReservationRepository : IMedicineReservationRepository
{
    private readonly PetCareDbContext _context;

    public MedicineReservationRepository(PetCareDbContext context) => _context = context;

    public Task<MedicineReservation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.MedicineReservations.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(MedicineReservation reservation, CancellationToken ct = default) =>
        await _context.MedicineReservations.AddAsync(reservation, ct);
}