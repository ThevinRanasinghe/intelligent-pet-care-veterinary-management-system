using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.PetOwners;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/petowners")]
[Route("api/owners")]
public class PetOwnersController : ControllerBase
{
    private readonly IPetOwnerService _petOwnerService;

    public PetOwnersController(IPetOwnerService petOwnerService)
    {
        _petOwnerService = petOwnerService;
    }

    // POST: api/petowners
    [HttpPost]
    public async Task<ActionResult<PetOwnerDto>> Create(
        [FromBody] CreatePetOwnerDto dto)
    {
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
        var owners = await _petOwnerService.GetAllAsync();

        return Ok(owners);
    }

    // GET: api/petowners/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<PetOwnerDto>> GetById(string id)
    {
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