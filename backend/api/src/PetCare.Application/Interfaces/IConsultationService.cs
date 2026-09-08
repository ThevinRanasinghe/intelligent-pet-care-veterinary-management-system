using PetCare.Application.DTOs;
using PetCare.Domain.Enums;

namespace PetCare.Application.Interfaces;

public interface IConsultationService
{
    // UC-09 to UC-13: Submit Consultation Request (with UC-14 ownership validation)
    Task<ConsultationResponseDto> SubmitConsultationRequestAsync(CreateConsultationRequestDto dto);

    // UC-14: Validate Pet Ownership (Check pet belongs to requesting owner)
    Task<bool> ValidatePetOwnershipAsync(string petId, string ownerId);

    // UC-15: View Requests & Request History
    Task<ConsultationResponseDto> GetConsultationByIdAsync(string id);
    Task<IEnumerable<ConsultationResponseDto>> GetConsultationsByOwnerAsync(string ownerId);
    Task<IEnumerable<ConsultationResponseDto>> GetConsultationsByPetAsync(string petId);
    Task<IEnumerable<ConsultationResponseDto>> GetAllConsultationsAsync(ConsultationStatus? statusFilter = null);

    // UC-16: Track Consultation Status
    Task<ConsultationStatusTrackingDto> GetConsultationStatusAsync(string id);
    Task<ConsultationResponseDto> UpdateConsultationStatusAsync(string id, UpdateConsultationStatusDto dto);

    // UC-17: View Consultation Status History / Audit Trail
    Task<IEnumerable<ConsultationHistoryItemDto>> GetConsultationHistoryAsync(string id);
}
