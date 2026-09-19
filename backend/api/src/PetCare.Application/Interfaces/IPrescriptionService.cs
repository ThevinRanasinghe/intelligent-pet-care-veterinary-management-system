using PetCare.Application.DTOs;

namespace PetCare.Application.Interfaces;

public interface IPrescriptionService
{
    Task<List<PrescriptionResponseDto>> GetAllAsync();
    Task<PrescriptionResponseDto?> GetByIdAsync(Guid id);
    Task<List<PrescriptionResponseDto>> GetByTreatmentRecordIdAsync(Guid treatmentRecordId); // business-specific
    Task<PrescriptionResponseDto> CreateAsync(CreatePrescriptionDto dto);
    Task<bool> DeleteAsync(Guid id);
}
