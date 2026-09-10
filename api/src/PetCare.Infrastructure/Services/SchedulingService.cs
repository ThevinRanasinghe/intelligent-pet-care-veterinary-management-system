using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Infrastructure.Services;

public sealed class SchedulingService : ISchedulingService
{
    private readonly PetCareDbContext _dbContext;

    public SchedulingService(PetCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppointmentResponseDto>> GetAppointmentsAsync(
        Guid organizationId,
        string? search = null,
        AppointmentStatus? status = null,
        DateOnly? date = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.Appointments
            .Include(a => a.Veterinarian)
            .Include(a => a.AppointmentSlot)
            .Include(a => a.Quotation)
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId || (a.Veterinarian != null && a.Veterinarian.OrganizationId == organizationId));

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (date.HasValue)
        {
            query = query.Where(a => a.Date == date.Value);
        }

        var list = await query
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.StartTime)
            .ToListAsync(ct);

        // Fetch pet & owner names if available
        var petIds = list.Select(a => a.PetId.ToString()).Distinct().ToList();
        var pets = await _dbContext.Pets
            .Include(p => p.Owner)
            .AsNoTracking()
            .Where(p => petIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var result = new List<AppointmentResponseDto>();
        foreach (var a in list)
        {
            var pet = pets.GetValueOrDefault(a.PetId.ToString());
            var petName = pet?.Name ?? $"Pet {a.PetId.ToString()[..8]}";
            var ownerName = pet?.Owner?.FullName ?? "Pet Owner";

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                var match = petName.ToLower().Contains(term)
                    || ownerName.ToLower().Contains(term)
                    || (a.Veterinarian?.Name.ToLower().Contains(term) ?? false)
                    || (a.Notes?.ToLower().Contains(term) ?? false);
                if (!match) continue;
            }

            result.Add(new AppointmentResponseDto(
                Id: a.Id,
                OrganizationId: a.OrganizationId,
                PetId: a.PetId,
                PetName: petName,
                OwnerName: ownerName,
                VeterinarianId: a.VeterinarianId,
                VeterinarianName: a.Veterinarian?.Name ?? "Veterinarian",
                AppointmentSlotId: a.AppointmentSlotId,
                Date: a.Date,
                StartTime: a.StartTime,
                EndTime: a.EndTime,
                Status: a.Status.ToString(),
                Notes: a.Notes,
                QuotationTotal: a.Quotation?.Total,
                CreatedAt: a.CreatedAt
            ));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AppointmentResponseDto?> GetAppointmentByIdAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var a = await _dbContext.Appointments
            .Include(a => a.Veterinarian)
            .Include(a => a.AppointmentSlot)
            .Include(a => a.Quotation)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && (a.OrganizationId == organizationId || (a.Veterinarian != null && a.Veterinarian.OrganizationId == organizationId)), ct);

        if (a is null) return null;

        var pet = await _dbContext.Pets
            .Include(p => p.Owner)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == a.PetId.ToString(), ct);

        return new AppointmentResponseDto(
            Id: a.Id,
            OrganizationId: a.OrganizationId,
            PetId: a.PetId,
            PetName: pet?.Name ?? $"Pet {a.PetId.ToString()[..8]}",
            OwnerName: pet?.Owner?.FullName ?? "Pet Owner",
            VeterinarianId: a.VeterinarianId,
            VeterinarianName: a.Veterinarian?.Name ?? "Veterinarian",
            AppointmentSlotId: a.AppointmentSlotId,
            Date: a.Date,
            StartTime: a.StartTime,
            EndTime: a.EndTime,
            Status: a.Status.ToString(),
            Notes: a.Notes,
            QuotationTotal: a.Quotation?.Total,
            CreatedAt: a.CreatedAt
        );
    }

    /// <inheritdoc />
    public async Task<AppointmentResponseDto> CreateAppointmentAsync(
        Guid organizationId,
        CreateAppointmentDto request,
        CancellationToken ct = default)
    {
        if (request.EndTime <= request.StartTime)
            throw new InvalidOperationException("End time must be after start time.");

        // Verify veterinarian belongs to manager's organization
        var vet = await _dbContext.Veterinarians
            .FirstOrDefaultAsync(v => v.Id == request.VeterinarianId && v.OrganizationId == organizationId && v.Active, ct);

        if (vet is null)
            throw new InvalidOperationException("Veterinarian not found or inactive in your organization.");

        // Verify slot belongs to veterinarian and is Available
        var slot = await _dbContext.AppointmentSlots
            .FirstOrDefaultAsync(s => s.Id == request.AppointmentSlotId && s.VeterinarianId == request.VeterinarianId, ct);

        if (slot is null)
            throw new InvalidOperationException("Appointment slot not found for this veterinarian.");

        if (slot.Status != AppointmentSlotStatus.Available)
            throw new InvalidOperationException("The requested appointment slot is not available.");

        // Overlap conflict check
        var hasConflict = await CheckConflictAsync(organizationId, new ConflictCheckDto(
            request.VeterinarianId, request.Date, request.StartTime, request.EndTime), ct);

        if (hasConflict)
            throw new InvalidOperationException("Proposed appointment overlaps with an existing non-cancelled appointment for this veterinarian.");

        // Mark slot reserved
        slot.Status = AppointmentSlotStatus.Reserved;
        slot.UpdatedAt = DateTimeOffset.UtcNow;

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            PetId = request.PetId,
            VeterinarianId = request.VeterinarianId,
            AppointmentSlotId = request.AppointmentSlotId,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = AppointmentStatus.Reserved,
            Notes = request.Notes,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Appointments.Add(appointment);
        await _dbContext.SaveChangesAsync(ct);

        var pet = await _dbContext.Pets
            .Include(p => p.Owner)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PetId.ToString(), ct);

        return new AppointmentResponseDto(
            Id: appointment.Id,
            OrganizationId: appointment.OrganizationId,
            PetId: appointment.PetId,
            PetName: pet?.Name ?? $"Pet {appointment.PetId.ToString()[..8]}",
            OwnerName: pet?.Owner?.FullName ?? "Pet Owner",
            VeterinarianId: appointment.VeterinarianId,
            VeterinarianName: vet.Name,
            AppointmentSlotId: appointment.AppointmentSlotId,
            Date: appointment.Date,
            StartTime: appointment.StartTime,
            EndTime: appointment.EndTime,
            Status: appointment.Status.ToString(),
            Notes: appointment.Notes,
            QuotationTotal: null,
            CreatedAt: appointment.CreatedAt
        );
    }

    /// <inheritdoc />
    public async Task<AppointmentResponseDto> UpdateAppointmentAsync(
        Guid id,
        Guid organizationId,
        UpdateAppointmentDto request,
        CancellationToken ct = default)
    {
        if (request.EndTime <= request.StartTime)
            throw new InvalidOperationException("End time must be after start time.");

        var appt = await _dbContext.Appointments
            .Include(a => a.Veterinarian)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId, ct);

        if (appt is null)
            throw new KeyNotFoundException("Appointment not found in your organization.");

        var hasConflict = await CheckConflictAsync(organizationId, new ConflictCheckDto(
            appt.VeterinarianId, request.Date, request.StartTime, request.EndTime, appt.Id), ct);

        if (hasConflict)
            throw new InvalidOperationException("Updated appointment time overlaps with an existing appointment for this veterinarian.");

        appt.Date = request.Date;
        appt.StartTime = request.StartTime;
        appt.EndTime = request.EndTime;
        if (request.Notes != null) appt.Notes = request.Notes;
        appt.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return (await GetAppointmentByIdAsync(id, organizationId, ct))!;
    }

    /// <inheritdoc />
    public async Task<AppointmentResponseDto> UpdateAppointmentStatusAsync(
        Guid id,
        Guid organizationId,
        AppointmentStatus newStatus,
        CancellationToken ct = default)
    {
        var appt = await _dbContext.Appointments
            .Include(a => a.AppointmentSlot)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId, ct);

        if (appt is null)
            throw new KeyNotFoundException("Appointment not found in your organization.");

        appt.Status = newStatus;
        appt.UpdatedAt = DateTimeOffset.UtcNow;

        if (appt.AppointmentSlot != null)
        {
            if (newStatus == AppointmentStatus.Cancelled)
            {
                appt.AppointmentSlot.Status = AppointmentSlotStatus.Available;
            }
            else if (newStatus == AppointmentStatus.Confirmed)
            {
                appt.AppointmentSlot.Status = AppointmentSlotStatus.Confirmed;
            }
            appt.AppointmentSlot.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
        return (await GetAppointmentByIdAsync(id, organizationId, ct))!;
    }

    /// <inheritdoc />
    public async Task CancelAppointmentAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default)
    {
        await UpdateAppointmentStatusAsync(id, organizationId, AppointmentStatus.Cancelled, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppointmentSlotResponseDto>> GetSlotsAsync(
        Guid organizationId,
        Guid? veterinarianId = null,
        DateOnly? date = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.AppointmentSlots
            .Include(s => s.Veterinarian)
            .Include(s => s.Appointment)
            .AsNoTracking()
            .Where(s => s.Veterinarian.OrganizationId == organizationId);

        if (veterinarianId.HasValue)
            query = query.Where(s => s.VeterinarianId == veterinarianId.Value);

        if (date.HasValue)
            query = query.Where(s => s.Date == date.Value);

        var list = await query
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        return list.Select(s => new AppointmentSlotResponseDto(
            Id: s.Id,
            VeterinarianId: s.VeterinarianId,
            VeterinarianName: s.Veterinarian.Name,
            Date: s.Date,
            StartTime: s.StartTime,
            EndTime: s.EndTime,
            Branch: s.Branch,
            Status: s.Status.ToString(),
            PetName: null,
            OwnerName: null
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<AppointmentSlotResponseDto> CreateSlotAsync(
        Guid organizationId,
        CreateAppointmentSlotDto request,
        CancellationToken ct = default)
    {
        if (request.EndTime <= request.StartTime)
            throw new InvalidOperationException("End time must be after start time.");

        var vet = await _dbContext.Veterinarians
            .FirstOrDefaultAsync(v => v.Id == request.VeterinarianId && v.OrganizationId == organizationId && v.Active, ct);

        if (vet is null)
            throw new InvalidOperationException("Veterinarian not found or inactive in your organization.");

        // Check for exact slot duplicate
        var duplicate = await _dbContext.AppointmentSlots
            .AnyAsync(s => s.VeterinarianId == request.VeterinarianId
                        && s.Date == request.Date
                        && s.StartTime == request.StartTime, ct);

        if (duplicate)
            throw new InvalidOperationException("A slot with the exact same start time already exists for this veterinarian.");

        var slot = new AppointmentSlot
        {
            Id = Guid.NewGuid(),
            VeterinarianId = request.VeterinarianId,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Branch = string.IsNullOrWhiteSpace(request.Branch) ? vet.Branch : request.Branch.Trim(),
            Status = AppointmentSlotStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.AppointmentSlots.Add(slot);
        await _dbContext.SaveChangesAsync(ct);

        return new AppointmentSlotResponseDto(
            Id: slot.Id,
            VeterinarianId: slot.VeterinarianId,
            VeterinarianName: vet.Name,
            Date: slot.Date,
            StartTime: slot.StartTime,
            EndTime: slot.EndTime,
            Branch: slot.Branch,
            Status: slot.Status.ToString(),
            PetName: null,
            OwnerName: null
        );
    }

    /// <inheritdoc />
    public async Task<AppointmentSlotResponseDto> UpdateSlotAsync(
        Guid id,
        Guid organizationId,
        UpdateAppointmentSlotDto request,
        CancellationToken ct = default)
    {
        if (request.EndTime <= request.StartTime)
            throw new InvalidOperationException("End time must be after start time.");

        var slot = await _dbContext.AppointmentSlots
            .Include(s => s.Veterinarian)
            .FirstOrDefaultAsync(s => s.Id == id && s.Veterinarian.OrganizationId == organizationId, ct);

        if (slot is null)
            throw new KeyNotFoundException("Appointment slot not found in your organization.");

        slot.Date = request.Date;
        slot.StartTime = request.StartTime;
        slot.EndTime = request.EndTime;
        if (!string.IsNullOrWhiteSpace(request.Branch)) slot.Branch = request.Branch.Trim();
        if (request.Status.HasValue) slot.Status = request.Status.Value;
        slot.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return new AppointmentSlotResponseDto(
            Id: slot.Id,
            VeterinarianId: slot.VeterinarianId,
            VeterinarianName: slot.Veterinarian.Name,
            Date: slot.Date,
            StartTime: slot.StartTime,
            EndTime: slot.EndTime,
            Branch: slot.Branch,
            Status: slot.Status.ToString(),
            PetName: null,
            OwnerName: null
        );
    }

    /// <inheritdoc />
    public async Task DeleteSlotAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var slot = await _dbContext.AppointmentSlots
            .Include(s => s.Veterinarian)
            .FirstOrDefaultAsync(s => s.Id == id && s.Veterinarian.OrganizationId == organizationId, ct);

        if (slot is null)
            throw new KeyNotFoundException("Appointment slot not found in your organization.");

        if (slot.Status != AppointmentSlotStatus.Available)
            throw new InvalidOperationException("Only available slots that have no active appointment can be removed.");

        _dbContext.AppointmentSlots.Remove(slot);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppointmentSlotResponseDto>> GetAvailableSlotsAsync(
        Guid organizationId,
        Guid? veterinarianId = null,
        DateOnly? date = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.AppointmentSlots
            .Include(s => s.Veterinarian)
            .AsNoTracking()
            .Where(s => s.Veterinarian.OrganizationId == organizationId && s.Status == AppointmentSlotStatus.Available && s.Veterinarian.Active);

        if (veterinarianId.HasValue)
            query = query.Where(s => s.VeterinarianId == veterinarianId.Value);

        if (date.HasValue)
            query = query.Where(s => s.Date == date.Value);

        var list = await query
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        return list.Select(s => new AppointmentSlotResponseDto(
            Id: s.Id,
            VeterinarianId: s.VeterinarianId,
            VeterinarianName: s.Veterinarian.Name,
            Date: s.Date,
            StartTime: s.StartTime,
            EndTime: s.EndTime,
            Branch: s.Branch,
            Status: s.Status.ToString(),
            PetName: null,
            OwnerName: null
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> CheckConflictAsync(
        Guid organizationId,
        ConflictCheckDto request,
        CancellationToken ct = default)
    {
        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.VeterinarianId == request.VeterinarianId
                     && a.Date == request.Date
                     && a.Status != AppointmentStatus.Cancelled);

        if (request.ExcludeAppointmentId.HasValue)
        {
            query = query.Where(a => a.Id != request.ExcludeAppointmentId.Value);
        }

        var appointments = await query.ToListAsync(ct);

        return appointments.Any(a => request.StartTime < a.EndTime && request.EndTime > a.StartTime);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VeterinarianDto>> GetOrganizationVeterinariansAsync(
        Guid organizationId,
        CancellationToken ct = default)
    {
        var vets = await _dbContext.Veterinarians
            .AsNoTracking()
            .Where(v => v.OrganizationId == organizationId)
            .OrderBy(v => v.Name)
            .ToListAsync(ct);

        return vets.Select(v => new VeterinarianDto(
            Id: v.Id,
            OrganizationId: v.OrganizationId,
            Name: v.Name,
            Specialisation: v.Specialisation,
            Branch: v.Branch,
            Active: v.Active
        )).ToList();
    }
}
