using PetCare.Domain.Entities;

namespace PetCare.Application.DTOs;

/// <summary>
/// Shared Prescription -&gt; PrescriptionResponseDto mapping. Denormalised
/// fields require the navigation chain
/// Prescription -&gt; TreatmentRecord -&gt; Diagnosis -&gt; Examination
/// (-&gt; Pet -&gt; Owner, -&gt; Veterinarian) and Medicine to be loaded —
/// repository/service queries include them.
/// </summary>
public static class PrescriptionMapper
{
    public static PrescriptionResponseDto ToDto(Prescription prescription)
    {
        var examination = prescription.TreatmentRecord?.Diagnosis?.Examination;

        return new PrescriptionResponseDto
        {
            Id = prescription.Id,
            TreatmentRecordId = prescription.TreatmentRecordId,
            MedicineId = prescription.MedicineId,
            Dosage = prescription.Dosage,
            DurationDays = prescription.DurationDays,
            Quantity = prescription.Quantity,
            Frequency = prescription.Frequency,
            Route = prescription.Route,
            Instructions = prescription.Instructions,
            RequestStatus = prescription.RequestStatus.ToString(),
            UnavailableReason = prescription.UnavailableReason,
            ReservationId = prescription.ReservationId,
            ProcessedAt = prescription.ProcessedAt,
            CreatedAt = prescription.CreatedAt,
            MedicineName = prescription.Medicine?.Name,
            MedicineStrength = prescription.Medicine?.Strength,
            MedicineDosageForm = prescription.Medicine?.DosageForm,
            MedicineUnitPrice = prescription.Medicine?.UnitPrice,
            PetId = examination?.PetId,
            PetName = examination?.Pet?.Name,
            OwnerName = examination?.Pet?.Owner?.FullName,
            VeterinarianId = examination?.VeterinarianId,
            VeterinarianName = examination?.Veterinarian?.Name,
            ExaminationId = examination?.Id,
            AppointmentId = examination?.AppointmentId,
            VeterinarianCharge = examination?.VeterinarianCharge ?? 0m
        };
    }
}
