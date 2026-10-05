using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DiagnosesController : ControllerBase
{
    private const string StaffRoles =
        $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.SuperAdmin}";

    private const string OwnerOrStaffReadRoles =
        $"{Roles.PetOwner},{Roles.Veterinarian},{Roles.ClinicManager},{Roles.SuperAdmin}";

    private const string ClinicalWriteRoles =
        $"{Roles.Veterinarian},{Roles.SuperAdmin}";

    private readonly IDiagnosisService _diagnosisService;
    private readonly IOwnerAccessService _ownerAccess;

    public DiagnosesController(
        IDiagnosisService diagnosisService,
        IOwnerAccessService ownerAccess)
    {
        _diagnosisService = diagnosisService;
        _ownerAccess = ownerAccess;
    }

    [HttpGet]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<IEnumerable<DiagnosisResponseDto>>> GetAll()
    {
        var diagnoses = await _diagnosisService.GetAllAsync();
        return Ok(diagnoses);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = OwnerOrStaffReadRoles)]
    public async Task<ActionResult<DiagnosisResponseDto>> GetById(Guid id)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsDiagnosisAsync(id))
        {
            return NotFound(new { message = $"Diagnosis with ID {id} not found." });
        }

        var diagnosis = await _diagnosisService.GetByIdAsync(id);
        if (diagnosis == null)
            return NotFound(new { message = $"Diagnosis with ID {id} not found." });

        return Ok(diagnosis);
    }

    [HttpGet("examination/{examinationId}")]
    [Authorize(Roles = OwnerOrStaffReadRoles)]
    public async Task<ActionResult<IEnumerable<DiagnosisResponseDto>>> GetByExaminationId(Guid examinationId)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsExaminationAsync(examinationId))
        {
            return Forbid();
        }

        var diagnoses = await _diagnosisService.GetByExaminationIdAsync(examinationId);
        return Ok(diagnoses);
    }

    [HttpPost]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<ActionResult<DiagnosisResponseDto>> Create(CreateDiagnosisDto dto)
    {
        var createdDiagnosis = await _diagnosisService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdDiagnosis.Id }, createdDiagnosis);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<IActionResult> Update(Guid id, UpdateDiagnosisDto dto)
    {
        var updatedDiagnosis = await _diagnosisService.UpdateAsync(id, dto);
        if (updatedDiagnosis == null)
            return NotFound(new { message = $"Diagnosis with ID {id} not found." });

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _diagnosisService.DeleteAsync(id);
        if (!result)
            return NotFound(new { message = $"Diagnosis with ID {id} not found." });

        return NoContent();
    }
}
