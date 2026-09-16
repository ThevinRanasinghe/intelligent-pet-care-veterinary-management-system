using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Pets;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Services;

public class PetService : IPetService
{
    private readonly IPetCareDbContext _context;

    public PetService(IPetCareDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // CREATE PET
    // ============================================================
    public async Task<PetDto> CreateAsync(CreatePetDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.OwnerId))
            throw new ArgumentException("Owner ID is required.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Pet name is required.");

        if (string.IsNullOrWhiteSpace(dto.Species))
            throw new ArgumentException("Species is required.");

        // Check whether the owner exists
        var ownerExists = await _context.PetOwners
            .AnyAsync(x => x.Id == dto.OwnerId.Trim());

        if (!ownerExists)
            throw new ArgumentException("Pet owner was not found.");

        // Validate weight
        if (dto.Weight.HasValue && dto.Weight.Value <= 0)
            throw new ArgumentException("Weight must be greater than zero.");

        var pet = new Pet
        {
            Id = $"PET-{Guid.NewGuid():N}".Substring(0, 12).ToUpper(),

            OwnerId = dto.OwnerId.Trim(),

            Name = dto.Name.Trim(),

            Species = dto.Species.Trim(),

            Breed = string.IsNullOrWhiteSpace(dto.Breed)
                ? null
                : dto.Breed.Trim(),

            Gender = string.IsNullOrWhiteSpace(dto.Gender)
                ? null
                : dto.Gender.Trim(),

            // PostgreSQL timestamp with time zone requires UTC
            DateOfBirth = dto.DateOfBirth.HasValue
                ? DateTime.SpecifyKind(
                    dto.DateOfBirth.Value,
                    DateTimeKind.Utc)
                : null,

            Weight = dto.Weight,

            PhotoUrl = string.IsNullOrWhiteSpace(dto.PhotoUrl)
                ? null
                : dto.PhotoUrl.Trim(),

            Notes = string.IsNullOrWhiteSpace(dto.Notes)
                ? null
                : dto.Notes.Trim(),

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow
        };

        _context.Pets.Add(pet);

        await _context.SaveChangesAsync();

        return MapToDto(pet);
    }


    // ============================================================
    // GET ALL PETS
    // ============================================================
    public async Task<List<PetDto>> GetAllAsync()
    {
        return await _context.Pets
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new PetDto
            {
                Id = x.Id,
                OwnerId = x.OwnerId,
                Name = x.Name,
                Species = x.Species,
                Breed = x.Breed,
                Gender = x.Gender,
                DateOfBirth = x.DateOfBirth,
                Weight = x.Weight,
                PhotoUrl = x.PhotoUrl,
                Notes = x.Notes
            })
            .ToListAsync();
    }


    // ============================================================
    // GET PET BY ID
    // ============================================================
    public async Task<PetDto?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var pet = await _context.Pets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        if (pet == null)
            return null;

        return MapToDto(pet);
    }


    // ============================================================
    // GET PETS BY OWNER ID
    // ============================================================
    public async Task<List<PetDto>> GetByOwnerIdAsync(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return new List<PetDto>();

        ownerId = ownerId.Trim();

        return await _context.Pets
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId)
            .OrderBy(x => x.Name)
            .Select(x => new PetDto
            {
                Id = x.Id,
                OwnerId = x.OwnerId,
                Name = x.Name,
                Species = x.Species,
                Breed = x.Breed,
                Gender = x.Gender,
                DateOfBirth = x.DateOfBirth,
                Weight = x.Weight,
                PhotoUrl = x.PhotoUrl,
                Notes = x.Notes
            })
            .ToListAsync();
    }


    // ============================================================
    // UPDATE PET
    // ============================================================
    public async Task<PetDto?> UpdateAsync(
        string id,
        UpdatePetDto dto)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Pet name is required.");

        if (string.IsNullOrWhiteSpace(dto.Species))
            throw new ArgumentException("Species is required.");

        // Validate weight
        if (dto.Weight.HasValue && dto.Weight.Value <= 0)
            throw new ArgumentException("Weight must be greater than zero.");

        id = id.Trim();

        var pet = await _context.Pets
            .FirstOrDefaultAsync(x => x.Id == id);

        if (pet == null)
            return null;

        pet.Name = dto.Name.Trim();

        pet.Species = dto.Species.Trim();

        pet.Breed = string.IsNullOrWhiteSpace(dto.Breed)
            ? null
            : dto.Breed.Trim();

        pet.Gender = string.IsNullOrWhiteSpace(dto.Gender)
            ? null
            : dto.Gender.Trim();

        // PostgreSQL timestamp with time zone requires UTC
        pet.DateOfBirth = dto.DateOfBirth.HasValue
            ? DateTime.SpecifyKind(
                dto.DateOfBirth.Value,
                DateTimeKind.Utc)
            : null;

        pet.Weight = dto.Weight;

        pet.PhotoUrl = string.IsNullOrWhiteSpace(dto.PhotoUrl)
            ? null
            : dto.PhotoUrl.Trim();

        pet.Notes = string.IsNullOrWhiteSpace(dto.Notes)
            ? null
            : dto.Notes.Trim();

        pet.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(pet);
    }


    // ============================================================
    // DELETE PET
    // ============================================================
    public async Task<bool> DeleteAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        id = id.Trim();

        var pet = await _context.Pets
            .FirstOrDefaultAsync(x => x.Id == id);

        if (pet == null)
            return false;

        // Do not delete a pet if it already has
        // consultation history.
        var hasConsultations = await _context.ConsultationRequests
            .AnyAsync(x => x.PetId == id);

        if (hasConsultations)
        {
            throw new InvalidOperationException(
                "This pet cannot be deleted because it has consultation history.");
        }

        _context.Pets.Remove(pet);

        await _context.SaveChangesAsync();

        return true;
    }


    // ============================================================
    // MAP ENTITY TO DTO
    // ============================================================
    private static PetDto MapToDto(Pet pet)
    {
        return new PetDto
        {
            Id = pet.Id,
            OwnerId = pet.OwnerId,
            Name = pet.Name,
            Species = pet.Species,
            Breed = pet.Breed,
            Gender = pet.Gender,
            DateOfBirth = pet.DateOfBirth,
            Weight = pet.Weight,
            PhotoUrl = pet.PhotoUrl,
            Notes = pet.Notes
        };
    }
}