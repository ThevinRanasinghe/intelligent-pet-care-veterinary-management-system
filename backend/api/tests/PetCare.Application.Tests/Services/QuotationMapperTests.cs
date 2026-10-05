using PetCare.Application.DTOs.Billing;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// QuotationMapper owner-facing detail: the bill response must carry the
/// linked prescription medications (all statuses), administration
/// instructions, clinic name and appointment times so the pet owner sees
/// the complete bill + prescription without extra requests.
/// </summary>
public class QuotationMapperTests
{
    private static Quotation BuildQuotation()
    {
        var organization = new Organization { Id = Guid.NewGuid(), Name = "PetCare Colombo 10" };
        var veterinarian = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Silva", Organization = organization };
        var owner = new PetOwner { Id = "OWN-1", FullName = "Amal" };
        var pet = new Pet { Id = "pet-1", Name = "Shadow", Owner = owner, OwnerId = owner.Id };

        var examination = new Examination
        {
            Id = Guid.NewGuid(),
            PetId = pet.Id,
            Pet = pet,
            VeterinarianId = veterinarian.Id,
            Veterinarian = veterinarian,
            VeterinarianCharge = 2500m,
            ExaminationDate = new DateTime(2026, 9, 26, 10, 15, 0)
        };
        var diagnosis = new Diagnosis
        {
            Id = Guid.NewGuid(),
            ExaminationId = examination.Id,
            Examination = examination,
            ConditionName = "Gastritis"
        };
        var treatmentRecord = new TreatmentRecord
        {
            Id = Guid.NewGuid(),
            DiagnosisId = diagnosis.Id,
            Diagnosis = diagnosis,
            ProcedureName = "Bland diet"
        };
        var amoxicillin = new Prescription
        {
            Id = Guid.NewGuid(),
            TreatmentRecordId = treatmentRecord.Id,
            TreatmentRecord = treatmentRecord,
            Dosage = "1 tablet",
            DurationDays = 5,
            Quantity = 10,
            Frequency = "twice daily",
            Route = "Oral",
            Instructions = "Give after food",
            RequestStatus = MedicineRequestStatus.Issued,
            Medicine = new Medicine { Id = Guid.NewGuid(), Name = "Amoxicillin", Strength = "250 mg", DosageForm = "Tablet", UnitPrice = 150m }
        };
        var meloxicam = new Prescription
        {
            Id = Guid.NewGuid(),
            TreatmentRecordId = treatmentRecord.Id,
            TreatmentRecord = treatmentRecord,
            Dosage = "1 tablet",
            DurationDays = 3,
            Quantity = 5,
            Frequency = "once daily",
            Route = "Oral",
            Instructions = "With breakfast",
            RequestStatus = MedicineRequestStatus.Unavailable,
            UnavailableReason = "Out of stock",
            Medicine = new Medicine { Id = Guid.NewGuid(), Name = "Meloxicam", Strength = "1.5 mg", DosageForm = "Tablet", UnitPrice = 90m }
        };
        amoxicillin.MedicineId = amoxicillin.Medicine.Id;
        meloxicam.MedicineId = meloxicam.Medicine.Id;
        treatmentRecord.Prescriptions.Add(amoxicillin);
        treatmentRecord.Prescriptions.Add(meloxicam);
        diagnosis.TreatmentRecords.Add(treatmentRecord);
        examination.Diagnosis = diagnosis;

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PetId = pet.Id,
            Pet = pet,
            VeterinarianId = veterinarian.Id,
            Veterinarian = veterinarian,
            Date = new DateOnly(2026, 9, 26),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
            Examination = examination
        };
        examination.AppointmentId = appointment.Id;

        return new Quotation
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            Appointment = appointment,
            Status = QuotationStatus.Finalised,
            PaymentStatus = PaymentStatus.Paid,
            PaidAt = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
            Items =
            {
                new QuotationItem { Id = Guid.NewGuid(), Category = "Examination", Description = "Veterinarian charge — Dr. Silva", Quantity = 1, UnitPrice = 2500m, TotalPrice = 2500m },
                new QuotationItem { Id = Guid.NewGuid(), Category = "Medicine", Description = "Amoxicillin (1 tablet)", Quantity = 10, UnitPrice = 150m, TotalPrice = 1500m }
            },
            Subtotal = 4000m,
            Total = 4000m
        };
    }

    [Fact]
    public void ToResponse_CarriesConsultationAndPaymentDetails()
    {
        var response = QuotationMapper.ToResponse(BuildQuotation());

        Assert.Equal("PetCare Colombo 10", response.ClinicName);
        Assert.Equal(new TimeOnly(10, 0), response.AppointmentStartTime);
        Assert.Equal(new TimeOnly(11, 0), response.AppointmentEndTime);
        Assert.Equal("Shadow", response.PetName);
        Assert.Equal("Dr. Silva", response.VeterinarianName);
        Assert.Equal("Paid", response.PaymentStatus);
        Assert.Equal(2500m, response.VeterinarianChargeTotal);
        Assert.Equal(1500m, response.MedicineTotal);
        Assert.Equal(4000m, response.Total);
        Assert.StartsWith("INV-", response.InvoiceNumber);
    }

    [Fact]
    public void ToResponse_MedicationsCarryFullAdministrationInstructions()
    {
        var response = QuotationMapper.ToResponse(BuildQuotation());

        Assert.Equal(2, response.Medications.Count);

        var issued = response.Medications[0];
        Assert.Equal("Amoxicillin", issued.MedicineName);
        Assert.Equal("250 mg", issued.MedicineStrength);
        Assert.Equal("Tablet", issued.MedicineDosageForm);
        Assert.Equal("1 tablet", issued.Dosage);
        Assert.Equal("twice daily", issued.Frequency);
        Assert.Equal("Oral", issued.Route);
        Assert.Equal(5, issued.DurationDays);
        Assert.Equal("Give after food", issued.Instructions);
        Assert.Equal(10, issued.Quantity);
        Assert.Equal("Issued", issued.RequestStatus);
        Assert.Equal("Dr. Silva", issued.VeterinarianName);
        Assert.StartsWith("RX-", issued.PrescriptionNumber);

        var unavailable = response.Medications[1];
        Assert.Equal("Meloxicam", unavailable.MedicineName);
        Assert.Equal("Unavailable", unavailable.RequestStatus);
        Assert.Equal("Out of stock", unavailable.UnavailableReason);
    }

    [Fact]
    public void ToResponse_NoExaminationOrPrescriptions_MedicationsEmpty()
    {
        var quotation = new Quotation
        {
            Id = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            Status = QuotationStatus.Draft
        };

        var response = QuotationMapper.ToResponse(quotation);

        Assert.Empty(response.Medications);
        Assert.Null(response.ClinicName);
        Assert.Null(response.AppointmentStartTime);
    }
}
