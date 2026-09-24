using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Infrastructure;

namespace PetCare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LookupsController : ControllerBase
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public LookupsController(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    // Pets are owner-domain records (not org-owned); the pet lookup exists
    // for clinical staff forms, so InventoryOfficer is excluded.
    [Authorize(Roles = $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.SuperAdmin}")]
    [HttpGet("pets")]
    public async Task<ActionResult<IEnumerable<object>>> GetPets()
    {
        var pets = await _context.Pets
            .AsNoTracking()
            .Select(p => new { p.Id, p.Name, p.Species, p.Breed })
            .ToListAsync();
        return Ok(pets);
    }

    [Authorize(Roles = $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpGet("medicines")]
    public async Task<ActionResult<IEnumerable<Medicine>>> GetMedicines()
    {
        var query = _context.Medicines.AsNoTracking().AsQueryable();

        // Inventory lookup is organization-scoped for staff callers.
        if (_tenant.IsOrganizationScoped)
        {
            var orgId = await _tenant.GetOrganizationIdAsync();
            query = query.Where(m => m.OrganizationId == orgId);
        }

        var medicines = await query.ToListAsync();
        return Ok(medicines);
    }
}
