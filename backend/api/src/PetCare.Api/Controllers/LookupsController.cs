using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Entities;
using PetCare.Infrastructure;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LookupsController : ControllerBase
{
    private readonly PetCareDbContext _context;

    public LookupsController(PetCareDbContext context)
    {
        _context = context;
    }

    [HttpGet("pets")]
    public async Task<ActionResult<IEnumerable<object>>> GetPets()
    {
        var pets = await _context.Pets
            .AsNoTracking()
            .Select(p => new { p.Id, p.Name, p.Species, p.Breed })
            .ToListAsync();
        return Ok(pets);
    }

    [HttpGet("medicines")]
    public async Task<ActionResult<IEnumerable<Medicine>>> GetMedicines()
    {
        var medicines = await _context.Medicines.AsNoTracking().ToListAsync();
        return Ok(medicines);
    }
}
