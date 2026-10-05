using PetCare.Application.DTOs;

namespace PetCare.Application.Interfaces;

public interface ITreatmentRecordService
{
    Task<List<TreatmentRecordResponseDto>> GetAllAsync();
    Task<TreatmentRecordResponseDto?> GetByIdAsync(Guid id);
    Task<List<TreatmentRecordResponseDto>> GetByDiagnosisIdAsync(Guid diagnosisId);
    Task<TreatmentRecordResponseDto> CreateAsync(CreateTreatmentRecordDto dto);
    Task<TreatmentRecordResponseDto?> UpdateAsync(Guid id, UpdateTreatmentRecordDto dto);
    Task<TreatmentRecordResponseDto?> UpdateStatusAsync(Guid id, UpdateTreatmentStatusDto dto); // business-specific
    Task<bool> DeleteAsync(Guid id);
}
