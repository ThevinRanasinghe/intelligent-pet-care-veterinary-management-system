using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Repositories;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly PetCareDbContext _context;

    public AppointmentRepository(PetCareDbContext context)
    {
        _context = context;
    }

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Appointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Appointments.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetActiveByVeterinarianAndDateAsync(
        Guid veterinarianId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
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
