using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.PetOwners;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/petowners")]
[Route("api/owners")]
[Authorize]
public class PetOwnersController : ControllerBase
{
    // Owner-profile visibility: the owner themselves (scoped inside each
    // action) plus clinical staff and management.
    private const string ReadRoles =
        $"{Roles.PetOwner},{Roles.Veterinarian},{Roles.ClinicManager},{Roles.SuperAdmin}";

    // Profile creation: self-service for PetOwner accounts, or clinic
    // management registering an owner profile.
    private const string CreateRoles =
        $"{Roles.PetOwner},{Roles.ClinicManager},{Roles.SuperAdmin}";

    private readonly IPetOwnerService _petOwnerService;
    private readonly IOwnerAccessService _ownerAccess;

    public PetOwnersController(IPetOwnerService petOwnerService, IOwnerAccessService ownerAccess)
    {
        _petOwnerService = petOwnerService;
        _ownerAccess = ownerAccess;
    }

    // POST: api/petowners
    [HttpPost]
    [Authorize(Roles = CreateRoles)]
    public async Task<ActionResult<PetOwnerDto>> Create(
        [FromBody] CreatePetOwnerDto dto)
    {
        Guid? userId = null;

        if (_ownerAccess.IsPetOwner)
        {
            // A pet owner may only create an owner profile for their own
            // account: the email must match the account email, the profile
            // is linked via PetOwner.UserId, and an account can only ever
            // have one owner profile.
            if (!string.Equals(dto.Email, _ownerAccess.CallerEmail, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if (await _ownerAccess.GetOwnerIdAsync() != null)
            {
                return Conflict(new
                {
                    message = "This account already has a pet owner profile."
                });
            }

            userId = _ownerAccess.CallerUserId;
        }

        try
        {
            var owner = await _petOwnerService.CreateAsync(dto, userId);

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
    [Authorize(Roles = ReadRoles)]
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
    [Authorize(Roles = ReadRoles)]
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
