using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Repositories;

public class AppointmentSlotRepository : IAppointmentSlotRepository
{
    private readonly PetCareDbContext _context;

    public AppointmentSlotRepository(PetCareDbContext context)
    {
        _context = context;
    }

    public Task<AppointmentSlot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.AppointmentSlots.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentSlot>> GetAvailableAsync(
        Guid? veterinarianId,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AppointmentSlots
            .Where(s => s.Status == AppointmentSlotStatus.Available);

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
