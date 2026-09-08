using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Domain.Exceptions;

namespace PetCare.Application.Services;

public class ConsultationService : IConsultationService
{
    private readonly IPetCareDbContext _context;
    private readonly IIdGenerator _idGenerator;

    public ConsultationService(IPetCareDbContext context, IIdGenerator idGenerator)
    {
        _context = context;
        _idGenerator = idGenerator;
    }

    // UC-09 to UC-13: Submit Consultation Request
    public async Task<ConsultationResponseDto> SubmitConsultationRequestAsync(CreateConsultationRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PetId))
            throw new ArgumentException("Pet ID is required.", nameof(dto.PetId));

        if (string.IsNullOrWhiteSpace(dto.OwnerId))
            throw new ArgumentException("Owner ID is required.", nameof(dto.OwnerId));

        if (string.IsNullOrWhiteSpace(dto.SymptomsDescription))
            throw new ArgumentException("Symptoms description is required.", nameof(dto.SymptomsDescription));

        if (dto.BudgetLimit <= 0)
            throw new ArgumentException("Budget limit must be greater than zero.", nameof(dto.BudgetLimit));

        // Coordinate range validation
        if (dto.PreferredClinicLocationLat.HasValue &&
            (dto.PreferredClinicLocationLat.Value < -90 || dto.PreferredClinicLocationLat.Value > 90))
        {
            throw new ArgumentException("Latitude must be between -90 and 90 degrees.", nameof(dto.PreferredClinicLocationLat));
        }

        if (dto.PreferredClinicLocationLong.HasValue &&
            (dto.PreferredClinicLocationLong.Value < -180 || dto.PreferredClinicLocationLong.Value > 180))
        {
            throw new ArgumentException("Longitude must be between -180 and 180 degrees.", nameof(dto.PreferredClinicLocationLong));
        }

        // UC-14: Validate Pet Ownership (Security & Business Logic Check)
        var pet = await _context.Pets
            .Include(p => p.Owner)
            .FirstOrDefaultAsync(p => p.Id == dto.PetId);

        if (pet == null)
            throw new NotFoundException($"Pet with ID '{dto.PetId}' was not found.");

        if (pet.OwnerId != dto.OwnerId)
            throw new OwnershipValidationException(dto.PetId, dto.OwnerId);

        var requestId = !string.IsNullOrWhiteSpace(dto.Id)
            ? dto.Id
            : await _idGenerator.GenerateConsultationIdAsync();

        var request = new ConsultationRequest
        {
            Id = requestId,
            PetId = dto.PetId,
            OwnerId = dto.OwnerId,
            SymptomsDescription = dto.SymptomsDescription.Trim(),
            PhotoUrl = dto.PhotoUrl,
            PreferredDate = dto.PreferredDate == default ? DateTime.UtcNow.AddDays(1) : dto.PreferredDate,
            BudgetLimit = dto.BudgetLimit,
            PreferredClinicLocationLat = dto.PreferredClinicLocationLat,
            PreferredClinicLocationLong = dto.PreferredClinicLocationLong,
            PreferredBranch = dto.PreferredBranch,
            Status = ConsultationStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // UC-17: Initial entry in status audit trail
        var history = new ConsultationStatusHistory
        {
            ConsultationRequestId = requestId,
            Status = ConsultationStatus.Submitted,
            Comments = "Consultation request submitted by pet owner.",
            ChangedAt = DateTime.UtcNow
        };

        _context.ConsultationRequests.Add(request);
        _context.ConsultationStatusHistories.Add(history);
        await _context.SaveChangesAsync();

        return MapToResponse(request, pet.Name, pet.Owner?.FullName);
    }

    // UC-14: Validate Pet Ownership
    public async Task<bool> ValidatePetOwnershipAsync(string petId, string ownerId)
    {
        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.Id == petId);
        if (pet == null)
            throw new NotFoundException($"Pet with ID '{petId}' was not found.");

        return pet.OwnerId == ownerId;
    }

    // UC-15: View Requests
    public async Task<ConsultationResponseDto> GetConsultationByIdAsync(string id)
    {
        var request = await _context.ConsultationRequests
            .Include(r => r.Pet)
            .Include(r => r.Owner)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            throw new NotFoundException($"Consultation request with ID '{id}' was not found.");

        return MapToResponse(request, request.Pet?.Name, request.Owner?.FullName);
    }

    public async Task<IEnumerable<ConsultationResponseDto>> GetConsultationsByOwnerAsync(string ownerId)
    {
        var requests = await _context.ConsultationRequests
            .Include(r => r.Pet)
            .Include(r => r.Owner)
            .Where(r => r.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return requests.Select(r => MapToResponse(r, r.Pet?.Name, r.Owner?.FullName));
    }

    public async Task<IEnumerable<ConsultationResponseDto>> GetConsultationsByPetAsync(string petId)
    {
        var requests = await _context.ConsultationRequests
            .Include(r => r.Pet)
            .Include(r => r.Owner)
            .Where(r => r.PetId == petId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return requests.Select(r => MapToResponse(r, r.Pet?.Name, r.Owner?.FullName));
    }

    public async Task<IEnumerable<ConsultationResponseDto>> GetAllConsultationsAsync(ConsultationStatus? statusFilter = null)
    {
        var query = _context.ConsultationRequests
            .Include(r => r.Pet)
            .Include(r => r.Owner)
            .AsQueryable();

        if (statusFilter.HasValue)
        {
            query = query.Where(r => r.Status == statusFilter.Value);
        }

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return requests.Select(r => MapToResponse(r, r.Pet?.Name, r.Owner?.FullName));
    }

    // UC-16: Track Consultation Status
    public async Task<ConsultationStatusTrackingDto> GetConsultationStatusAsync(string id)
    {
        var request = await _context.ConsultationRequests
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            throw new NotFoundException($"Consultation request with ID '{id}' was not found.");

        var history = await _context.ConsultationStatusHistories
            .Where(h => h.ConsultationRequestId == id)
            .OrderBy(h => h.ChangedAt)
            .Select(h => new ConsultationHistoryItemDto
            {
                Id = h.Id,
                Status = h.Status.ToString(),
                Comments = h.Comments,
                ChangedAt = h.ChangedAt
            })
            .ToListAsync();

        return new ConsultationStatusTrackingDto
        {
            ConsultationId = request.Id,
            PetId = request.PetId,
            Status = request.Status.ToString(),
            StatusNotes = request.StatusNotes,
            UpdatedAt = request.UpdatedAt,
            History = history
        };
    }

    public async Task<ConsultationResponseDto> UpdateConsultationStatusAsync(string id, UpdateConsultationStatusDto dto)
    {
        var request = await _context.ConsultationRequests
            .Include(r => r.Pet)
            .Include(r => r.Owner)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            throw new NotFoundException($"Consultation request with ID '{id}' was not found.");

        request.Status = dto.Status;
        request.StatusNotes = dto.Comments;
        request.UpdatedAt = DateTime.UtcNow;

        // Record status change in audit trail (UC-17)
        var history = new ConsultationStatusHistory
        {
            ConsultationRequestId = request.Id,
            Status = dto.Status,
            Comments = dto.Comments ?? $"Status changed to {dto.Status}",
            ChangedAt = DateTime.UtcNow
        };

        _context.ConsultationStatusHistories.Add(history);
        await _context.SaveChangesAsync();

        return MapToResponse(request, request.Pet?.Name, request.Owner?.FullName);
    }

    // UC-17: View Requests & Request History
    public async Task<IEnumerable<ConsultationHistoryItemDto>> GetConsultationHistoryAsync(string id)
    {
        var exists = await _context.ConsultationRequests.AnyAsync(r => r.Id == id);
        if (!exists)
            throw new NotFoundException($"Consultation request with ID '{id}' was not found.");

        return await _context.ConsultationStatusHistories
            .Where(h => h.ConsultationRequestId == id)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new ConsultationHistoryItemDto
            {
                Id = h.Id,
                Status = h.Status.ToString(),
                Comments = h.Comments,
                ChangedAt = h.ChangedAt
            })
            .ToListAsync();
    }

    private static ConsultationResponseDto MapToResponse(ConsultationRequest request, string? petName = null, string? ownerName = null)
    {
        return new ConsultationResponseDto
        {
            Id = request.Id,
            PetId = request.PetId,
            PetName = petName,
            OwnerId = request.OwnerId,
            OwnerName = ownerName,
            SymptomsDescription = request.SymptomsDescription,
            PhotoUrl = request.PhotoUrl,
            PreferredDate = request.PreferredDate,
            BudgetLimit = request.BudgetLimit,
            PreferredClinicLocationLat = request.PreferredClinicLocationLat,
            PreferredClinicLocationLong = request.PreferredClinicLocationLong,
            PreferredBranch = request.PreferredBranch,
            Status = request.Status.ToString(),
            StatusNotes = request.StatusNotes,
            CreatedAt = request.CreatedAt,
            UpdatedAt = request.UpdatedAt
        };
    }
}
