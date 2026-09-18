using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LookupsController : ControllerBase
{
    private readonly AppDbContext _context;

    public LookupsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("pets")]
    public async Task<ActionResult<IEnumerable<Pet>>> GetPets()
    {
        var pets = await _context.Pets.AsNoTracking().ToListAsync();
        return Ok(pets);
    }

    [HttpGet("medicines")]
    public async Task<ActionResult<IEnumerable<Medicine>>> GetMedicines()
    {
        var medicines = await _context.Medicines.AsNoTracking().ToListAsync();
        return Ok(medicines);
    }
}
