using PetCare.Application.DTOs;

namespace PetCare.Application.Interfaces;

public interface IPetService
{
    // UC-05: Add Pet
    Task<PetResponseDto> RegisterPetAsync(CreatePetDto dto);

    // UC-06: View Pet Profile
    Task<PetResponseDto> GetPetByIdAsync(string id);
    Task<IEnumerable<PetResponseDto>> GetPetsByOwnerAsync(string ownerId);
    Task<IEnumerable<PetResponseDto>> GetAllPetsAsync();

    // UC-07: Update Pet Profile (Prevent owners from modifying clinical records)
    Task<PetResponseDto> UpdatePetProfileAsync(string petId, UpdatePetProfileDto dto, string? requestingOwnerId = null);

    // UC-08: View Pet Medical/Vaccination History
    Task<PetMedicalHistoryDto> GetPetMedicalHistoryAsync(string petId);

    // Clinical entries (Restricted to veterinary staff)
    Task<MedicalRecordDto> AddMedicalRecordAsync(string petId, CreateMedicalRecordDto dto);
    Task<VaccinationRecordDto> AddVaccinationRecordAsync(string petId, CreateVaccinationRecordDto dto);
}
