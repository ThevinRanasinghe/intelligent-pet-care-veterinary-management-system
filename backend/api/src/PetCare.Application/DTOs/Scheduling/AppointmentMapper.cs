using PetCare.Domain.Entities;

namespace PetCare.Application.DTOs.Scheduling;

/// <summary>
/// Shared Appointment -&gt; AppointmentResponse mapping. Denormalised fields
/// (PetName, OwnerName, VeterinarianName, Symptoms, ExaminationId) require
/// the navigation properties to be loaded — repository queries include them.
/// </summary>
public static class AppointmentMapper
{
    public static AppointmentResponse ToResponse(Appointment appointment) => new()
    {
        Id = appointment.Id,
        PetId = appointment.PetId,
        VeterinarianId = appointment.VeterinarianId,
        AppointmentSlotId = appointment.AppointmentSlotId,
        ScheduledStart = appointment.Date.ToDateTime(appointment.StartTime),
        ScheduledEnd = appointment.Date.ToDateTime(appointment.EndTime),
        Status = appointment.Status.ToString(),
        Notes = appointment.Notes,
        ConsultationRequestId = appointment.ConsultationRequestId,
        Type = appointment.Type.ToString(),
        PetName = appointment.Pet?.Name,
        OwnerName = appointment.Pet?.Owner?.FullName,
        VeterinarianName = appointment.Veterinarian?.Name,
        Symptoms = appointment.ConsultationRequest?.SymptomsDescription,
        ExaminationId = appointment.Examination?.Id,
        CreatedAt = appointment.CreatedAt,
        UpdatedAt = appointment.UpdatedAt
    };
}
