using PetCare.Application.DTOs.Consultations;

namespace PetCare.Application.Interfaces;

public interface IConsultationRequestService
{
    Task<ConsultationRequestDto> CreateAsync(
        CreateConsultationRequestDto dto);

    Task<List<ConsultationRequestDto>> GetAllAsync();

    Task<List<ConsultationRequestDto>> GetByOwnerIdAsync(
        string ownerId);

    Task<ConsultationRequestDto?> GetByIdAsync(
        string id);

    Task<ConsultationRequestDto?> UpdateAsync(
        string id,
        UpdateConsultationRequestDto dto);

    Task<ConsultationRequestDto?> SubmitAsync(
        string id);

    Task<bool> CancelAsync(
        string id);

    Task<bool> ValidateOwnershipAsync(
        string petId,
        string ownerId);
}