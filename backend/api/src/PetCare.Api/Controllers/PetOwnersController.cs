using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Exceptions;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/owners")]
public class PetOwnersController : ControllerBase
{
    private readonly IPetCareDbContext _context;
    private readonly IIdGenerator _idGenerator;

    public PetOwnersController(IPetCareDbContext context, IIdGenerator idGenerator)
    {
        _context = context;
        _idGenerator = idGenerator;
    }

    [HttpPost]
    public async Task<ActionResult<PetOwnerDto>> CreateOwner([FromBody] CreatePetOwnerDto dto)
    {
        var ownerId = !string.IsNullOrWhiteSpace(dto.Id)
            ? dto.Id
            : await _idGenerator.GenerateOwnerIdAsync();

        var owner = new PetOwner
        {
            Id = ownerId,
            FullName = dto.FullName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            Address = dto.Address,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.PetOwners.Add(owner);
        await _context.SaveChangesAsync();

        var response = new PetOwnerDto
        {
            Id = owner.Id,
            FullName = owner.FullName,
            Email = owner.Email,
            PhoneNumber = owner.PhoneNumber,
            Address = owner.Address,
            CreatedAt = owner.CreatedAt
        };

        return CreatedAtAction(nameof(GetOwnerById), new { id = owner.Id }, response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PetOwnerDto>> GetOwnerById(string id)
    {
        var owner = await _context.PetOwners.FirstOrDefaultAsync(o => o.Id == id);
        if (owner == null)
            throw new NotFoundException($"Owner with ID '{id}' was not found.");

        return Ok(new PetOwnerDto
        {
            Id = owner.Id,
            FullName = owner.FullName,
            Email = owner.Email,
            PhoneNumber = owner.PhoneNumber,
            Address = owner.Address,
            CreatedAt = owner.CreatedAt
        });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PetOwnerDto>>> GetAllOwners()
    {
        var owners = await _context.PetOwners
            .OrderBy(o => o.FullName)
            .Select(o => new PetOwnerDto
            {
                Id = o.Id,
                FullName = o.FullName,
                Email = o.Email,
                PhoneNumber = o.PhoneNumber,
                Address = o.Address,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync();

        return Ok(owners);
    }
}
