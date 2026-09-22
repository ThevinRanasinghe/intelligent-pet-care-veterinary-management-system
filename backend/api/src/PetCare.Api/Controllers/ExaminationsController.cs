using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExaminationsController : ControllerBase
{
    private const string StaffRoles =
        $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin}";

    private const string ClinicalWriteRoles =
        $"{Roles.Veterinarian},{Roles.SuperAdmin}";

    private readonly IExaminationService _examinationService;
    private readonly IOwnerAccessService _ownerAccess;

    public ExaminationsController(
        IExaminationService examinationService,
        IOwnerAccessService ownerAccess)
    {
        _examinationService = examinationService;
        _ownerAccess = ownerAccess;
    }

    [HttpGet]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<IEnumerable<ExaminationResponseDto>>> GetAll()
    {
        var examinations = await _examinationService.GetAllAsync();
        return Ok(examinations);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExaminationResponseDto>> GetById(Guid id)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsExaminationAsync(id))
        {
            return NotFound(new { message = $"Examination with ID {id} not found." });
        }

        var examination = await _examinationService.GetByIdAsync(id);
        if (examination == null)
            return NotFound(new { message = $"Examination with ID {id} not found." });

        return Ok(examination);
    }

    [HttpGet("pet/{petId}")]
    public async Task<ActionResult<IEnumerable<ExaminationResponseDto>>> GetByPetId(string petId)
    {
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsPetAsync(petId))
        {
            return Forbid();
        }

        var examinations = await _examinationService.GetByPetIdAsync(petId);
        return Ok(examinations);
    }

    [HttpPost]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<ActionResult<ExaminationResponseDto>> Create(CreateExaminationDto dto)
    {
        var createdExamination = await _examinationService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdExamination.Id }, createdExamination);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<IActionResult> Update(Guid id, UpdateExaminationDto dto)
    {
        var updatedExamination = await _examinationService.UpdateAsync(id, dto);
        if (updatedExamination == null)
            return NotFound(new { message = $"Examination with ID {id} not found." });

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = ClinicalWriteRoles)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _examinationService.DeleteAsync(id);
        if (!result)
            return NotFound(new { message = $"Examination with ID {id} not found." });

        return NoContent();
    }

    [HttpGet("{id}/recommendations")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<TreatmentRecommendationDto>> GetRecommendations(Guid id)
    {
        var exam = await _examinationService.GetByIdAsync(id);
        if (exam == null)
            return NotFound(new { message = $"Examination with ID {id} not found." });

        var recommendations = await _examinationService.GetRecommendationsAsync(id);
        return Ok(recommendations);
    }
}
