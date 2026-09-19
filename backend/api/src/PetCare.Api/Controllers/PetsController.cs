using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Pets;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/pets")]
public class PetsController : ControllerBase
{
    private readonly IPetService _petService;

    public PetsController(IPetService petService)
    {
        _petService = petService;
    }

    // POST: api/pets
    [HttpPost]
    public async Task<ActionResult<PetDto>> Create(
        [FromBody] CreatePetDto dto)
    {
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

    // GET: api/pets
    [HttpGet]
    public async Task<ActionResult<List<PetDto>>> GetAll()
    {
        var pets = await _petService.GetAllAsync();

        return Ok(pets);
    }

    // GET: api/pets/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<PetDto>> GetById(string id)
    {
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

    // GET: api/pets/owner/{ownerId}
    [HttpGet("owner/{ownerId}")]
    public async Task<ActionResult<List<PetDto>>> GetByOwner(
        string ownerId)
    {
        var pets = await _petService.GetByOwnerIdAsync(ownerId);

        return Ok(pets);
    }

    // PUT: api/pets/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<PetDto>> Update(
        string id,
        [FromBody] UpdatePetDto dto)
    {
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

    // DELETE: api/pets/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
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