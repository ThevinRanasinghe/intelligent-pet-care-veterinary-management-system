using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Data;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PetsController : ControllerBase
{
    private readonly PetCareDbContext _context;

    public PetsController(PetCareDbContext context)
    {
        _context = context;
    }

    // Endpoint 1: Register a new Pet
    [HttpPost]
    public async Task<ActionResult<PetResponseDto>> RegisterPet([FromBody] CreatePetDto dto)
    {
        var pet = new Pet
        {
            OwnerId = dto.OwnerId,
            Name = dto.Name,
            Species = dto.Species,
            Breed = dto.Breed,
            Age = dto.Age,
            MedicalHistorySummary = dto.MedicalHistorySummary
        };

        _context.Pets.Add(pet);
        await _context.SaveChangesAsync();

        var response = new PetResponseDto
        {
            Id = pet.Id,
            OwnerId = pet.OwnerId,
            Name = pet.Name,
            Species = pet.Species,
            Breed = pet.Breed,
            Age = pet.Age,
            MedicalHistorySummary = pet.MedicalHistorySummary,
            CreatedAt = pet.CreatedAt
        };

        return CreatedAtAction(nameof(GetPetsByOwner), new { ownerId = pet.OwnerId }, response);
    }

    // Endpoint 2: Get all Pets for a specific Owner
    [HttpGet("owner/{ownerId}")]
    public async Task<ActionResult<IEnumerable<PetResponseDto>>> GetPetsByOwner(Guid ownerId)
    {
        var pets = await _context.Pets
            .Where(p => p.OwnerId == ownerId)
            .Select(p => new PetResponseDto
            {
                Id = p.Id,
                OwnerId = p.OwnerId,
                Name = p.Name,
                Species = p.Species,
                Breed = p.Breed,
                Age = p.Age,
                MedicalHistorySummary = p.MedicalHistorySummary,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(pets);
    }
}