using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PetsController : ControllerBase
{
    private readonly IPetService _petService;

    public PetsController(IPetService petService)
    {
        _petService = petService;
    }

    /// <summary>
    /// UC-05: Add Pet (Name, Species, Breed, DOB/Age, Notes, OwnerId)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PetResponseDto>> RegisterPet([FromBody] CreatePetDto dto)
    {
        var response = await _petService.RegisterPetAsync(dto);
        return CreatedAtAction(nameof(GetPetById), new { id = response.Id }, response);
    }

    /// <summary>
    /// UC-06: View Pet Profile by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PetResponseDto>> GetPetById(string id)
    {
        var response = await _petService.GetPetByIdAsync(id);
        return Ok(response);
    }

    /// <summary>
    /// UC-06: View all Pets for a specific Owner
    /// </summary>
    [HttpGet("owner/{ownerId}")]
    public async Task<ActionResult<IEnumerable<PetResponseDto>>> GetPetsByOwner(string ownerId)
    {
        var pets = await _petService.GetPetsByOwnerAsync(ownerId);
        return Ok(pets);
    }

    /// <summary>
    /// UC-06: View all Pets (Clinic-wide)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PetResponseDto>>> GetAllPets()
    {
        var pets = await _petService.GetAllPetsAsync();
        return Ok(pets);
    }

    /// <summary>
    /// UC-06 & UC-07: Update Pet Profile
    /// Note: Clinical and medical records cannot be modified through this profile update endpoint.
    /// Only non-clinical fields (Name, Species, Breed, DOB, Age, Notes, PhotoUrl) are editable by the owner.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<PetResponseDto>> UpdatePetProfile(
        string id,
        [FromBody] UpdatePetProfileDto dto,
        [FromQuery] string? ownerId = null)
    {
        var response = await _petService.UpdatePetProfileAsync(id, dto, ownerId);
        return Ok(response);
    }

    /// <summary>
    /// UC-08: View Pet Medical & Vaccination History
    /// </summary>
    [HttpGet("{id}/medical-history")]
    public async Task<ActionResult<PetMedicalHistoryDto>> GetPetMedicalHistory(string id)
    {
        var history = await _petService.GetPetMedicalHistoryAsync(id);
        return Ok(history);
    }

    /// <summary>
    /// Add clinical medical record (Restricted to authorized veterinary staff)
    /// </summary>
    [HttpPost("{id}/medical-records")]
    public async Task<ActionResult<MedicalRecordDto>> AddMedicalRecord(string id, [FromBody] CreateMedicalRecordDto dto)
    {
        var record = await _petService.AddMedicalRecordAsync(id, dto);
        return CreatedAtAction(nameof(GetPetMedicalHistory), new { id }, record);
    }

    /// <summary>
    /// Add vaccination record (Restricted to authorized veterinary staff)
    /// </summary>
    [HttpPost("{id}/vaccinations")]
    public async Task<ActionResult<VaccinationRecordDto>> AddVaccinationRecord(string id, [FromBody] CreateVaccinationRecordDto dto)
    {
        var record = await _petService.AddVaccinationRecordAsync(id, dto);
        return CreatedAtAction(nameof(GetPetMedicalHistory), new { id }, record);
    }
}