using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

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
[Authorize]
public class AppointmentsController : ControllerBase
{
    // Scheduling visibility: clinical staff and management (Inventory
    // Officer has no scheduling responsibility).
    private const string ReadRoles =
        $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.SuperAdmin}";

    // Scheduling is managed by the Clinic Manager.
    private const string ManageRoles =
        $"{Roles.ClinicManager},{Roles.SuperAdmin}";

    // By-id/self views also allow the owning PetOwner (ownership is checked
    // inside the action).
    private const string OwnerOrStaffReadRoles =
        $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.SuperAdmin},{Roles.PetOwner}";

    private readonly ISchedulingService _schedulingService;
    private readonly IOwnerAccessService _ownerAccess;

    public AppointmentsController(
        ISchedulingService schedulingService,
        IOwnerAccessService ownerAccess)
    {
        _schedulingService = schedulingService;
        _ownerAccess = ownerAccess;
    }

    /// <summary>Gets all appointments.</summary>
    [HttpGet]
    [Authorize(Roles = ReadRoles)]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppointmentResponse>>> GetAppointments(CancellationToken cancellationToken)
    {
        var appointments = await _schedulingService.GetAppointmentsAsync(cancellationToken);
        return Ok(appointments);
    }

    /// <summary>
    /// Appointments relevant to the caller: veterinarians see their own
    /// schedule, owners see appointments for their pets, managers/admins see
    /// the organization list.
    /// </summary>
    [HttpGet("mine")]
    [Authorize(Roles = OwnerOrStaffReadRoles)]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppointmentResponse>>> GetMyAppointments(
        [FromQuery] string? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? petId,
        CancellationToken cancellationToken)
    {
        var appointments = await _schedulingService.GetMyAppointmentsAsync(status, from, to, petId, cancellationToken);
        return Ok(appointments);
    }

    /// <summary>Gets a single appointment by id.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = OwnerOrStaffReadRoles)]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponse>> GetAppointmentById(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await _schedulingService.GetAppointmentByIdAsync(id, cancellationToken);
        if (appointment is null)
        {
            return NotFound();
        }

        // A pet owner may only read appointments for their own pets.
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsPetAsync(appointment.PetId))
        {
            return NotFound();
        }

        return Ok(appointment);
    }

    /// <summary>
    /// Creates a new appointment. Enforces veterinarian existence/active
    /// status, slot existence/ownership/bounds, and the overlap conflict rule.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = ManageRoles)]
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
    [Authorize(Roles = ManageRoles)]
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
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAppointment(Guid id, CancellationToken cancellationToken)
    {
        await _schedulingService.CancelAppointmentAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Gets Available appointment slots, optionally filtered by veterinarian and/or date.</summary>
    [HttpGet("available-slots")]
    [Authorize(Roles = ReadRoles)]
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
    [Authorize(Roles = ReadRoles)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> CheckConflict(
        [FromBody] ConflictCheckRequest request,
        CancellationToken cancellationToken)
    {
        var hasConflict = await _schedulingService.CheckConflictAsync(request, cancellationToken);
        return Ok(hasConflict);
    }
}
