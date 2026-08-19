using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Data;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConsultationsController : ControllerBase
{
    private readonly PetCareDbContext _context;

    public ConsultationsController(PetCareDbContext context)
    {
        _context = context;
    }

    // Endpoint 3: Create Consultation Request (Includes Business Logic: Ownership Validation)
    [HttpPost]
    public async Task<ActionResult<ConsultationResponseDto>> CreateConsultation([FromBody] CreateConsultationRequestDto dto)
    {
        // Business Operation: Validate Pet Ownership
        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.Id == dto.PetId && p.OwnerId == dto.OwnerId);
        if (pet == null)
        {
            return BadRequest(new { message = "Pet ownership validation failed. Pet does not belong to this owner." });
        }

        var request = new ConsultationRequest
        {
            PetId = dto.PetId,
            OwnerId = dto.OwnerId,
            SymptomsDescription = dto.SymptomsDescription,
            PhotoUrl = dto.PhotoUrl,
            PreferredBranch = dto.PreferredBranch,
            PreferredDate = dto.PreferredDate,
            BudgetLimit = dto.BudgetLimit,
            Status = "Pending"
        };

        _context.ConsultationRequests.Add(request);
        await _context.SaveChangesAsync();

        var response = new ConsultationResponseDto
        {
            Id = request.Id,
            PetId = request.PetId,
            OwnerId = request.OwnerId,
            SymptomsDescription = request.SymptomsDescription,
            PhotoUrl = request.PhotoUrl,
            PreferredBranch = request.PreferredBranch,
            PreferredDate = request.PreferredDate,
            BudgetLimit = request.BudgetLimit,
            Status = request.Status,
            CreatedAt = request.CreatedAt
        };

        return Ok(response);
    }

    // Endpoint 4: Check Consultation Request Status
    [HttpGet("{id}/status")]
    public async Task<ActionResult> GetConsultationStatus(Guid id)
    {
        var request = await _context.ConsultationRequests.FindAsync(id);
        if (request == null)
        {
            return NotFound(new { message = "Consultation request not found." });
        }

        return Ok(new { ConsultationId = request.Id, Status = request.Status, UpdatedAt = request.UpdatedAt });
    }
}