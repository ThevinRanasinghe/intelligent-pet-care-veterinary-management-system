using PetCare.Application.DTOs;

namespace PetCare.Application.Interfaces;

public interface IDiagnosisService
{
    Task<List<DiagnosisResponseDto>> GetAllAsync();
    Task<DiagnosisResponseDto?> GetByIdAsync(Guid id);
    Task<List<DiagnosisResponseDto>> GetByExaminationIdAsync(Guid examinationId);
    Task<DiagnosisResponseDto> CreateAsync(CreateDiagnosisDto dto);
    Task<DiagnosisResponseDto?> UpdateAsync(Guid id, UpdateDiagnosisDto dto);
    Task<bool> DeleteAsync(Guid id);
}
