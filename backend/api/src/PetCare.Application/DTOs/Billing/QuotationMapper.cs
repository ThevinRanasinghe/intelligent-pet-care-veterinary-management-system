using PetCare.Domain.Entities;

namespace PetCare.Application.DTOs.Billing;

/// <summary>
/// Shared Quotation -&gt; QuotationResponse mapping. The denormalised
/// appointment/pet/owner/veterinarian/examination fields require those
/// navigation properties to be loaded — repository queries include them.
/// </summary>
public static class QuotationMapper
{
    public static QuotationResponse ToResponse(Quotation quotation)
    {
        var appointment = quotation.Appointment;
        var examination = appointment?.Examination;

        return new QuotationResponse
        {
            Id = quotation.Id,
            AppointmentId = quotation.AppointmentId,
            Budget = quotation.Budget,
            Subtotal = quotation.Subtotal,
            Total = quotation.Total,
            IsWithinBudget = quotation.Total <= quotation.Budget,
            Status = quotation.Status.ToString(),
            PaymentStatus = quotation.PaymentStatus.ToString(),
            PaidAt = quotation.PaidAt,
            Items = quotation.Items.Select(ToItemResponse).ToList(),
            AppointmentDate = appointment?.Date,
            AppointmentStartTime = appointment?.StartTime,
            AppointmentEndTime = appointment?.EndTime,
            ClinicName = appointment?.Veterinarian?.Organization?.Name,
            PetId = appointment?.PetId,
            PetName = appointment?.Pet?.Name,
            OwnerId = appointment?.Pet?.OwnerId,
            OwnerName = appointment?.Pet?.Owner?.FullName,
            OwnerEmail = appointment?.Pet?.Owner?.Email,
            OwnerPhone = appointment?.Pet?.Owner?.PhoneNumber,
            VeterinarianId = appointment?.VeterinarianId,
            VeterinarianName = appointment?.Veterinarian?.Name,
            ExaminationId = examination?.Id,
            ExaminationDate = examination?.ExaminationDate,
            VeterinarianChargeTotal = SumCategory(quotation, "Examination"),
            MedicineTotal = SumCategory(quotation, "Medicine"),
            Medications = (examination?.Diagnosis?.TreatmentRecords ?? Enumerable.Empty<TreatmentRecord>())
                .SelectMany(t => t.Prescriptions)
                .OrderBy(p => p.CreatedAt)
                .Select(PrescriptionMapper.ToDto)
                .ToList(),
            CreatedAt = quotation.CreatedAt,
            UpdatedAt = quotation.UpdatedAt
        };
    }

    private static decimal SumCategory(Quotation quotation, string category) =>
        quotation.Items
            .Where(i => i.Category == category)
            .Sum(i => i.TotalPrice);

    private static QuotationItemResponse ToItemResponse(QuotationItem item) => new()
    {
        Id = item.Id,
        Category = item.Category,
        Description = item.Description,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice,
        TotalPrice = item.TotalPrice
    };
}
