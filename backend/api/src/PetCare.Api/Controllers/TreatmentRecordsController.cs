using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TreatmentRecordsController : ControllerBase
{
    private readonly ITreatmentRecordService _treatmentRecordService;

    public TreatmentRecordsController(ITreatmentRecordService treatmentRecordService)
    {
        _treatmentRecordService = treatmentRecordService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TreatmentRecordResponseDto>>> GetAll()
    {
        var records = await _treatmentRecordService.GetAllAsync();
        return Ok(records);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TreatmentRecordResponseDto>> GetById(Guid id)
    {
        var record = await _treatmentRecordService.GetByIdAsync(id);
        if (record == null)
            return NotFound(new { message = $"Treatment record with ID {id} not found." });

        return Ok(record);
    }

    [HttpGet("diagnosis/{diagnosisId}")]
    public async Task<ActionResult<IEnumerable<TreatmentRecordResponseDto>>> GetByDiagnosisId(Guid diagnosisId)
    {
        var records = await _treatmentRecordService.GetByDiagnosisIdAsync(diagnosisId);
        return Ok(records);
    }

    [HttpPost]
    public async Task<ActionResult<TreatmentRecordResponseDto>> Create(CreateTreatmentRecordDto dto)
    {
        var createdRecord = await _treatmentRecordService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdRecord.Id }, createdRecord);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateTreatmentRecordDto dto)
    {
        var updatedRecord = await _treatmentRecordService.UpdateAsync(id, dto);
        if (updatedRecord == null)
            return NotFound(new { message = $"Treatment record with ID {id} not found." });

        return NoContent();
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateTreatmentStatusDto dto)
    {
        var updatedRecord = await _treatmentRecordService.UpdateStatusAsync(id, dto);
        if (updatedRecord == null)
            return NotFound(new { message = $"Treatment record with ID {id} not found." });

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _treatmentRecordService.DeleteAsync(id);
        if (!result)
            return NotFound(new { message = $"Treatment record with ID {id} not found." });

        return NoContent();
    }
}
