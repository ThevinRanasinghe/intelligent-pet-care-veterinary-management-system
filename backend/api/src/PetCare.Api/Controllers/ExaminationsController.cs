using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExaminationsController : ControllerBase
{
    private readonly IExaminationService _examinationService;

    public ExaminationsController(IExaminationService examinationService)
    {
        _examinationService = examinationService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExaminationResponseDto>>> GetAll()
    {
        var examinations = await _examinationService.GetAllAsync();
        return Ok(examinations);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExaminationResponseDto>> GetById(Guid id)
    {
        var examination = await _examinationService.GetByIdAsync(id);
        if (examination == null)
            return NotFound(new { message = $"Examination with ID {id} not found." });

        return Ok(examination);
    }

    [HttpGet("pet/{petId}")]
    public async Task<ActionResult<IEnumerable<ExaminationResponseDto>>> GetByPetId(string petId)
    {
        var examinations = await _examinationService.GetByPetIdAsync(petId);
        return Ok(examinations);
    }

    [HttpPost]
    public async Task<ActionResult<ExaminationResponseDto>> Create(CreateExaminationDto dto)
    {
        var createdExamination = await _examinationService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdExamination.Id }, createdExamination);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateExaminationDto dto)
    {
        var updatedExamination = await _examinationService.UpdateAsync(id, dto);
        if (updatedExamination == null)
            return NotFound(new { message = $"Examination with ID {id} not found." });

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _examinationService.DeleteAsync(id);
        if (!result)
            return NotFound(new { message = $"Examination with ID {id} not found." });

        return NoContent();
    }

    [HttpGet("{id}/recommendations")]
    public async Task<ActionResult<TreatmentRecommendationDto>> GetRecommendations(Guid id)
    {
        var exam = await _examinationService.GetByIdAsync(id);
        if (exam == null)
            return NotFound(new { message = $"Examination with ID {id} not found." });

        var recommendations = await _examinationService.GetRecommendationsAsync(id);
        return Ok(recommendations);
    }
}
