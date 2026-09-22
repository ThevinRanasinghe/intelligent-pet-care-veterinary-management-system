using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.PetOwners;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/petowners")]
[Route("api/owners")]
[Authorize]
public class PetOwnersController : ControllerBase
{
    private readonly IPetOwnerService _petOwnerService;
    private readonly IOwnerAccessService _ownerAccess;

    public PetOwnersController(IPetOwnerService petOwnerService, IOwnerAccessService ownerAccess)
    {
        _petOwnerService = petOwnerService;
        _ownerAccess = ownerAccess;
    }

    // POST: api/petowners
    [HttpPost]
    public async Task<ActionResult<PetOwnerDto>> Create(
        [FromBody] CreatePetOwnerDto dto)
    {
        // A pet owner may only create an owner profile for their own account.
        if (_ownerAccess.IsPetOwner &&
            !string.Equals(dto.Email, _ownerAccess.CallerEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        try
        {
            var owner = await _petOwnerService.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = owner.Id },
                owner);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // GET: api/petowners
    [HttpGet]
    public async Task<ActionResult<List<PetOwnerDto>>> GetAll()
    {
        if (_ownerAccess.IsPetOwner)
        {
            var ownerId = await _ownerAccess.GetOwnerIdAsync();
            var own = ownerId == null ? null : await _petOwnerService.GetByIdAsync(ownerId);

            return Ok(own == null ? new List<PetOwnerDto>() : new List<PetOwnerDto> { own });
        }

        var owners = await _petOwnerService.GetAllAsync();

        return Ok(owners);
    }

    // GET: api/petowners/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<PetOwnerDto>> GetById(string id)
    {
        if (_ownerAccess.IsPetOwner && id != await _ownerAccess.GetOwnerIdAsync())
        {
            return NotFound(new
            {
                message = "Pet owner not found."
            });
        }

        var owner = await _petOwnerService.GetByIdAsync(id);

        if (owner == null)
        {
            return NotFound(new
            {
                message = "Pet owner not found."
            });
        }

        return Ok(owner);
    }
}
