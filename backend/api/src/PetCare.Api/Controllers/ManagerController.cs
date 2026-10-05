using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Manager;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

/// <summary>
/// ClinicManager self-administration endpoints. Every action is scoped to
/// the caller's own organization, resolved from the JWT identity via
/// ITenantContext — no client-supplied organization id is ever used.
/// </summary>
[ApiController]
[Route("api/manager")]
[Authorize(Roles = Roles.ClinicManager)]
public class ManagerController : ControllerBase
{
    private readonly IManagerService _managerService;

    public ManagerController(IManagerService managerService)
    {
        _managerService = managerService;
    }

    // GET: api/manager/veterinarians
    /// <summary>Lists the active veterinarians in the caller's organization.</summary>
    [HttpGet("veterinarians")]
    [ProducesResponseType(typeof(IReadOnlyList<ManagerVeterinarianResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ManagerVeterinarianResponse>>> GetVeterinarians(
        CancellationToken cancellationToken)
    {
        var veterinarians = await _managerService.GetVeterinariansAsync(cancellationToken);
        return Ok(veterinarians);
    }

    // GET: api/manager/veterinarians/{veterinarianId}/history
    /// <summary>
    /// Work history for a veterinarian in the caller's organization:
    /// appointments, examinations, prescriptions/medicine requests, and bills.
    /// </summary>
    [HttpGet("veterinarians/{veterinarianId:guid}/history")]
    [ProducesResponseType(typeof(VeterinarianHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VeterinarianHistoryResponse>> GetVeterinarianHistory(
        Guid veterinarianId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var history = await _managerService.GetVeterinarianHistoryAsync(
            veterinarianId, from, to, cancellationToken);
        return Ok(history);
    }

    // POST: api/manager/users/veterinarians
    /// <summary>
    /// Creates a Veterinarian account inside the caller's organization.
    /// Role and OrganizationId are fixed server-side; the response contains
    /// a one-time temporary password. Requires MustChangePassword on first
    /// login.
    /// </summary>
    [HttpPost("users/veterinarians")]
    public async Task<ActionResult<CreatedStaffAccountResponse>> CreateVeterinarian(
        [FromBody] ManagerCreateStaffRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _managerService.CreateVeterinarianAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST: api/manager/users/inventory-officers
    /// <summary>Same as CreateVeterinarian but assigns the InventoryOfficer role.</summary>
    [HttpPost("users/inventory-officers")]
    public async Task<ActionResult<CreatedStaffAccountResponse>> CreateInventoryOfficer(
        [FromBody] ManagerCreateStaffRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _managerService.CreateInventoryOfficerAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
