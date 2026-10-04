using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Pets;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/pets")]
[Authorize]
public class PetsController : ControllerBase
{
    // Pet visibility: the owning PetOwner (scoped inside each action) plus
    // clinical staff and management. Inventory Officer has no pet-domain
    // responsibility.
    private const string ReadRoles =
        $"{Roles.PetOwner},{Roles.Veterinarian},{Roles.ClinicManager},{Roles.SuperAdmin}";

    // Pet records are managed by their owner or the clinic's management.
    private const string ManageRoles =
        $"{Roles.PetOwner},{Roles.ClinicManager},{Roles.SuperAdmin}";

    private readonly IPetService _petService;
    private readonly IOwnerAccessService _ownerAccess;

    public PetsController(IPetService petService, IOwnerAccessService ownerAccess)
    {
        _petService = petService;
        _ownerAccess = ownerAccess;
    }

    // POST: api/pets
    [HttpPost]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<PetDto>> Create(
        [FromBody] CreatePetDto dto)
    {
        if (_ownerAccess.IsPetOwner)
        {
            var ownerId = await _ownerAccess.GetOwnerIdAsync();
            if (ownerId == null)
            {
                return Forbid();
            }

            // A pet owner can only register pets under their own profile.
            dto.OwnerId = ownerId;
        }

        try
        {
            var pet = await _petService.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = pet.Id },
                pet);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // GET: api/pets?includeArchived=true
    // Active pets by default; archived pets only when explicitly asked.
    [HttpGet]
    [Authorize(Roles = ReadRoles)]
    public async Task<ActionResult<List<PetDto>>> GetAll(
        [FromQuery] bool includeArchived = false)
    {
        if (_ownerAccess.IsPetOwner)
        {
            var ownerId = await _ownerAccess.GetOwnerIdAsync();
            var ownPets = ownerId == null
                ? new List<PetDto>()
                : await _petService.GetByOwnerIdAsync(
                    ownerId, includeArchived);

            return Ok(ownPets);
        }

        var pets = await _petService.GetAllAsync(includeArchived);

        return Ok(pets);
    }

    // GET: api/pets/{id}
    [HttpGet("{id}")]
    [Authorize(Roles = ReadRoles)]
    public async Task<ActionResult<PetDto>> GetById(string id)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsPetAsync(id))
        {
            return NotFound(new
            {
                message = "Pet not found."
            });
        }

        var pet = await _petService.GetByIdAsync(id);

        if (pet == null)
        {
            return NotFound(new
            {
                message = "Pet not found."
            });
        }

        return Ok(pet);
    }

    // GET: api/pets/owner/{ownerId}?includeArchived=true
    [HttpGet("owner/{ownerId}")]
    [Authorize(Roles = ReadRoles)]
    public async Task<ActionResult<List<PetDto>>> GetByOwner(
        string ownerId,
        [FromQuery] bool includeArchived = false)
    {
        if (_ownerAccess.IsPetOwner && ownerId != await _ownerAccess.GetOwnerIdAsync())
        {
            return Forbid();
        }

        var pets = await _petService.GetByOwnerIdAsync(
            ownerId, includeArchived);

        return Ok(pets);
    }

    // PUT: api/pets/{id}
    [HttpPut("{id}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<PetDto>> Update(
        string id,
        [FromBody] UpdatePetDto dto)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsPetAsync(id))
        {
            return NotFound(new
            {
                message = "Pet not found."
            });
        }

        try
        {
            var pet = await _petService.UpdateAsync(id, dto);

            if (pet == null)
            {
                return NotFound(new
                {
                    message = "Pet not found."
                });
            }

            return Ok(pet);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // POST: api/pets/{id}/archive
    // Removes the pet from the owner's active list without deleting
    // any medical, consultation, appointment, or billing history.
    [HttpPost("{id}/archive")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<PetDto>> Archive(string id)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsPetAsync(id))
        {
            return NotFound(new
            {
                message = "Pet not found."
            });
        }

        try
        {
            var pet = await _petService.ArchiveAsync(id);

            if (pet == null)
            {
                return NotFound(new
                {
                    message = "Pet not found."
                });
            }

            return Ok(pet);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // POST: api/pets/{id}/restore
    // Returns an archived pet to the owner's active list.
    [HttpPost("{id}/restore")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<PetDto>> Restore(string id)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsPetAsync(id))
        {
            return NotFound(new
            {
                message = "Pet not found."
            });
        }

        try
        {
            var pet = await _petService.RestoreAsync(id);

            if (pet == null)
            {
                return NotFound(new
                {
                    message = "Pet not found."
                });
            }

            return Ok(pet);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // DELETE: api/pets/{id}
    // Permanent delete — only allowed when the pet has no protected
    // history; otherwise archive it instead.
    [HttpDelete("{id}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> Delete(string id)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsPetAsync(id))
        {
            return NotFound(new
            {
                message = "Pet not found."
            });
        }

        try
        {
            var deleted = await _petService.DeleteAsync(id);

            if (!deleted)
            {
                return NotFound(new
                {
                    message = "Pet not found."
                });
            }

            return Ok(new
            {
                message = "Pet deleted successfully."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}
