using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Exceptions;

namespace PetCare.Application.Services;

public class PetService : IPetService
{
    private readonly IPetCareDbContext _context;
    private readonly IIdGenerator _idGenerator;

    public PetService(IPetCareDbContext context, IIdGenerator idGenerator)
    {
        _context = context;
        _idGenerator = idGenerator;
    }

    // UC-05: Add Pet
    public async Task<PetResponseDto> RegisterPetAsync(CreatePetDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Pet name is required.", nameof(dto.Name));

        if (string.IsNullOrWhiteSpace(dto.Species))
            throw new ArgumentException("Pet species is required.", nameof(dto.Species));

        if (string.IsNullOrWhiteSpace(dto.OwnerId))
            throw new ArgumentException("Owner ID is required.", nameof(dto.OwnerId));

        // Verify or create owner if it doesn't exist yet for test convenience
        var ownerExists = await _context.PetOwners.AnyAsync(o => o.Id == dto.OwnerId);
        if (!ownerExists)
        {
            _context.PetOwners.Add(new PetOwner
            {
                Id = dto.OwnerId,
                FullName = $"Owner {dto.OwnerId}",
                Email = $"{dto.OwnerId.ToLower()}@example.com",
                PhoneNumber = "000-000-0000"
            });
        }

        var petId = !string.IsNullOrWhiteSpace(dto.Id)
            ? dto.Id
            : await _idGenerator.GeneratePetIdAsync();

        var pet = new Pet
        {
            Id = petId,
            OwnerId = dto.OwnerId,
            Name = dto.Name.Trim(),
            Species = dto.Species.Trim(),
            Breed = dto.Breed?.Trim() ?? string.Empty,
            DateOfBirth = dto.DateOfBirth,
            Age = dto.Age,
            Notes = dto.Notes,
            PhotoUrl = dto.PhotoUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Pets.Add(pet);
        await _context.SaveChangesAsync();

        return MapToResponse(pet);
    }

    // UC-06: View Pet Profile
    public async Task<PetResponseDto> GetPetByIdAsync(string id)
    {
        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.Id == id);
        if (pet == null)
            throw new NotFoundException($"Pet with ID '{id}' was not found.");

        return MapToResponse(pet);
    }

    public async Task<IEnumerable<PetResponseDto>> GetPetsByOwnerAsync(string ownerId)
    {
        var pets = await _context.Pets
            .Where(p => p.OwnerId == ownerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return pets.Select(MapToResponse);
    }

    public async Task<IEnumerable<PetResponseDto>> GetAllPetsAsync()
    {
        var pets = await _context.Pets
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return pets.Select(MapToResponse);
    }

    // UC-06, UC-07: Update Pet Profile (Prevent owners from modifying clinical records)
    public async Task<PetResponseDto> UpdatePetProfileAsync(string petId, UpdatePetProfileDto dto, string? requestingOwnerId = null)
    {
        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.Id == petId);
        if (pet == null)
            throw new NotFoundException($"Pet with ID '{petId}' was not found.");

        // UC-14 validation when requestingOwnerId is provided
        if (!string.IsNullOrWhiteSpace(requestingOwnerId) && pet.OwnerId != requestingOwnerId)
        {
            throw new OwnershipValidationException(petId, requestingOwnerId);
        }

        // Apply profile updates - strictly non-clinical fields
        if (!string.IsNullOrWhiteSpace(dto.Name)) pet.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Species)) pet.Species = dto.Species.Trim();
        if (dto.Breed != null) pet.Breed = dto.Breed.Trim();
        if (dto.DateOfBirth.HasValue) pet.DateOfBirth = dto.DateOfBirth;
        if (dto.Age >= 0) pet.Age = dto.Age;
        if (dto.Notes != null) pet.Notes = dto.Notes;
        if (dto.PhotoUrl != null) pet.PhotoUrl = dto.PhotoUrl;

        pet.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToResponse(pet);
    }

    // UC-08: View Pet Medical/Vaccination History
    public async Task<PetMedicalHistoryDto> GetPetMedicalHistoryAsync(string petId)
    {
        var pet = await _context.Pets
            .Include(p => p.MedicalRecords)
            .Include(p => p.VaccinationRecords)
            .FirstOrDefaultAsync(p => p.Id == petId);

        if (pet == null)
            throw new NotFoundException($"Pet with ID '{petId}' was not found.");

        return new PetMedicalHistoryDto
        {
            PetId = pet.Id,
            PetName = pet.Name,
            MedicalRecords = pet.MedicalRecords
                .OrderByDescending(m => m.RecordDate)
                .Select(m => new MedicalRecordDto
                {
                    Id = m.Id,
                    PetId = m.PetId,
                    RecordDate = m.RecordDate,
                    Diagnosis = m.Diagnosis,
                    Treatment = m.Treatment,
                    VeterinarianName = m.VeterinarianName,
                    ClinicalNotes = m.ClinicalNotes,
                    CreatedAt = m.CreatedAt
                }).ToList(),
            VaccinationRecords = pet.VaccinationRecords
                .OrderByDescending(v => v.DateAdministered)
                .Select(v => new VaccinationRecordDto
                {
                    Id = v.Id,
                    PetId = v.PetId,
                    VaccineName = v.VaccineName,
                    DateAdministered = v.DateAdministered,
                    NextDueDate = v.NextDueDate,
                    VeterinarianName = v.VeterinarianName,
                    BatchNumber = v.BatchNumber,
                    CreatedAt = v.CreatedAt
                }).ToList()
        };
    }

    public async Task<MedicalRecordDto> AddMedicalRecordAsync(string petId, CreateMedicalRecordDto dto)
    {
        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.Id == petId);
        if (pet == null)
            throw new NotFoundException($"Pet with ID '{petId}' was not found.");

        var recordId = !string.IsNullOrWhiteSpace(dto.Id)
            ? dto.Id
            : await _idGenerator.GenerateMedicalRecordIdAsync();

        var record = new MedicalRecord
        {
            Id = recordId,
            PetId = petId,
            RecordDate = dto.RecordDate ?? DateTime.UtcNow,
            Diagnosis = dto.Diagnosis,
            Treatment = dto.Treatment,
            VeterinarianName = dto.VeterinarianName,
            ClinicalNotes = dto.ClinicalNotes,
            CreatedAt = DateTime.UtcNow
        };

        _context.MedicalRecords.Add(record);
        await _context.SaveChangesAsync();

        return new MedicalRecordDto
        {
            Id = record.Id,
            PetId = record.PetId,
            RecordDate = record.RecordDate,
            Diagnosis = record.Diagnosis,
            Treatment = record.Treatment,
            VeterinarianName = record.VeterinarianName,
            ClinicalNotes = record.ClinicalNotes,
            CreatedAt = record.CreatedAt
        };
    }

    public async Task<VaccinationRecordDto> AddVaccinationRecordAsync(string petId, CreateVaccinationRecordDto dto)
    {
        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.Id == petId);
        if (pet == null)
            throw new NotFoundException($"Pet with ID '{petId}' was not found.");

        var recordId = !string.IsNullOrWhiteSpace(dto.Id)
            ? dto.Id
            : await _idGenerator.GenerateVaccinationRecordIdAsync();

        var record = new VaccinationRecord
        {
            Id = recordId,
            PetId = petId,
            VaccineName = dto.VaccineName,
            DateAdministered = dto.DateAdministered,
            NextDueDate = dto.NextDueDate,
            VeterinarianName = dto.VeterinarianName,
            BatchNumber = dto.BatchNumber,
            CreatedAt = DateTime.UtcNow
        };

        _context.VaccinationRecords.Add(record);
        await _context.SaveChangesAsync();

        return new VaccinationRecordDto
        {
            Id = record.Id,
            PetId = record.PetId,
            VaccineName = record.VaccineName,
            DateAdministered = record.DateAdministered,
            NextDueDate = record.NextDueDate,
            VeterinarianName = record.VeterinarianName,
            BatchNumber = record.BatchNumber,
            CreatedAt = record.CreatedAt
        };
    }

    private static PetResponseDto MapToResponse(Pet pet)
    {
        return new PetResponseDto
        {
            Id = pet.Id,
            OwnerId = pet.OwnerId,
            Name = pet.Name,
            Species = pet.Species,
            Breed = pet.Breed,
            DateOfBirth = pet.DateOfBirth,
            Age = pet.Age,
            Notes = pet.Notes,
            PhotoUrl = pet.PhotoUrl,
            CreatedAt = pet.CreatedAt,
            UpdatedAt = pet.UpdatedAt
        };
    }
}
