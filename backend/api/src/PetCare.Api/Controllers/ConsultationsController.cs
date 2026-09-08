using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;
using PetCare.Domain.Enums;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConsultationsController : ControllerBase
{
    private readonly IConsultationService _consultationService;

    public ConsultationsController(IConsultationService consultationService)
    {
        _consultationService = consultationService;
    }

    /// <summary>
    /// UC-09 to UC-13: Submit Consultation Request
    /// (Includes UC-14 Security/Business Logic Check: Validates Pet belongs to requesting Owner)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ConsultationResponseDto>> CreateConsultation([FromBody] CreateConsultationRequestDto dto)
    {
        var response = await _consultationService.SubmitConsultationRequestAsync(dto);
        return CreatedAtAction(nameof(GetConsultationById), new { id = response.Id }, response);
    }

    /// <summary>
    /// UC-14: Validate Pet Ownership
    /// Explicit verification check to ensure Pet belongs to requesting Owner.
    /// </summary>
    [HttpGet("validate-ownership")]
    public async Task<ActionResult> ValidateOwnership([FromQuery] string petId, [FromQuery] string ownerId)
    {
        var isValid = await _consultationService.ValidatePetOwnershipAsync(petId, ownerId);
        if (!isValid)
        {
            return BadRequest(new
            {
                isValid = false,
                message = $"Pet ownership validation failed. Pet '{petId}' does not belong to owner '{ownerId}'."
            });
        }

        return Ok(new
        {
            isValid = true,
            petId,
            ownerId,
            message = "Pet ownership validated successfully."
        });
    }

    /// <summary>
    /// UC-15: View all Consultation Requests (clinic-wide, with optional status filter)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConsultationResponseDto>>> GetAllConsultations([FromQuery] ConsultationStatus? status = null)
    {
        var requests = await _consultationService.GetAllConsultationsAsync(status);
        return Ok(requests);
    }

    /// <summary>
    /// UC-15: Get single Consultation Request by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ConsultationResponseDto>> GetConsultationById(string id)
    {
        var response = await _consultationService.GetConsultationByIdAsync(id);
        return Ok(response);
    }

    /// <summary>
    /// UC-15: Get all Consultation Requests for a specific Owner
    /// </summary>
    [HttpGet("owner/{ownerId}")]
    public async Task<ActionResult<IEnumerable<ConsultationResponseDto>>> GetConsultationsByOwner(string ownerId)
    {
        var requests = await _consultationService.GetConsultationsByOwnerAsync(ownerId);
        return Ok(requests);
    }

    /// <summary>
    /// UC-15: Get all Consultation Requests for a specific Pet
    /// </summary>
    [HttpGet("pet/{petId}")]
    public async Task<ActionResult<IEnumerable<ConsultationResponseDto>>> GetConsultationsByPet(string petId)
    {
        var requests = await _consultationService.GetConsultationsByPetAsync(petId);
        return Ok(requests);
    }

    /// <summary>
    /// UC-16: Track Consultation Status
    /// Returns current workflow status and transition history.
    /// </summary>
    [HttpGet("{id}/status")]
    public async Task<ActionResult<ConsultationStatusTrackingDto>> GetConsultationStatus(string id)
    {
        var status = await _consultationService.GetConsultationStatusAsync(id);
        return Ok(status);
    }

    /// <summary>
    /// UC-16: Update Consultation Workflow Status
    /// Supported values: Submitted, Processing, PendingApproval, Approved, Rejected, RevisionRequired, AppointmentConfirmed.
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<ConsultationResponseDto>> UpdateConsultationStatus(
        string id,
        [FromBody] UpdateConsultationStatusDto dto)
    {
        var updated = await _consultationService.UpdateConsultationStatusAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>
    /// UC-17: View Consultation Request History / Audit Log
    /// </summary>
    [HttpGet("{id}/history")]
    public async Task<ActionResult<IEnumerable<ConsultationHistoryItemDto>>> GetConsultationHistory(string id)
    {
        var history = await _consultationService.GetConsultationHistoryAsync(id);
        return Ok(history);
    }
}