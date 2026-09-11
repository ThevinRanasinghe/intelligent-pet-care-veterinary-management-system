using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Api.DTOs;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

/// <summary>
/// Veterinarian schedule and slot management endpoints.
/// Strictly scoped to the authenticated user's organization.
/// </summary>
[ApiController]
[Route("api/schedules")]
[Produces("application/json")]
[Authorize(Roles = $"{Roles.ClinicManager},{Roles.SuperAdmin}")]
public sealed class VeterinarianSchedulesController : ControllerBase
{
    private readonly ISchedulingService _schedulingService;
    private readonly ICurrentUserService _currentUser;

    public VeterinarianSchedulesController(
        ISchedulingService schedulingService,
        ICurrentUserService currentUser)
    {
        _schedulingService = schedulingService;
        _currentUser = currentUser;
    }

    private Guid? GetOrganizationId() => _currentUser.OrganizationId;

    /// <summary>
    /// READ VETERINARIANS: Lists all veterinarians in the organization.
    /// </summary>
    [HttpGet("veterinarians")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<VeterinarianDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVeterinarians(CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var vets = await _schedulingService.GetOrganizationVeterinariansAsync(orgId.Value, ct);
        return Ok(ApiResponse<IReadOnlyList<VeterinarianDto>>.Ok(vets));
    }

    /// <summary>
    /// READ SLOTS: Gets all appointment slots for a veterinarian or clinic on a given date.
    /// </summary>
    [HttpGet("slots")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppointmentSlotResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlots(
        [FromQuery] Guid? veterinarianId,
        [FromQuery] DateOnly? date,
        CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var slots = await _schedulingService.GetSlotsAsync(orgId.Value, veterinarianId, date, ct);
        return Ok(ApiResponse<IReadOnlyList<AppointmentSlotResponseDto>>.Ok(slots));
    }

    /// <summary>
    /// READ AVAILABLE SLOTS: Gets open/available appointment slots.
    /// </summary>
    [HttpGet("slots/available")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppointmentSlotResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableSlots(
        [FromQuery] Guid? veterinarianId,
        [FromQuery] DateOnly? date,
        CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var slots = await _schedulingService.GetAvailableSlotsAsync(orgId.Value, veterinarianId, date, ct);
        return Ok(ApiResponse<IReadOnlyList<AppointmentSlotResponseDto>>.Ok(slots));
    }

    /// <summary>
    /// CREATE SLOT: Adds a new schedule slot for a veterinarian.
    /// </summary>
    [HttpPost("slots")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentSlotResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSlot([FromBody] CreateAppointmentSlotDto dto, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var slot = await _schedulingService.CreateSlotAsync(orgId.Value, dto, ct);
            return Ok(ApiResponse<AppointmentSlotResponseDto>.Ok(slot, "Slot created successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// UPDATE SLOT: Updates an existing appointment slot.
    /// </summary>
    [HttpPut("slots/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentSlotResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSlot(Guid id, [FromBody] UpdateAppointmentSlotDto dto, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var slot = await _schedulingService.UpdateSlotAsync(id, orgId.Value, dto, ct);
            return Ok(ApiResponse<AppointmentSlotResponseDto>.Ok(slot, "Slot updated successfully."));
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
    /// DELETE SLOT: Deletes an open appointment slot.
    /// </summary>
    [HttpDelete("slots/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSlot(Guid id, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            await _schedulingService.DeleteSlotAsync(id, orgId.Value, ct);
            return Ok(ApiResponse.Ok("Slot deleted successfully."));
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
}
