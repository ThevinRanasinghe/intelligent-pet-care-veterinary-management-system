using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Api.DTOs;
using PetCare.Application.DTOs.Users;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

/// <summary>
/// ClinicManager Staff Management endpoints.
/// Manages Veterinarian and InventoryOfficer accounts strictly for the authenticated ClinicManager's organization.
/// </summary>
[ApiController]
[Route("api/organization/users")]
[Route("api/organization/staff")]
[Produces("application/json")]
[Authorize(Roles = $"{Roles.ClinicManager},{Roles.SuperAdmin}")]
public sealed class OrganizationUsersController : ControllerBase
{
    private readonly IStaffService                           _staffService;
    private readonly IValidator<CreateStaffUserRequestDto>   _createValidator;

    public OrganizationUsersController(
        IStaffService staffService,
        IValidator<CreateStaffUserRequestDto> createValidator)
    {
        _staffService    = staffService;
        _createValidator = createValidator;
    }

    /// <summary>
    /// READ ALL: Lists all staff members in the authenticated ClinicManager's organization.
    /// Supports search and filtering by role (Veterinarian, InventoryOfficer) and account status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StaffUserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var staff = await _staffService.GetOrganizationStaffAsync(search, role, status, ct);
        return Ok(ApiResponse<IEnumerable<StaffUserDto>>.Ok(staff));
    }

    /// <summary>
    /// READ ONE: Gets details of a staff member.
    /// Enforces multi-tenant isolation: ClinicManager can only view staff in their own organization.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<StaffUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        try
        {
            var staff = await _staffService.GetStaffMemberByIdAsync(id, ct);
            return Ok(ApiResponse<StaffUserDto>.Ok(staff));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// CREATE: Creates a new staff member (Veterinarian or InventoryOfficer) in the manager's organization.
    /// Client-supplied OrganizationId is strictly ignored — OrganizationId is automatically assigned from the JWT.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<StaffUserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStaffUserRequestDto request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationErrorResponse(
                false,
                "Validation failed.",
                validation.ToDictionary()));
        }

        var staff = await _staffService.CreateStaffMemberAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<StaffUserDto>.Ok(staff, $"{request.Role} account created successfully."));
    }

    /// <summary>
    /// UPDATE: Updates details of a staff member in the manager's organization.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<StaffUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateStaffUserRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var staff = await _staffService.UpdateStaffMemberAsync(id, request, ct);
            return Ok(ApiResponse<StaffUserDto>.Ok(staff, "Staff details updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// STATUS: Activates or disables a staff member's account.
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<StaffUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromBody] UpdateStaffStatusRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var staff = await _staffService.UpdateStaffStatusAsync(id, request, ct);
            return Ok(ApiResponse<StaffUserDto>.Ok(staff, $"Staff status updated to {request.Status}."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// RESET PASSWORD: Sets a new temporary password for a staff member and marks MustChangePassword = true.
    /// </summary>
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        string id,
        [FromBody] ResetStaffPasswordRequestDto request,
        CancellationToken ct)
    {
        try
        {
            await _staffService.ResetStaffPasswordAsync(id, request, ct);
            return Ok(ApiResponse.Ok("Temporary password set successfully. Staff must change it on next login."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// VERIFY: Approves and activates a staff member's account.
    /// Can be performed by either the ClinicManager or the SuperAdmin.
    /// </summary>
    [HttpPost("{id}/verify")]
    [ProducesResponseType(typeof(ApiResponse<StaffUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyStaff(string id, CancellationToken ct)
    {
        try
        {
            var staff = await _staffService.VerifyStaffMemberAsync(id, ct);
            return Ok(ApiResponse<StaffUserDto>.Ok(staff, "Staff account verified and activated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }
}
