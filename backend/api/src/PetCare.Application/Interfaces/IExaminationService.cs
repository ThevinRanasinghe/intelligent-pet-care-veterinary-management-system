using PetCare.Application.DTOs;

namespace PetCare.Application.Interfaces;

public interface IExaminationService
{
    Task<List<ExaminationResponseDto>> GetAllAsync();
    Task<ExaminationResponseDto?> GetByIdAsync(Guid id);
    Task<List<ExaminationResponseDto>> GetByPetIdAsync(Guid petId); // business-specific operation
    Task<ExaminationResponseDto> CreateAsync(CreateExaminationDto dto);
    Task<ExaminationResponseDto?> UpdateAsync(Guid id, UpdateExaminationDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<TreatmentRecommendationDto> GetRecommendationsAsync(Guid examinationId);
}
