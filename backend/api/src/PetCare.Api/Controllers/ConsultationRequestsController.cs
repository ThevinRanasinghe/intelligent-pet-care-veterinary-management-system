using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Clinics;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/consultations")]
[Authorize]
public class ConsultationRequestsController : ControllerBase
{
    // Consultation visibility: the owning PetOwner (scoped inside each
    // action) plus clinic management. Veterinarians work from assigned
    // appointments — the general request queue is not theirs; the
    // Inventory Officer has no consultation-domain responsibility.
    private const string ReadRoles =
        $"{Roles.PetOwner},{Roles.ClinicManager},{Roles.SuperAdmin}";

    // Requests are filed/managed by their owner or clinic management
    // (e.g. front-desk intake); veterinarians work from examinations.
    private const string ManageRoles =
        $"{Roles.PetOwner},{Roles.ClinicManager},{Roles.SuperAdmin}";

    // Assigning a veterinarian to a request is a clinic-management action —
    // the pet owner and the veterinarian never choose the assignee.
    private const string AssignRoles =
        $"{Roles.ClinicManager},{Roles.SuperAdmin}";

    // Follow-up consultations are requested by the attending veterinarian.
    private const string FollowUpRoles =
        $"{Roles.Veterinarian},{Roles.SuperAdmin}";

    // Availability is read by everyone involved in booking: owners pick a
    // slot, managers assign one, vets request follow-ups, the inventory
    // officer needs read visibility.
    private const string AvailabilityRoles =
        $"{Roles.PetOwner},{Roles.Veterinarian},{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin}";

    private readonly IConsultationRequestService _consultationRequestService;
    private readonly IConsultationWorkflowService _consultationWorkflow;
    private readonly IBookingAvailabilityService _bookingAvailability;
    private readonly IOwnerAccessService _ownerAccess;
    private readonly IClinicLocatorService _clinicLocator;
    private readonly IValidator<CreateConsultationRequestDto> _createValidator;
    private readonly IValidator<UpdateConsultationRequestDto> _updateValidator;

    public ConsultationRequestsController(
        IConsultationRequestService consultationRequestService,
        IConsultationWorkflowService consultationWorkflow,
        IBookingAvailabilityService bookingAvailability,
        IOwnerAccessService ownerAccess,
        IClinicLocatorService clinicLocator,
        IValidator<CreateConsultationRequestDto> createValidator,
        IValidator<UpdateConsultationRequestDto> updateValidator)
    {
        _consultationRequestService = consultationRequestService;
        _consultationWorkflow = consultationWorkflow;
        _bookingAvailability = bookingAvailability;
        _ownerAccess = ownerAccess;
        _clinicLocator = clinicLocator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    // ============================================================
    // CREATE CONSULTATION REQUEST
    // POST: api/consultations
    // ============================================================
    [HttpPost]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<ConsultationRequestDto>> Create(
        [FromBody] CreateConsultationRequestDto dto)
    {
        // Booking rules (org/date/slot) are enforced up front — failures map
        // to 400 via the ValidationException middleware path.
        await _createValidator.ValidateAndThrowAsync(dto);

        if (_ownerAccess.IsPetOwner)
        {
            var ownerId = await _ownerAccess.GetOwnerIdAsync();
            if (ownerId == null || !await _ownerAccess.OwnsPetAsync(dto.PetId))
            {
                return Forbid();
            }

            // A pet owner can only file requests under their own profile.
            dto.OwnerId = ownerId;
        }

        try
        {
            var consultation =
                await _consultationRequestService.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = consultation.Id },
                consultation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // ASSIGN VETERINARIAN
    // POST: api/consultations/{id}/assign
    // ============================================================
    /// <summary>
    /// Assigns a veterinarian to a Submitted/Processing request: creates the
    /// Reserved slot + Confirmed appointment and moves the request to
    /// AppointmentConfirmed.
    /// </summary>
    [HttpPost("{id}/assign")]
    [Authorize(Roles = AssignRoles)]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> Assign(
        string id,
        [FromBody] AssignVeterinarianRequest request,
        CancellationToken cancellationToken)
    {
        var appointment =
            await _consultationWorkflow.AssignConsultationAsync(id, request, cancellationToken);

        return Ok(appointment);
    }

    // ============================================================
    // VETERINARIAN FOLLOW-UP REQUEST
    // POST: api/consultations/follow-up
    // ============================================================
    /// <summary>
    /// Files a FollowUp consultation request for a pet on behalf of the
    /// attending veterinarian (RequestType = FollowUp, Status = Submitted).
    /// </summary>
    [HttpPost("follow-up")]
    [Authorize(Roles = FollowUpRoles)]
    [ProducesResponseType(typeof(ConsultationRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsultationRequestDto>> CreateFollowUp(
        [FromBody] CreateFollowUpRequest request,
        CancellationToken cancellationToken)
    {
        var consultation =
            await _consultationWorkflow.CreateFollowUpAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = consultation.Id },
            consultation);
    }

    // ============================================================
    // BOOKING AVAILABILITY
    // GET: api/consultations/availability?organizationId&date[&veterinarianId]
    // GET: api/consultations/availability/month?organizationId&year&month
    // ============================================================
    /// <summary>
    /// Slot-by-slot availability for one organization day. A slot is
    /// available while at least one active veterinarian is free;
    /// <c>availableVeterinarianIds</c> lets a manager pick a vet for a slot.
    /// </summary>
    [HttpGet("availability")]
    [Authorize(Roles = AvailabilityRoles)]
    [ProducesResponseType(typeof(DayAvailabilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DayAvailabilityDto>> GetAvailability(
        [FromQuery] Guid organizationId,
        [FromQuery] DateOnly date,
        [FromQuery] Guid? veterinarianId,
        CancellationToken cancellationToken)
    {
        var availability = await _bookingAvailability.GetDayAvailabilityAsync(
            organizationId, date, veterinarianId, cancellationToken);
        return Ok(availability);
    }

    /// <summary>
    /// Month overview for the booking calendar: past days, fully-booked days
    /// and days that still have a free veterinarian.
    /// </summary>
    [HttpGet("availability/month")]
    [Authorize(Roles = AvailabilityRoles)]
    [ProducesResponseType(typeof(IReadOnlyList<MonthAvailabilityDayDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MonthAvailabilityDayDto>>> GetMonthAvailability(
        [FromQuery] Guid organizationId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
        {
            return BadRequest(new { message = "A valid year and month are required." });
        }

        var days = await _bookingAvailability.GetMonthAvailabilityAsync(
            organizationId, year, month, cancellationToken);
        return Ok(days);
    }

    // ============================================================
    // GET ALL CONSULTATION REQUESTS
    // GET: api/consultations
    // ============================================================
    [HttpGet]
    [Authorize(Roles = ReadRoles)]
    public async Task<ActionResult<List<ConsultationRequestDto>>> GetAll()
    {
        if (_ownerAccess.IsPetOwner)
        {
            var ownerId = await _ownerAccess.GetOwnerIdAsync();
            var ownConsultations = ownerId == null
                ? new List<ConsultationRequestDto>()
                : await _consultationRequestService.GetByOwnerIdAsync(ownerId);

            return Ok(ownConsultations);
        }

        var consultations =
            await _consultationRequestService.GetAllAsync();

        return Ok(consultations);
    }

    // ============================================================
    // GET CONSULTATION REQUESTS BY OWNER
    // GET: api/consultations/owner/{ownerId}
    // ============================================================
    [HttpGet("owner/{ownerId}")]
    [Authorize(Roles = ReadRoles)]
    public async Task<ActionResult<List<ConsultationRequestDto>>> GetByOwnerId(
        string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return BadRequest(new
            {
                message = "Owner ID is required."
            });
        }

        if (_ownerAccess.IsPetOwner && ownerId != await _ownerAccess.GetOwnerIdAsync())
        {
            return Forbid();
        }

        var consultations =
            await _consultationRequestService.GetByOwnerIdAsync(ownerId);

        return Ok(consultations);
    }

    // ============================================================
    // GET CONSULTATION REQUEST BY ID
    // GET: api/consultations/{id}
    // ============================================================
    [HttpGet("{id}")]
    [Authorize(Roles = ReadRoles)]
    public async Task<ActionResult<ConsultationRequestDto>> GetById(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(new
            {
                message = "Consultation ID is required."
            });
        }

        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsConsultationAsync(id))
        {
            return NotFound(new
            {
                message = "Consultation request was not found."
            });
        }

        var consultation =
            await _consultationRequestService.GetByIdAsync(id);

        if (consultation == null)
        {
            return NotFound(new
            {
                message = "Consultation request was not found."
            });
        }

        return Ok(consultation);
    }

    // ============================================================
    // AI CONSULTATION ANALYSIS (ADVISORY)
    // GET: api/consultations/{id}/analysis
    // ============================================================
    /// <summary>
    /// Advisory AI triage of a consultation request for the reviewing
    /// clinic manager. Read-only: the caller's bearer token is forwarded
    /// to the agentic service so the agent's backend reads keep the
    /// caller's role and organization scope. Nothing is persisted — the
    /// manager still reviews and assigns manually. Agent unavailability
    /// returns Source="unavailable" rather than blocking the workflow.
    /// </summary>
    [HttpGet("{id}/analysis")]
    [Authorize(Roles = AssignRoles)]
    public async Task<ActionResult<ConsultationAnalysisDto>> GetAnalysis(
        string id)
    {
        var bearerToken = Request.Headers.Authorization.ToString();
        var analysis =
            await _consultationRequestService.GetAnalysisAsync(
                id, bearerToken);
        return Ok(analysis);
    }

    // ============================================================
    // GET CONSULTATION STATUS HISTORY
    // GET: api/consultations/{id}/history
    // ============================================================
    [HttpGet("{id}/history")]
    [Authorize(Roles = ReadRoles)]
    public async Task<ActionResult<List<ConsultationStatusHistoryDto>>>
        GetStatusHistory(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new
                {
                    message = "Consultation ID is required."
                });
            }

            if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsConsultationAsync(id))
            {
                return NotFound(new
                {
                    message = "Consultation request was not found."
                });
            }

            var history =
                await _consultationRequestService
                    .GetStatusHistoryAsync(id);

            return Ok(history);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // UPDATE CONSULTATION REQUEST
    // PUT: api/consultations/{id}
    // ============================================================
    [HttpPut("{id}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<ConsultationRequestDto>> Update(
        string id,
        [FromBody] UpdateConsultationRequestDto dto)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsConsultationAsync(id))
        {
            return NotFound(new
            {
                message = "Consultation request was not found."
            });
        }

        await _updateValidator.ValidateAndThrowAsync(dto);

        try
        {
            var consultation =
                await _consultationRequestService.UpdateAsync(id, dto);

            if (consultation == null)
            {
                return NotFound(new
                {
                    message = "Consultation request was not found."
                });
            }

            return Ok(consultation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // SUBMIT CONSULTATION REQUEST
    // POST: api/consultations/{id}/submit
    // ============================================================
    [HttpPost("{id}/submit")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<ConsultationRequestDto>> Submit(
        string id)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsConsultationAsync(id))
        {
            return NotFound(new
            {
                message = "Consultation request was not found."
            });
        }

        try
        {
            var consultation =
                await _consultationRequestService.SubmitAsync(id);

            if (consultation == null)
            {
                return NotFound(new
                {
                    message = "Consultation request was not found."
                });
            }

            return Ok(consultation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // CANCEL CONSULTATION REQUEST
    // PATCH: api/consultations/{id}/cancel
    // ============================================================
    [HttpPatch("{id}/cancel")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> Cancel(string id)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsConsultationAsync(id))
        {
            return NotFound(new
            {
                message = "Consultation request was not found."
            });
        }

        try
        {
            var cancelled =
                await _consultationRequestService.CancelAsync(id);

            if (!cancelled)
            {
                return NotFound(new
                {
                    message = "Consultation request was not found."
                });
            }

            return Ok(new
            {
                message = "Consultation request cancelled successfully."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // FIND NEARBY CLINICS
    // GET: api/consultations/nearby-clinics
    // ============================================================
    // Any authenticated role may search clinics by distance (the owner map
    // uses it when the browser shares a location).
    [HttpGet("nearby-clinics")]
    public async Task<ActionResult<List<NearbyClinicResponse>>> GetNearbyClinics(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm = 50)
    {
        if (latitude < -90 || latitude > 90)
        {
            return BadRequest(new
            {
                message = "Invalid latitude."
            });
        }

        if (longitude < -180 || longitude > 180)
        {
            return BadRequest(new
            {
                message = "Invalid longitude."
            });
        }

        if (radiusKm <= 0)
        {
            return BadRequest(new
            {
                message = "radiusKm must be greater than zero."
            });
        }

        var clinics = await _clinicLocator.FindNearbyAsync(
            latitude, longitude, radiusKm);

        return Ok(clinics);
    }

    // ============================================================
    // FIND NEAREST CLINIC
    // GET: api/consultations/nearest-clinic
    // ============================================================
    [HttpGet("nearest-clinic")]
    public async Task<ActionResult<object>> GetNearestClinic(
        [FromQuery] double latitude,
        [FromQuery] double longitude)
    {
        if (latitude < -90 || latitude > 90)
        {
            return BadRequest(new
            {
                message = "Invalid latitude."
            });
        }

        if (longitude < -180 || longitude > 180)
        {
            return BadRequest(new
            {
                message = "Invalid longitude."
            });
        }

        var nearestClinic = (await _clinicLocator.FindNearbyAsync(
                latitude, longitude, double.MaxValue))
            .FirstOrDefault();

        if (nearestClinic == null)
        {
            return NotFound(new
            {
                message = "No active clinics with a location were found."
            });
        }

        return Ok(new
        {
            id = nearestClinic.Id,
            name = nearestClinic.Name,
            address = nearestClinic.Address,
            latitude = nearestClinic.Latitude,
            longitude = nearestClinic.Longitude,
            distanceKm = nearestClinic.DistanceKm
        });
    }

    // ============================================================
    // VALIDATE PET OWNERSHIP
    // GET: api/consultations/validate-ownership
    // ============================================================
    [HttpGet("validate-ownership")]
    [Authorize(Roles = ReadRoles)]
    public async Task<ActionResult<object>> ValidateOwnership(
        [FromQuery] string petId,
        [FromQuery] string ownerId)
    {
        if (string.IsNullOrWhiteSpace(petId) ||
            string.IsNullOrWhiteSpace(ownerId))
        {
            return BadRequest(new
            {
                isValid = false,
                message = "Pet ID and Owner ID are required."
            });
        }

        if (_ownerAccess.IsPetOwner && ownerId != await _ownerAccess.GetOwnerIdAsync())
        {
            return Forbid();
        }

        var isValid =
            await _consultationRequestService
                .ValidateOwnershipAsync(petId, ownerId);

        return Ok(new
        {
            isValid,
            message = isValid
                ? "Pet ownership validated successfully."
                : "The selected pet does not belong to the selected owner."
        });
    }
}