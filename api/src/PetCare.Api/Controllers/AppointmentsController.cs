using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Api.DTOs;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Enums;

namespace PetCare.Api.Controllers;

/// <summary>
/// Appointment management endpoints for Clinic Managers.
/// Strictly scoped to the authenticated ClinicManager's organization.
/// </summary>
[ApiController]
[Route("api/appointments")]
[Produces("application/json")]
[Authorize(Roles = $"{Roles.ClinicManager},{Roles.SuperAdmin}")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly ISchedulingService _schedulingService;
    private readonly ICurrentUserService _currentUser;

    public AppointmentsController(
        ISchedulingService schedulingService,
        ICurrentUserService currentUser)
    {
        _schedulingService = schedulingService;
        _currentUser = currentUser;
    }

    private Guid? GetOrganizationId() => _currentUser.OrganizationId;

    /// <summary>
    /// READ ALL: Lists appointments for the clinic with search and status filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppointmentResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] AppointmentStatus? status,
        [FromQuery] DateOnly? date,
        CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var appointments = await _schedulingService.GetAppointmentsAsync(orgId.Value, search, status, date, ct);
        return Ok(ApiResponse<IReadOnlyList<AppointmentResponseDto>>.Ok(appointments));
    }

    /// <summary>
    /// READ ONE: Gets appointment details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var appointment = await _schedulingService.GetAppointmentByIdAsync(id, orgId.Value, ct);
        if (appointment is null)
            return NotFound(ApiResponse.Fail("Appointment not found."));

        return Ok(ApiResponse<AppointmentResponseDto>.Ok(appointment));
    }

    /// <summary>
    /// CREATE: Books a new appointment with real-time schedule conflict prevention.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AppointmentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentDto dto, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var appointment = await _schedulingService.CreateAppointmentAsync(orgId.Value, dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, ApiResponse<AppointmentResponseDto>.Ok(appointment, "Appointment created successfully."));
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
    /// UPDATE: Reschedules or updates an existing appointment.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppointmentDto dto, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var appointment = await _schedulingService.UpdateAppointmentAsync(id, orgId.Value, dto, ct);
            return Ok(ApiResponse<AppointmentResponseDto>.Ok(appointment, "Appointment updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
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
    /// STATUS UPDATE: Updates the status of an appointment.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAppointmentStatusDto dto, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var appointment = await _schedulingService.UpdateAppointmentStatusAsync(id, orgId.Value, dto.Status, ct);
            return Ok(ApiResponse<AppointmentResponseDto>.Ok(appointment, "Appointment status updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// CANCEL: Cancels an appointment.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            await _schedulingService.CancelAppointmentAsync(id, orgId.Value, ct);
            return Ok(ApiResponse.Ok("Appointment cancelled successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// CONFLICT CHECK: Checks if a veterinarian has a schedule conflict.
    /// </summary>
    [HttpPost("check-conflict")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckConflict([FromBody] ConflictCheckDto dto, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var hasConflict = await _schedulingService.CheckConflictAsync(orgId.Value, dto, ct);
        return Ok(ApiResponse<bool>.Ok(hasConflict));
    }
}
