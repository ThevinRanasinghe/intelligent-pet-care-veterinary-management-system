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

    private readonly IPrescriptionService _prescriptionService;
    private readonly IOwnerAccessService _ownerAccess;

    public PrescriptionsController(
        IPrescriptionService prescriptionService,
        IOwnerAccessService ownerAccess)
    {
        _prescriptionService = prescriptionService;
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

    [HttpPost]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<ActionResult<PrescriptionResponseDto>> Create(CreatePrescriptionDto dto)
    {
        var createdPrescription = await _prescriptionService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdPrescription.Id }, createdPrescription);
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
