using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

/// <summary>
/// Appointment scheduling endpoints. All business rules (veterinarian
/// availability, conflict detection, slot validation) are enforced by
/// <see cref="ISchedulingService"/> in PetCare.Application; this controller
/// only handles HTTP concerns (routing, status codes, model binding).
/// </summary>
[ApiController]
[Route("api/appointments")]
[Produces("application/json")]
public class AppointmentsController : ControllerBase
{
    private readonly ISchedulingService _schedulingService;

    public AppointmentsController(ISchedulingService schedulingService)
    {
        _schedulingService = schedulingService;
    }

    /// <summary>Gets all appointments.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppointmentResponse>>> GetAppointments(CancellationToken cancellationToken)
    {
        var appointments = await _schedulingService.GetAppointmentsAsync(cancellationToken);
        return Ok(appointments);
    }

    /// <summary>Gets a single appointment by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponse>> GetAppointmentById(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await _schedulingService.GetAppointmentByIdAsync(id, cancellationToken);
        return appointment is null ? NotFound() : Ok(appointment);
    }

    /// <summary>
    /// Creates a new appointment. Enforces veterinarian existence/active
    /// status, slot existence/ownership/bounds, and the overlap conflict rule.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> CreateAppointment(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var appointment = await _schedulingService.CreateAppointmentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAppointmentById), new { id = appointment.Id }, appointment);
    }

    /// <summary>Updates an existing appointment's schedule/notes, re-validating conflicts.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> UpdateAppointment(
        Guid id,
        [FromBody] UpdateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var appointment = await _schedulingService.UpdateAppointmentAsync(id, request, cancellationToken);
        return Ok(appointment);
    }

    /// <summary>
    /// Cancels an appointment and frees its slot. Modeled as HTTP DELETE
    /// (removes the appointment from the active schedule) but implemented as
    /// a soft cancel, not a hard row delete, per the domain model.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAppointment(Guid id, CancellationToken cancellationToken)
    {
        await _schedulingService.CancelAppointmentAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Gets Available appointment slots, optionally filtered by veterinarian and/or date.</summary>
    [HttpGet("available-slots")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentSlotResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppointmentSlotResponse>>> GetAvailableSlots(
        [FromQuery] Guid? veterinarianId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var slots = await _schedulingService.GetAvailableSlotsAsync(veterinarianId, date, cancellationToken);
        return Ok(slots);
    }

    /// <summary>
    /// Checks whether a proposed appointment time overlaps an existing one
    /// for the same veterinarian, without creating anything.
    /// </summary>
    [HttpPost("check-conflict")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> CheckConflict(
        [FromBody] ConflictCheckRequest request,
        CancellationToken cancellationToken)
    {
        var hasConflict = await _schedulingService.CheckConflictAsync(request, cancellationToken);
        return Ok(hasConflict);
    }
}
