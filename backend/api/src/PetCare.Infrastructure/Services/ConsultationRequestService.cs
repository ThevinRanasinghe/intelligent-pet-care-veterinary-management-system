using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Services;

public class ConsultationRequestService : IConsultationRequestService
{
    private readonly IPetCareDbContext _context;

    public ConsultationRequestService(IPetCareDbContext context)
    {
        _context = context;
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

            SymptomsDescription = dto.Symptoms.Trim(),

            PhotoUrl = string.IsNullOrWhiteSpace(dto.SymptomPhotoUrl)
                ? null
                : dto.SymptomPhotoUrl.Trim(),

            // PostgreSQL timestamp with time zone requires UTC DateTime
            PreferredDate = dto.PreferredDate.HasValue
                ? DateTime.SpecifyKind(
                    dto.PreferredDate.Value,
                    DateTimeKind.Utc)
                : DateTime.UtcNow,

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
        return await _context.ConsultationRequests
            .AsNoTracking()
            .Include(x => x.Pet)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => MapToDto(x))
            .ToListAsync();
    }


    // =========================================================
    // GET CONSULTATION REQUESTS BY OWNER
    // =========================================================
    public async Task<List<ConsultationRequestDto>> GetByOwnerIdAsync(
        string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("Owner ID is required.");

        return await _context.ConsultationRequests
            .AsNoTracking()
            .Include(x => x.Pet)
            .Where(x => x.OwnerId == ownerId.Trim())
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => MapToDto(x))
            .ToListAsync();
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
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        if (consultation == null)
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

        if (dto.PreferredDate.HasValue)
        {
            // PostgreSQL timestamp with time zone requires UTC DateTime
            consultation.PreferredDate =
                DateTime.SpecifyKind(
                    dto.PreferredDate.Value,
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

            // Current entity does not have PreferredTime.
            PreferredTime = null,

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

            CreatedAt =
                consultation.CreatedAt,

            UpdatedAt =
                consultation.UpdatedAt
        };
    }
}