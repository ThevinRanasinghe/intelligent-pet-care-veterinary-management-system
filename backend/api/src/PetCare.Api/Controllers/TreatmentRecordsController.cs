using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TreatmentRecordsController : ControllerBase
{
    private const string StaffRoles =
        $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.SuperAdmin}";

    private const string OwnerOrStaffReadRoles =
        $"{Roles.PetOwner},{Roles.Veterinarian},{Roles.ClinicManager},{Roles.SuperAdmin}";

    private const string ClinicalWriteRoles =
        $"{Roles.Veterinarian},{Roles.SuperAdmin}";

    private readonly ITreatmentRecordService _treatmentRecordService;
    private readonly IOwnerAccessService _ownerAccess;

    public TreatmentRecordsController(
        ITreatmentRecordService treatmentRecordService,
        IOwnerAccessService ownerAccess)
    {
        _treatmentRecordService = treatmentRecordService;
        _ownerAccess = ownerAccess;
    }

    [HttpGet]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<IEnumerable<TreatmentRecordResponseDto>>> GetAll()
    {
        var records = await _treatmentRecordService.GetAllAsync();
        return Ok(records);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = OwnerOrStaffReadRoles)]
    public async Task<ActionResult<TreatmentRecordResponseDto>> GetById(Guid id)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsTreatmentRecordAsync(id))
        {
            return NotFound(new { message = $"Treatment record with ID {id} not found." });
        }

        var record = await _treatmentRecordService.GetByIdAsync(id);
        if (record == null)
            return NotFound(new { message = $"Treatment record with ID {id} not found." });

        return Ok(record);
    }

    [HttpGet("diagnosis/{diagnosisId}")]
    [Authorize(Roles = OwnerOrStaffReadRoles)]
    public async Task<ActionResult<IEnumerable<TreatmentRecordResponseDto>>> GetByDiagnosisId(Guid diagnosisId)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsDiagnosisAsync(diagnosisId))
        {
            return Forbid();
        }

        var records = await _treatmentRecordService.GetByDiagnosisIdAsync(diagnosisId);
        return Ok(records);
    }

    [HttpPost]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<ActionResult<TreatmentRecordResponseDto>> Create(CreateTreatmentRecordDto dto)
    {
        var createdRecord = await _treatmentRecordService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdRecord.Id }, createdRecord);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<IActionResult> Update(Guid id, UpdateTreatmentRecordDto dto)
    {
        var updatedRecord = await _treatmentRecordService.UpdateAsync(id, dto);
        if (updatedRecord == null)
            return NotFound(new { message = $"Treatment record with ID {id} not found." });

        return NoContent();
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateTreatmentStatusDto dto)
    {
        var updatedRecord = await _treatmentRecordService.UpdateStatusAsync(id, dto);
        if (updatedRecord == null)
            return NotFound(new { message = $"Treatment record with ID {id} not found." });

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _treatmentRecordService.DeleteAsync(id);
        if (!result)
            return NotFound(new { message = $"Treatment record with ID {id} not found." });

        return NoContent();
    }
}
