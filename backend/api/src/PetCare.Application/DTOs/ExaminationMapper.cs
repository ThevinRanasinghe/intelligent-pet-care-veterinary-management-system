using PetCare.Domain.Entities;

namespace PetCare.Application.DTOs;

/// <summary>Shared Examination -&gt; ExaminationResponseDto mapping.</summary>
public static class ExaminationMapper
{
    public static ExaminationResponseDto ToDto(Examination examination) => new()
    {
        Id = examination.Id,
        PetId = examination.PetId,
        VeterinarianId = examination.VeterinarianId,
        ConsultationRequestId = examination.ConsultationRequestId,
        AppointmentId = examination.AppointmentId,
        VeterinarianCharge = examination.VeterinarianCharge,
        Symptoms = examination.Symptoms,
        Notes = examination.Notes,
        ExaminationDate = examination.ExaminationDate,
        CreatedAt = examination.CreatedAt
    };
}
