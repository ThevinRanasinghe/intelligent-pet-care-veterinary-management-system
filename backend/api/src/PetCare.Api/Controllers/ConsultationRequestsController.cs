using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/consultations")]
[Authorize]
public class ConsultationRequestsController : ControllerBase
{
    private readonly IConsultationRequestService _consultationRequestService;
    private readonly IOwnerAccessService _ownerAccess;

    public ConsultationRequestsController(
        IConsultationRequestService consultationRequestService,
        IOwnerAccessService ownerAccess)
    {
        _consultationRequestService = consultationRequestService;
        _ownerAccess = ownerAccess;
    }

    // ============================================================
    // CREATE CONSULTATION REQUEST
    // POST: api/consultations
    // ============================================================
    [HttpPost]
    public async Task<ActionResult<ConsultationRequestDto>> Create(
        [FromBody] CreateConsultationRequestDto dto)
    {
        if (_ownerAccess.IsPetOwner)
        {
            var ownerId = await _ownerAccess.GetOwnerIdAsync();
            if (ownerId == null || !await _ownerAccess.OwnsPetAsync(dto.PetId))
            {
                return Forbid();
            }

            // A pet owner can only file requests under their own profile.
            dto.OwnerId = ownerId;
        }

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
        if (_ownerAccess.IsPetOwner)
        {
            var ownerId = await _ownerAccess.GetOwnerIdAsync();
            var ownConsultations = ownerId == null
                ? new List<ConsultationRequestDto>()
                : await _consultationRequestService.GetByOwnerIdAsync(ownerId);

            return Ok(ownConsultations);
        }

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

        if (_ownerAccess.IsPetOwner && ownerId != await _ownerAccess.GetOwnerIdAsync())
        {
            return Forbid();
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

        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsConsultationAsync(id))
        {
            return NotFound(new
            {
                message = "Consultation request was not found."
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
    // GET CONSULTATION STATUS HISTORY
    // GET: api/consultations/{id}/history
    // ============================================================
    [HttpGet("{id}/history")]
    public async Task<ActionResult<List<ConsultationStatusHistoryDto>>>
        GetStatusHistory(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new
                {
                    message = "Consultation ID is required."
                });
            }

            if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsConsultationAsync(id))
            {
                return NotFound(new
                {
                    message = "Consultation request was not found."
                });
            }

            var history =
                await _consultationRequestService
                    .GetStatusHistoryAsync(id);

            return Ok(history);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
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
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsConsultationAsync(id))
        {
            return NotFound(new
            {
                message = "Consultation request was not found."
            });
        }

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
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsConsultationAsync(id))
        {
            return NotFound(new
            {
                message = "Consultation request was not found."
            });
        }

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
        if (_ownerAccess.IsPetOwner && !await _ownerAccess.OwnsConsultationAsync(id))
        {
            return NotFound(new
            {
                message = "Consultation request was not found."
            });
        }

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

    // ============================================================
// FIND NEAREST CLINIC
// GET: api/consultations/nearest-clinic
// ============================================================
[HttpGet("nearest-clinic")]
public ActionResult<object> GetNearestClinic(
    [FromQuery] double latitude,
    [FromQuery] double longitude)
{
    if (latitude < -90 || latitude > 90)
    {
        return BadRequest(new
        {
            message = "Invalid latitude."
        });
    }

    if (longitude < -180 || longitude > 180)
    {
        return BadRequest(new
        {
            message = "Invalid longitude."
        });
    }

    var clinics = new[]
    {
        new
        {
            name = "Colombo 03 Central Clinic",
            address = "Colombo 03",
            latitude = 6.9044,
            longitude = 79.8528
        },
        new
        {
            name = "Kandy Veterinary Hospital",
            address = "Kandy",
            latitude = 7.2906,
            longitude = 80.6337
        },
        new
        {
            name = "Negombo Pet Care Clinic",
            address = "Negombo",
            latitude = 7.2083,
            longitude = 79.8358
        }
    };

    var nearestClinic = clinics
        .Select(clinic => new
        {
            clinic.name,
            clinic.address,
            clinic.latitude,
            clinic.longitude,
            distanceKm = CalculateDistanceKm(
                latitude,
                longitude,
                clinic.latitude,
                clinic.longitude)
        })
        .OrderBy(x => x.distanceKm)
        .First();

    return Ok(nearestClinic);
}

// ============================================================
// HAVERSINE DISTANCE CALCULATION
// ============================================================
private static double CalculateDistanceKm(
    double lat1,
    double lon1,
    double lat2,
    double lon2)
{
    const double earthRadiusKm = 6371;

    var dLat = DegreesToRadians(lat2 - lat1);
    var dLon = DegreesToRadians(lon2 - lon1);

    var a =
        Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
        Math.Cos(DegreesToRadians(lat1)) *
        Math.Cos(DegreesToRadians(lat2)) *
        Math.Sin(dLon / 2) *
        Math.Sin(dLon / 2);

    var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

    return Math.Round(earthRadiusKm * c, 2);
}

private static double DegreesToRadians(double degrees)
{
    return degrees * Math.PI / 180;
}


    // ============================================================
    // VALIDATE PET OWNERSHIP
    // GET: api/consultations/validate-ownership
    // ============================================================
    [HttpGet("validate-ownership")]
    public async Task<ActionResult<object>> ValidateOwnership(
        [FromQuery] string petId,
        [FromQuery] string ownerId)
    {
        if (string.IsNullOrWhiteSpace(petId) ||
            string.IsNullOrWhiteSpace(ownerId))
        {
            return BadRequest(new
            {
                isValid = false,
                message = "Pet ID and Owner ID are required."
            });
        }

        if (_ownerAccess.IsPetOwner && ownerId != await _ownerAccess.GetOwnerIdAsync())
        {
            return Forbid();
        }

        var isValid =
            await _consultationRequestService
                .ValidateOwnershipAsync(petId, ownerId);

        return Ok(new
        {
            isValid,
            message = isValid
                ? "Pet ownership validated successfully."
                : "The selected pet does not belong to the selected owner."
        });
    }
}