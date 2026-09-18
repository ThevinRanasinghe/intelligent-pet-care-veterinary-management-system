using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosesController : ControllerBase
{
    private readonly IDiagnosisService _diagnosisService;

    public DiagnosesController(IDiagnosisService diagnosisService)
    {
        _diagnosisService = diagnosisService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DiagnosisResponseDto>>> GetAll()
    {
        var diagnoses = await _diagnosisService.GetAllAsync();
        return Ok(diagnoses);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DiagnosisResponseDto>> GetById(Guid id)
    {
        var diagnosis = await _diagnosisService.GetByIdAsync(id);
        if (diagnosis == null)
            return NotFound(new { message = $"Diagnosis with ID {id} not found." });

        return Ok(diagnosis);
    }

    [HttpGet("examination/{examinationId}")]
    public async Task<ActionResult<IEnumerable<DiagnosisResponseDto>>> GetByExaminationId(Guid examinationId)
    {
        var diagnoses = await _diagnosisService.GetByExaminationIdAsync(examinationId);
        return Ok(diagnoses);
    }

    [HttpPost]
    public async Task<ActionResult<DiagnosisResponseDto>> Create(CreateDiagnosisDto dto)
    {
        var createdDiagnosis = await _diagnosisService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdDiagnosis.Id }, createdDiagnosis);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateDiagnosisDto dto)
    {
        var updatedDiagnosis = await _diagnosisService.UpdateAsync(id, dto);
        if (updatedDiagnosis == null)
            return NotFound(new { message = $"Diagnosis with ID {id} not found." });

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _diagnosisService.DeleteAsync(id);
        if (!result)
            return NotFound(new { message = $"Diagnosis with ID {id} not found." });

        return NoContent();
    }
}
