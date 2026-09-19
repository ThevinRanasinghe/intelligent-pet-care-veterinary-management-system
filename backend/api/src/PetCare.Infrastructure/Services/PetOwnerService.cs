using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.PetOwners;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Services;

public class PetOwnerService : IPetOwnerService
{
    private readonly IPetCareDbContext _context;

    public PetOwnerService(IPetCareDbContext context)
    {
        _context = context;
    }

    public async Task<PetOwnerDto> CreateAsync(CreatePetOwnerDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new ArgumentException("Full name is required.");

        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email is required.");

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            throw new ArgumentException("Phone number is required.");

        var owner = new PetOwner
        {
            Id = $"OWN-{Guid.NewGuid():N}".Substring(0, 12).ToUpper(),
            FullName = dto.FullName.Trim(),
            Email = dto.Email.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            Address = string.IsNullOrWhiteSpace(dto.Address)
                ? null
                : dto.Address.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.PetOwners.Add(owner);

        await _context.SaveChangesAsync();

        return MapToDto(owner);
    }

    public async Task<List<PetOwnerDto>> GetAllAsync()
    {
        return await _context.PetOwners
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new PetOwnerDto
            {
                Id = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                PhoneNumber = x.PhoneNumber,
                Address = x.Address
            })
            .ToListAsync();
    }

    public async Task<PetOwnerDto?> GetByIdAsync(string id)
    {
        var owner = await _context.PetOwners
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (owner == null)
            return null;

        return MapToDto(owner);
    }

    private static PetOwnerDto MapToDto(PetOwner owner)
    {
        return new PetOwnerDto
        {
            Id = owner.Id,
            FullName = owner.FullName,
            Email = owner.Email,
            PhoneNumber = owner.PhoneNumber,
            Address = owner.Address
        };
    }
}