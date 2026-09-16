using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/consultations")]
public class ConsultationRequestsController : ControllerBase
{
    private readonly IConsultationRequestService _consultationRequestService;

    public ConsultationRequestsController(
        IConsultationRequestService consultationRequestService)
    {
        _consultationRequestService = consultationRequestService;
    }

    // ============================================================
    // CREATE CONSULTATION REQUEST
    // POST: api/consultations
    // ============================================================
    [HttpPost]
    public async Task<ActionResult<ConsultationRequestDto>> Create(
        [FromBody] CreateConsultationRequestDto dto)
    {
        try
        {
            var consultation =
                await _consultationRequestService.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = consultation.Id },
                consultation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // GET ALL CONSULTATION REQUESTS
    // GET: api/consultations
    // ============================================================
    [HttpGet]
    public async Task<ActionResult<List<ConsultationRequestDto>>> GetAll()
    {
        var consultations =
            await _consultationRequestService.GetAllAsync();

        return Ok(consultations);
    }

    // ============================================================
    // GET CONSULTATION REQUESTS BY OWNER
    // GET: api/consultations/owner/{ownerId}
    // ============================================================
    [HttpGet("owner/{ownerId}")]
    public async Task<ActionResult<List<ConsultationRequestDto>>> GetByOwnerId(
        string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return BadRequest(new
            {
                message = "Owner ID is required."
            });
        }

        var consultations =
            await _consultationRequestService.GetByOwnerIdAsync(ownerId);

        return Ok(consultations);
    }

    // ============================================================
    // GET CONSULTATION REQUEST BY ID
    // GET: api/consultations/{id}
    // ============================================================
    [HttpGet("{id}")]
    public async Task<ActionResult<ConsultationRequestDto>> GetById(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(new
            {
                message = "Consultation ID is required."
            });
        }

        var consultation =
            await _consultationRequestService.GetByIdAsync(id);

        if (consultation == null)
        {
            return NotFound(new
            {
                message = "Consultation request was not found."
            });
        }

        return Ok(consultation);
    }

    // ============================================================
    // UPDATE CONSULTATION REQUEST
    // PUT: api/consultations/{id}
    // ============================================================
    [HttpPut("{id}")]
    public async Task<ActionResult<ConsultationRequestDto>> Update(
        string id,
        [FromBody] UpdateConsultationRequestDto dto)
    {
        try
        {
            var consultation =
                await _consultationRequestService.UpdateAsync(id, dto);

            if (consultation == null)
            {
                return NotFound(new
                {
                    message = "Consultation request was not found."
                });
            }

            return Ok(consultation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // SUBMIT CONSULTATION REQUEST
    // POST: api/consultations/{id}/submit
    // ============================================================
    [HttpPost("{id}/submit")]
    public async Task<ActionResult<ConsultationRequestDto>> Submit(
        string id)
    {
        try
        {
            var consultation =
                await _consultationRequestService.SubmitAsync(id);

            if (consultation == null)
            {
                return NotFound(new
                {
                    message = "Consultation request was not found."
                });
            }

            return Ok(consultation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // CANCEL CONSULTATION REQUEST
    // PATCH: api/consultations/{id}/cancel
    // ============================================================
    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        try
        {
            var cancelled =
                await _consultationRequestService.CancelAsync(id);

            if (!cancelled)
            {
                return NotFound(new
                {
                    message = "Consultation request was not found."
                });
            }

            return Ok(new
            {
                message = "Consultation request cancelled successfully."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}