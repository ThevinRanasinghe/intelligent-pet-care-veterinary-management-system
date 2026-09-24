using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Admin;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

/// <summary>
/// Platform administration endpoints. Restricted to the Administrator
/// (SuperAdmin) role — these manage users, organizations, and expose
/// system-level information.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.SuperAdmin)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IWebHostEnvironment _environment;

    public AdminController(IAdminService adminService, IWebHostEnvironment environment)
    {
        _adminService = adminService;
        _environment = environment;
    }

    // GET: api/admin/users
    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        return Ok(await _adminService.GetUsersAsync(cancellationToken));
    }

    // PATCH: api/admin/users/{id}/status
    [HttpPatch("users/{id:guid}/status")]
    public async Task<ActionResult<AdminUserResponse>> SetUserStatus(
        Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var callerEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        try
        {
            return Ok(await _adminService.SetUserActiveAsync(id, request.Active, callerEmail, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST: api/admin/users/veterinarians
    /// <summary>
    /// Creates a Veterinarian account inside the selected organization.
    /// The role is fixed server-side; only Active organizations are
    /// selectable and the account requires a password change on first login.
    /// The response contains a one-time temporary password.
    /// </summary>
    [HttpPost("users/veterinarians")]
    public async Task<ActionResult<CreateStaffUserResponse>> CreateVeterinarian(
        [FromBody] CreateStaffUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _adminService.CreateVeterinarianAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetUsers), new { id = created.Id }, created);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST: api/admin/users/inventory-officers
    /// <summary>Same as CreateVeterinarian but assigns the InventoryOfficer role.</summary>
    [HttpPost("users/inventory-officers")]
    public async Task<ActionResult<CreateStaffUserResponse>> CreateInventoryOfficer(
        [FromBody] CreateStaffUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _adminService.CreateInventoryOfficerAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetUsers), new { id = created.Id }, created);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // GET: api/admin/organizations
    [HttpGet("organizations")]
    public async Task<ActionResult<List<AdminOrganizationResponse>>> GetOrganizations(CancellationToken cancellationToken)
    {
        return Ok(await _adminService.GetOrganizationsAsync(cancellationToken));
    }

    // PATCH: api/admin/organizations/{id}/status
    [HttpPatch("organizations/{id:guid}/status")]
    public async Task<ActionResult<AdminOrganizationResponse>> SetOrganizationStatus(
        Guid id, [FromBody] UpdateOrganizationStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _adminService.SetOrganizationStatusAsync(id, request.Status, request.Reason, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // GET: api/admin/roles
    [HttpGet("roles")]
    public ActionResult<IReadOnlyList<string>> GetRoles()
    {
        return Ok(_adminService.GetRoles());
    }

    // GET: api/admin/system
    [HttpGet("system")]
    public async Task<ActionResult<AdminSystemInfoResponse>> GetSystemInfo(CancellationToken cancellationToken)
    {
        var stats = await _adminService.GetSystemStatsAsync(cancellationToken);
        return Ok(stats with
        {
            Environment = _environment.EnvironmentName,
            DatabaseProvider = "PostgreSQL",
        });
    }
}
