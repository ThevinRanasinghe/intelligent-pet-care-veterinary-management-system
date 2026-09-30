using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrescriptionsController : ControllerBase
{
    private const string StaffRoles =
        $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.SuperAdmin}";

    private const string OwnerOrStaffReadRoles =
        $"{Roles.PetOwner},{Roles.Veterinarian},{Roles.ClinicManager},{Roles.SuperAdmin}";

    private const string ClinicalWriteRoles =
        $"{Roles.Veterinarian},{Roles.SuperAdmin}";

    // The medicine-request queue is the Inventory Officer's responsibility;
    // veterinarians and managers can see it for visibility.
    private const string MedicineRequestReadRoles =
        $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin}";

    // Issuing stock / marking unavailable is restricted to the inventory desk.
    private const string MedicineRequestProcessRoles =
        $"{Roles.InventoryOfficer},{Roles.SuperAdmin}";

    private readonly IPrescriptionService _prescriptionService;
    private readonly IMedicineRequestService _medicineRequests;
    private readonly IOwnerAccessService _ownerAccess;

    public PrescriptionsController(
        IPrescriptionService prescriptionService,
        IMedicineRequestService medicineRequests,
        IOwnerAccessService ownerAccess)
    {
        _prescriptionService = prescriptionService;
        _medicineRequests = medicineRequests;
        _ownerAccess = ownerAccess;
    }

    [HttpGet]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<IEnumerable<PrescriptionResponseDto>>> GetAll()
    {
        var prescriptions = await _prescriptionService.GetAllAsync();
        return Ok(prescriptions);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = OwnerOrStaffReadRoles)]
    public async Task<ActionResult<PrescriptionResponseDto>> GetById(Guid id)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsPrescriptionAsync(id))
        {
            return NotFound(new { message = $"Prescription with ID {id} not found." });
        }

        var prescription = await _prescriptionService.GetByIdAsync(id);
        if (prescription == null)
            return NotFound(new { message = $"Prescription with ID {id} not found." });

        return Ok(prescription);
    }

    [HttpGet("treatment/{treatmentRecordId}")]
    [Authorize(Roles = OwnerOrStaffReadRoles)]
    public async Task<ActionResult<IEnumerable<PrescriptionResponseDto>>> GetByTreatmentRecordId(Guid treatmentRecordId)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsTreatmentRecordAsync(treatmentRecordId))
        {
            return Forbid();
        }

        var prescriptions = await _prescriptionService.GetByTreatmentRecordIdAsync(treatmentRecordId);
        return Ok(prescriptions);
    }

    /// <summary>
    /// Creates one medicine request holding 1–10 medicine items; each item
    /// becomes a Pending prescription row the inventory desk can process
    /// independently.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<ActionResult<IReadOnlyList<PrescriptionResponseDto>>> Create(CreatePrescriptionDto dto)
    {
        var created = await _prescriptionService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetByTreatmentRecordId), new { treatmentRecordId = dto.TreatmentRecordId }, created);
    }

    /// <summary>
    /// Medicine-request queue: prescriptions awaiting (or already)
    /// fulfillment, optionally filtered by status
    /// (Pending | Issued | Unavailable), newest first.
    /// </summary>
    [HttpGet("requests")]
    [Authorize(Roles = MedicineRequestReadRoles)]
    [ProducesResponseType(typeof(IReadOnlyList<PrescriptionResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PrescriptionResponseDto>>> GetRequests(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var requests = await _medicineRequests.GetRequestsAsync(status, cancellationToken);
        return Ok(requests);
    }

    /// <summary>
    /// Issues stock for a pending request: reserves + dispenses atomically,
    /// then refreshes the appointment's bill.
    /// </summary>
    [HttpPost("{id:guid}/issue")]
    [Authorize(Roles = MedicineRequestProcessRoles)]
    [ProducesResponseType(typeof(PrescriptionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PrescriptionResponseDto>> Issue(Guid id, CancellationToken cancellationToken)
    {
        var prescription = await _medicineRequests.IssueAsync(id, cancellationToken);
        return Ok(prescription);
    }

    /// <summary>Marks a pending request Unavailable with a required reason.</summary>
    [HttpPost("{id:guid}/unavailable")]
    [Authorize(Roles = MedicineRequestProcessRoles)]
    [ProducesResponseType(typeof(PrescriptionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PrescriptionResponseDto>> MarkUnavailable(
        Guid id,
        [FromBody] MarkPrescriptionUnavailableRequest request,
        CancellationToken cancellationToken)
    {
        var prescription = await _medicineRequests.MarkUnavailableAsync(id, request.Reason, cancellationToken);
        return Ok(prescription);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _prescriptionService.DeleteAsync(id);
        if (!result)
            return NotFound(new { message = $"Prescription with ID {id} not found." });

        return NoContent();
    }
}
