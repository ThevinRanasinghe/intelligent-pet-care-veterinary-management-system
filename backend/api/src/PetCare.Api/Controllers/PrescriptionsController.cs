using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionService _prescriptionService;

    public PrescriptionsController(IPrescriptionService prescriptionService)
    {
        _prescriptionService = prescriptionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrescriptionResponseDto>>> GetAll()
    {
        var prescriptions = await _prescriptionService.GetAllAsync();
        return Ok(prescriptions);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PrescriptionResponseDto>> GetById(Guid id)
    {
        var prescription = await _prescriptionService.GetByIdAsync(id);
        if (prescription == null)
            return NotFound(new { message = $"Prescription with ID {id} not found." });

        return Ok(prescription);
    }

    [HttpGet("treatment/{treatmentRecordId}")]
    public async Task<ActionResult<IEnumerable<PrescriptionResponseDto>>> GetByTreatmentRecordId(Guid treatmentRecordId)
    {
        var prescriptions = await _prescriptionService.GetByTreatmentRecordIdAsync(treatmentRecordId);
        return Ok(prescriptions);
    }

    [HttpPost]
    public async Task<ActionResult<PrescriptionResponseDto>> Create(CreatePrescriptionDto dto)
    {
        var createdPrescription = await _prescriptionService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = createdPrescription.Id }, createdPrescription);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _prescriptionService.DeleteAsync(id);
        if (!result)
            return NotFound(new { message = $"Prescription with ID {id} not found." });

        return NoContent();
    }
}
