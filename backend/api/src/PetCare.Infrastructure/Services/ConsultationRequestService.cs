using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Services;

public class ConsultationRequestService : IConsultationRequestService
{
    private readonly IPetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public ConsultationRequestService(
        IPetCareDbContext context,
        ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    // =========================================================
    // CREATE CONSULTATION REQUEST
    // =========================================================
    public async Task<ConsultationRequestDto> CreateAsync(
        CreateConsultationRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.OwnerId))
            throw new ArgumentException("Owner ID is required.");

        if (string.IsNullOrWhiteSpace(dto.PetId))
            throw new ArgumentException("Pet ID is required.");

        if (string.IsNullOrWhiteSpace(dto.Symptoms))
            throw new ArgumentException("Symptoms are required.");

        if (dto.Budget.HasValue && dto.Budget.Value < 0)
            throw new ArgumentException("Budget cannot be negative.");

        if (dto.OrganizationId == Guid.Empty)
            throw new ArgumentException("An organization (clinic) must be selected.");

        if (!dto.PreferredDate.HasValue)
            throw new ArgumentException("Preferred date is required.");

        if (!dto.PreferredTime.HasValue)
            throw new ArgumentException("A time slot is required.");

        var slotDate = DateOnly.FromDateTime(dto.PreferredDate.Value);
        var slotStart = TimeOnly.FromTimeSpan(dto.PreferredTime.Value);

        if (!BookingRules.IsHourAligned(slotStart) ||
            !BookingRules.IsWithinOperatingHours(slotStart))
        {
            throw new ArgumentException(
                "Appointments start on the hour between 09:00 and 17:00 (one-hour slots, closing at 18:00).");
        }

        if (!BookingRules.IsInFuture(slotDate, slotStart))
        {
            throw new ArgumentException(
                "The selected date/time slot cannot be in the past.");
        }

        var organization = await RequireActiveOrganizationAsync(
            dto.OrganizationId);

        // The hour is bookable only while at least one active veterinarian
        // of the clinic has no overlapping appointment.
        await EnsureSlotAvailableAsync(
            organization.Id, slotDate, slotStart);

        // Check whether owner exists
        var ownerExists = await _context.PetOwners
            .AnyAsync(x => x.Id == dto.OwnerId.Trim());

        if (!ownerExists)
            throw new ArgumentException("Pet owner was not found.");

        // Check whether pet exists and belongs to this owner
        var pet = await _context.Pets
            .FirstOrDefaultAsync(x =>
                x.Id == dto.PetId.Trim() &&
                x.OwnerId == dto.OwnerId.Trim());

        if (pet == null)
            throw new ArgumentException(
                "Pet was not found or does not belong to this owner.");

        var consultation = new ConsultationRequest
        {
            Id = $"CON-{Guid.NewGuid():N}"
                .Substring(0, 12)
                .ToUpper(),

            PetId = dto.PetId.Trim(),

            OwnerId = dto.OwnerId.Trim(),

            OrganizationId = organization.Id,

            SymptomsDescription = dto.Symptoms.Trim(),

            PhotoUrl = string.IsNullOrWhiteSpace(dto.SymptomPhotoUrl)
                ? null
                : dto.SymptomPhotoUrl.Trim(),

            // The preferred date column carries the booked slot start:
            // booking date + hour-aligned slot start, stored as UTC-kind.
            // PostgreSQL timestamp with time zone requires UTC DateTime.
            PreferredDate = DateTime.SpecifyKind(
                dto.PreferredDate.Value.Date + dto.PreferredTime.Value,
                DateTimeKind.Utc),

            BudgetLimit = dto.Budget ?? 0,

            PreferredClinicLocationLat =
                dto.Latitude.HasValue
                    ? (double?)dto.Latitude.Value
                    : null,

            PreferredClinicLocationLong =
                dto.Longitude.HasValue
                    ? (double?)dto.Longitude.Value
                    : null,

            PreferredBranch = null,

            Status = "Draft",

            StatusNotes = string.IsNullOrWhiteSpace(dto.AdditionalNotes)
                ? null
                : dto.AdditionalNotes.Trim(),

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow
        };

        _context.ConsultationRequests.Add(consultation);

        await _context.SaveChangesAsync();

        return await GetByIdAsync(consultation.Id)
            ?? throw new InvalidOperationException(
                "Consultation request could not be retrieved after creation.");
    }


    // =========================================================
    // GET ALL CONSULTATION REQUESTS
    // =========================================================
    public async Task<List<ConsultationRequestDto>> GetAllAsync()
    {
        var query = _context.ConsultationRequests
            .AsNoTracking()
            .Include(x => x.Pet)
            .Include(x => x.Organization)
            .Include(x => x.RequestedByVeterinarian)
            .AsQueryable();

        query = await ApplyOrganizationScopeAsync(query);

        // Materialize before mapping — MapToDto is not translatable to SQL.
        var consultations = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return consultations.Select(MapToDto).ToList();
    }


    // =========================================================
    // GET CONSULTATION REQUESTS BY OWNER
    // =========================================================
    public async Task<List<ConsultationRequestDto>> GetByOwnerIdAsync(
        string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("Owner ID is required.");

        var query = _context.ConsultationRequests
            .AsNoTracking()
            .Include(x => x.Pet)
            .Include(x => x.Organization)
            .Include(x => x.RequestedByVeterinarian)
            .Where(x => x.OwnerId == ownerId.Trim());

        query = await ApplyOrganizationScopeAsync(query);

        // Materialize before mapping — MapToDto is not translatable to SQL.
        var consultations = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return consultations.Select(MapToDto).ToList();
    }


    // =========================================================
    // GET CONSULTATION REQUEST BY ID
    // =========================================================
    public async Task<ConsultationRequestDto?> GetByIdAsync(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var consultation = await _context.ConsultationRequests
            .AsNoTracking()
            .Include(x => x.Pet)
            .Include(x => x.Organization)
            .Include(x => x.RequestedByVeterinarian)
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        if (consultation == null)
            return null;

        // Foreign-organization requests are invisible to scoped staff.
        if (!await CanAccessAsync(consultation))
            return null;

        return MapToDto(consultation);
    }


    // =========================================================
    // UPDATE CONSULTATION REQUEST
    // =========================================================
    public async Task<ConsultationRequestDto?> UpdateAsync(
        string id,
        UpdateConsultationRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (string.IsNullOrWhiteSpace(dto.Symptoms))
            throw new ArgumentException("Symptoms are required.");

        if (dto.Budget.HasValue && dto.Budget.Value < 0)
            throw new ArgumentException("Budget cannot be negative.");

        var consultation = await _context.ConsultationRequests
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        if (consultation == null)
            return null;

        if (!await CanAccessAsync(consultation))
            return null;

        // Only Draft or RevisionRequired requests
        // can be edited.
        if (consultation.Status != "Draft" &&
            consultation.Status != "RevisionRequired")
        {
            throw new InvalidOperationException(
                "This consultation request cannot be updated in its current status.");
        }

        consultation.SymptomsDescription =
            dto.Symptoms.Trim();

        consultation.PhotoUrl =
            string.IsNullOrWhiteSpace(dto.SymptomPhotoUrl)
                ? null
                : dto.SymptomPhotoUrl.Trim();

        if (dto.OrganizationId.HasValue)
        {
            var organization =
                await RequireActiveOrganizationAsync(
                    dto.OrganizationId.Value);

            consultation.OrganizationId = organization.Id;
        }

        if (dto.PreferredDate.HasValue && dto.PreferredTime.HasValue)
        {
            var slotDate = DateOnly.FromDateTime(dto.PreferredDate.Value);
            var slotStart = TimeOnly.FromTimeSpan(dto.PreferredTime.Value);

            if (!BookingRules.IsHourAligned(slotStart) ||
                !BookingRules.IsWithinOperatingHours(slotStart))
            {
                throw new ArgumentException(
                    "Appointments start on the hour between 09:00 and 17:00 (one-hour slots, closing at 18:00).");
            }

            if (!BookingRules.IsInFuture(slotDate, slotStart))
            {
                throw new ArgumentException(
                    "The selected date/time slot cannot be in the past.");
            }

            // Store booking date + slot start together (UTC-kind).
            consultation.PreferredDate =
                DateTime.SpecifyKind(
                    dto.PreferredDate.Value.Date +
                    dto.PreferredTime.Value,
                    DateTimeKind.Utc);
        }

        if (dto.Budget.HasValue)
        {
            consultation.BudgetLimit =
                dto.Budget.Value;
        }

        consultation.PreferredClinicLocationLat =
            dto.Latitude.HasValue
                ? (double?)dto.Latitude.Value
                : null;

        consultation.PreferredClinicLocationLong =
            dto.Longitude.HasValue
                ? (double?)dto.Longitude.Value
                : null;

        consultation.StatusNotes =
            string.IsNullOrWhiteSpace(dto.AdditionalNotes)
                ? null
                : dto.AdditionalNotes.Trim();

        consultation.UpdatedAt =
            DateTime.UtcNow;

        // Re-check slot capacity at the (possibly changed) clinic/date/time
        // so a moved request never lands on a fully-booked slot.
        if (consultation.OrganizationId.HasValue)
        {
            await EnsureSlotAvailableAsync(
                consultation.OrganizationId.Value,
                DateOnly.FromDateTime(consultation.PreferredDate),
                TimeOnly.FromDateTime(consultation.PreferredDate),
                ignoreConsultationId: consultation.Id);
        }

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }


    // =========================================================
    // SUBMIT CONSULTATION REQUEST
    // =========================================================
    public async Task<ConsultationRequestDto?> SubmitAsync(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var consultation = await _context.ConsultationRequests
            .Include(x => x.Pet)
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        if (consultation == null)
            return null;

        if (!await CanAccessAsync(consultation))
            return null;

        // Only Draft requests can be submitted
        if (consultation.Status != "Draft")
        {
            throw new InvalidOperationException(
                "Only draft consultation requests can be submitted.");
        }

        // Verify pet ownership again
        if (consultation.Pet == null ||
            consultation.Pet.OwnerId != consultation.OwnerId)
        {
            throw new InvalidOperationException(
                "The selected pet does not belong to the request owner.");
        }

        if (string.IsNullOrWhiteSpace(
                consultation.SymptomsDescription))
        {
            throw new ArgumentException(
                "Symptoms are required before submission.");
        }

        if (consultation.BudgetLimit < 0)
        {
            throw new ArgumentException(
                "Budget cannot be negative.");
        }

        // The slot may have filled since the request was created — the
        // backend is the final authority on availability.
        if (consultation.OrganizationId.HasValue)
        {
            await EnsureSlotAvailableAsync(
                consultation.OrganizationId.Value,
                DateOnly.FromDateTime(consultation.PreferredDate),
                TimeOnly.FromDateTime(consultation.PreferredDate),
                ignoreConsultationId: consultation.Id);
        }

        consultation.Status = "Submitted";

        consultation.StatusNotes =
            "Consultation request submitted by pet owner.";

        consultation.UpdatedAt =
            DateTime.UtcNow;

        // Add status history
        var history = new ConsultationStatusHistory
        {
            ConsultationRequestId =
                consultation.Id,

            Status = "Submitted",

            Comments =
                "Consultation request submitted by pet owner.",

            ChangedAt = DateTime.UtcNow
        };

        _context.ConsultationStatusHistories
            .Add(history);

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }


    // =========================================================
    // CANCEL CONSULTATION REQUEST
    // =========================================================
    public async Task<bool> CancelAsync(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        var consultation = await _context.ConsultationRequests
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        if (consultation == null)
            return false;

        if (!await CanAccessAsync(consultation))
            return false;

        if (consultation.Status == "Cancelled")
            return false;

        // Cannot cancel approved/confirmed requests
        if (consultation.Status == "Approved" ||
            consultation.Status == "AppointmentConfirmed")
        {
            throw new InvalidOperationException(
                "This consultation request cannot be cancelled at this stage.");
        }

        var oldStatus = consultation.Status;

        consultation.Status = "Cancelled";

        consultation.StatusNotes =
            "Cancelled by pet owner.";

        consultation.UpdatedAt =
            DateTime.UtcNow;

        // Add status history
        var history = new ConsultationStatusHistory
        {
            ConsultationRequestId =
                consultation.Id,

            Status = "Cancelled",

            Comments =
                $"Status changed from {oldStatus} to Cancelled.",

            ChangedAt = DateTime.UtcNow
        };

        _context.ConsultationStatusHistories
            .Add(history);

        await _context.SaveChangesAsync();

        return true;
    }


    // =========================================================
    // GET CONSULTATION STATUS HISTORY
    // =========================================================
    public async Task<List<ConsultationStatusHistoryDto>>
        GetStatusHistoryAsync(string consultationId)
    {
        if (string.IsNullOrWhiteSpace(consultationId))
        {
            return new List<ConsultationStatusHistoryDto>();
        }

        var consultation =
            await _context.ConsultationRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == consultationId.Trim());

        if (consultation == null ||
            !await CanAccessAsync(consultation))
        {
            throw new ArgumentException(
                "Consultation request was not found.");
        }

        return await _context.ConsultationStatusHistories
            .AsNoTracking()
            .Where(x =>
                x.ConsultationRequestId == consultationId.Trim())
            .OrderBy(x => x.ChangedAt)
            .Select(x => new ConsultationStatusHistoryDto
            {
                Id = x.Id,

                ConsultationRequestId =
                    x.ConsultationRequestId,

                Status =
                    x.Status,

                Comments =
                    x.Comments,

                ChangedAt =
                    x.ChangedAt
            })
            .ToListAsync();
    }


    // =========================================================
    // VALIDATE PET OWNERSHIP
    // =========================================================
    public async Task<bool> ValidateOwnershipAsync(
        string petId,
        string ownerId)
    {
        if (string.IsNullOrWhiteSpace(petId) ||
            string.IsNullOrWhiteSpace(ownerId))
        {
            return false;
        }

        return await _context.Pets
            .AnyAsync(x =>
                x.Id == petId.Trim() &&
                x.OwnerId == ownerId.Trim());
    }


    // =========================================================
    // BOOKING-RULE HELPERS
    // =========================================================

    /// <summary>
    /// Loads the organization and enforces that it exists and is Active.
    /// Unknown id → 404; inactive/pending org → 400.
    /// </summary>
    private async Task<Organization> RequireActiveOrganizationAsync(
        Guid organizationId)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(x => x.Id == organizationId);

        if (organization == null)
        {
            throw new NotFoundException(
                "The selected organization was not found.");
        }

        if (organization.Status != OrganizationStatus.Active ||
            !organization.IsActive)
        {
            throw new ArgumentException(
                "The selected organization is not active.");
        }

        return organization;
    }

    /// <summary>
    /// A requested (date, start, start+1h) hour is bookable only while at
    /// least one active veterinarian of the organization has no overlapping
    /// non-cancelled appointment. Throws a 409 conflict otherwise.
    /// </summary>
    private async Task EnsureSlotAvailableAsync(
        Guid organizationId,
        DateOnly date,
        TimeOnly start,
        string? ignoreConsultationId = null)
    {
        var veterinarianIds = await _context.Veterinarians
            .Where(v =>
                v.OrganizationId == organizationId &&
                v.Active)
            .Select(v => v.Id)
            .ToListAsync();

        if (veterinarianIds.Count == 0)
        {
            throw new SchedulingConflictException(
                BookingRules.SlotUnavailableMessage);
        }

        var end = start.AddMinutes(BookingRules.SlotDurationMinutes);

        var busyVeterinarianIds = await _context.Appointments
            .Where(a =>
                veterinarianIds.Contains(a.VeterinarianId) &&
                a.Date == date &&
                a.Status != AppointmentStatus.Cancelled &&
                a.StartTime < end &&
                a.EndTime > start &&
                (ignoreConsultationId == null ||
                 a.ConsultationRequestId != ignoreConsultationId))
            .Select(a => a.VeterinarianId)
            .Distinct()
            .ToListAsync();

        if (busyVeterinarianIds.Count >= veterinarianIds.Count)
        {
            throw new SchedulingConflictException(
                BookingRules.SlotUnavailableMessage);
        }
    }

    // =========================================================
    // ORGANIZATION SCOPING HELPERS
    // =========================================================

    /// <summary>
    /// Restricts a query to the caller's organization when the caller is
    /// organization-scoped staff (ClinicManager / Veterinarian /
    /// InventoryOfficer). PetOwner and platform-Admin callers are unscoped.
    /// </summary>
    private async Task<IQueryable<ConsultationRequest>>
        ApplyOrganizationScopeAsync(
            IQueryable<ConsultationRequest> query)
    {
        if (!_tenant.IsOrganizationScoped)
        {
            return query;
        }

        var organizationId =
            await _tenant.GetOrganizationIdAsync();

        return query.Where(x => x.OrganizationId == organizationId);
    }

    /// <summary>Foreign-organization requests are invisible to scoped staff.</summary>
    private async Task<bool> CanAccessAsync(
        ConsultationRequest consultation)
    {
        if (!_tenant.IsOrganizationScoped)
        {
            return true;
        }

        var organizationId =
            await _tenant.GetOrganizationIdAsync();

        return consultation.OrganizationId == organizationId;
    }


    // =========================================================
    // MAP ENTITY TO DTO
    // =========================================================
    private static ConsultationRequestDto MapToDto(
        ConsultationRequest consultation)
    {
        return new ConsultationRequestDto
        {
            Id = consultation.Id,

            PetId = consultation.PetId,

            OwnerId = consultation.OwnerId,

            PetName =
                consultation.Pet?.Name
                ?? string.Empty,

            Symptoms =
                consultation.SymptomsDescription,

            SymptomPhotoUrl =
                consultation.PhotoUrl,

            // Current entity does not have Urgency.
            // Keep DTO compatibility.
            Urgency = "Medium",

            PreferredDate =
                consultation.PreferredDate,

            // The booked slot start is stored inside PreferredDate
            // (date + hour-aligned time).
            PreferredTime =
                consultation.PreferredDate.TimeOfDay,

            OrganizationId =
                consultation.OrganizationId,

            OrganizationName =
                consultation.Organization?.Name,

            Budget =
                consultation.BudgetLimit,

            Latitude =
                consultation.PreferredClinicLocationLat.HasValue
                    ? (decimal?)consultation.PreferredClinicLocationLat.Value
                    : null,

            Longitude =
                consultation.PreferredClinicLocationLong.HasValue
                    ? (decimal?)consultation.PreferredClinicLocationLong.Value
                    : null,

            AdditionalNotes =
                consultation.StatusNotes,

            Status =
                consultation.Status,

            RequestType =
                consultation.RequestType,

            RequestedByVeterinarianId =
                consultation.RequestedByVeterinarianId,

            RequestedByVeterinarianName =
                consultation.RequestedByVeterinarian?.Name,

            CreatedAt =
                consultation.CreatedAt,

            UpdatedAt =
                consultation.UpdatedAt
        };
    }
}
