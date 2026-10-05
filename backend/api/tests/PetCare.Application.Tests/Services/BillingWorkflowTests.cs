using FluentValidation;
using FluentValidation.Results;
using Moq;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// Unit tests for BillingService's workflow additions: automatic bill
/// generation from a completed examination (Examination + Issued-Medicine
/// lines only), paid-bill immutability, mark-paid transitions, and the
/// pet-owner "my bills" view.
/// </summary>
public class BillingWorkflowTests
{
    private readonly Mock<IQuotationRepository> _quotationRepository = new();
    private readonly Mock<IAppointmentRepository> _appointmentRepository = new();
    private readonly Mock<IExaminationRepository> _examinationRepository = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CreateQuotationRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateQuotationRequest>> _updateValidator = new();

    private static readonly Guid ExaminationId = Guid.NewGuid();
    private static readonly Guid AppointmentId = Guid.NewGuid();
    private static readonly Guid QuotationId = Guid.NewGuid();
    private static readonly Guid OfficerUserId = Guid.NewGuid();

    private BillingService CreateService() => new(
        _quotationRepository.Object,
        _appointmentRepository.Object,
        _examinationRepository.Object,
        _tenant.Object,
        _unitOfWork.Object,
        _createValidator.Object,
        _updateValidator.Object);

    /// <summary>
    /// An examination on an appointment with one Issued prescription
    /// (Amoxicillin x3 @ 100), one Pending and one Unavailable prescription.
    /// </summary>
    private static Examination ExaminationWithAppointment() => new()
    {
        Id = ExaminationId,
        PetId = "PET-TEST0001",
        VeterinarianId = Guid.NewGuid(),
        AppointmentId = AppointmentId,
        VeterinarianCharge = 2500m,
        Symptoms = "Fever",
        ExaminationDate = DateTime.UtcNow,
        Veterinarian = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Test Vet" },
        Diagnosis = new Diagnosis
        {
            Id = Guid.NewGuid(),
            ExaminationId = ExaminationId,
            ConditionName = "Infection",
            TreatmentRecords = new List<TreatmentRecord>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    DiagnosisId = Guid.NewGuid(),
                    ProcedureName = "Medication",
                    Prescriptions = new List<Prescription>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            MedicineId = Guid.NewGuid(),
                            Dosage = "1 pill twice daily",
                            DurationDays = 7,
                            Quantity = 3,
                            RequestStatus = MedicineRequestStatus.Issued,
                            Medicine = new Medicine { Id = Guid.NewGuid(), Name = "Amoxicillin", UnitPrice = 100m }
                        },
                        new()
                        {
                            Id = Guid.NewGuid(),
                            MedicineId = Guid.NewGuid(),
                            Dosage = "1 pill daily",
                            DurationDays = 5,
                            Quantity = 5,
                            RequestStatus = MedicineRequestStatus.Pending,
                            Medicine = new Medicine { Id = Guid.NewGuid(), Name = "Ibuprofen", UnitPrice = 50m }
                        },
                        new()
                        {
                            Id = Guid.NewGuid(),
                            MedicineId = Guid.NewGuid(),
                            Dosage = "spray",
                            DurationDays = 3,
                            Quantity = 1,
                            RequestStatus = MedicineRequestStatus.Unavailable,
                            Medicine = new Medicine { Id = Guid.NewGuid(), Name = "Topical", UnitPrice = 800m }
                        }
                    }
                }
            }
        }
    };

    // 1. Bill generation builds the Examination line (vet charge) and one
    //    Medicine line per Issued prescription with server-computed totals;
    //    Pending and Unavailable prescriptions are excluded.
    [Fact]
    public async Task GenerateBill_ExaminationWithIssuedMedicines_BuildsExaminationAndMedicineLines()
    {
        var examination = ExaminationWithAppointment();
        _examinationRepository.Setup(r => r.GetByIdAsync(ExaminationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(examination);
        _quotationRepository.Setup(r => r.GetByAppointmentIdAsync(AppointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Quotation?)null);

        var service = CreateService();
        var result = await service.GenerateOrRefreshBillForExaminationAsync(ExaminationId, OfficerUserId);

        Assert.NotNull(result);
        // Examination line: 1 x 2500, Medicine line: 3 x 100 = 300.
        Assert.Equal(2800m, result.Subtotal);
        Assert.Equal(2800m, result.Total);
        Assert.Equal(QuotationStatus.Finalised.ToString(), result.Status);
        Assert.Equal(PaymentStatus.Pending.ToString(), result.PaymentStatus);
        Assert.Equal(2500m, result.VeterinarianChargeTotal);
        Assert.Equal(300m, result.MedicineTotal);

        Assert.Equal(2, result.Items.Count);
        var examLine = Assert.Single(result.Items, i => i.Category == "Examination");
        Assert.Contains("Dr. Test Vet", examLine.Description);
        Assert.Equal(2500m, examLine.TotalPrice);
        var medLine = Assert.Single(result.Items, i => i.Category == "Medicine");
        Assert.Contains("Amoxicillin", medLine.Description);
        Assert.Equal(3, medLine.Quantity);
        Assert.Equal(300m, medLine.TotalPrice);

        _quotationRepository.Verify(r => r.AddAsync(
            It.Is<Quotation>(q => q.AppointmentId == AppointmentId), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // 2. An examination with no appointment produces no bill.
    [Fact]
    public async Task GenerateBill_ExaminationWithoutAppointment_Skips()
    {
        var examination = ExaminationWithAppointment();
        examination.AppointmentId = null;
        _examinationRepository.Setup(r => r.GetByIdAsync(ExaminationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(examination);

        var service = CreateService();
        var result = await service.GenerateOrRefreshBillForExaminationAsync(ExaminationId, OfficerUserId);

        Assert.Null(result);
        _quotationRepository.Verify(r => r.AddAsync(It.IsAny<Quotation>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // 3. A bill that is already Paid is never modified by regeneration.
    [Fact]
    public async Task GenerateBill_PaidQuotation_LeftUntouched()
    {
        var examination = ExaminationWithAppointment();
        var paidQuotation = new Quotation
        {
            Id = QuotationId,
            AppointmentId = AppointmentId,
            Status = QuotationStatus.Finalised,
            PaymentStatus = PaymentStatus.Paid,
            PaidAt = DateTimeOffset.UtcNow,
            Total = 999m,
            Subtotal = 999m,
            Items = new List<QuotationItem>
            {
                new() { Id = Guid.NewGuid(), QuotationId = QuotationId, Category = "Examination", Description = "Old", Quantity = 1, UnitPrice = 999m, TotalPrice = 999m }
            }
        };

        _examinationRepository.Setup(r => r.GetByIdAsync(ExaminationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(examination);
        _quotationRepository.Setup(r => r.GetByAppointmentIdAsync(AppointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paidQuotation);

        var service = CreateService();
        var result = await service.GenerateOrRefreshBillForExaminationAsync(ExaminationId, OfficerUserId);

        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Paid.ToString(), result.PaymentStatus);
        // Items were not regenerated.
        Assert.Single(paidQuotation.Items);
        Assert.Equal(999m, paidQuotation.Total);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // 4. Mark-paid transitions a Finalised, Pending quotation to Paid.
    [Fact]
    public async Task MarkPaidAsync_FinalisedPending_SetsPaid()
    {
        _tenant.Setup(t => t.UserId).Returns(OfficerUserId);
        var quotation = new Quotation
        {
            Id = QuotationId,
            AppointmentId = AppointmentId,
            Status = QuotationStatus.Finalised,
            PaymentStatus = PaymentStatus.Pending
        };
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotation);

        var service = CreateService();
        var result = await service.MarkPaidAsync(QuotationId);

        Assert.Equal(PaymentStatus.Paid, quotation.PaymentStatus);
        Assert.NotNull(quotation.PaidAt);
        Assert.Equal(OfficerUserId, quotation.PaidByUserId);
        Assert.Equal("Paid", result.PaymentStatus);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // 5. Marking an already-paid quotation paid again is a conflict.
    [Fact]
    public async Task MarkPaidAsync_AlreadyPaid_ThrowsConflict()
    {
        _tenant.Setup(t => t.UserId).Returns(OfficerUserId);
        var quotation = new Quotation
        {
            Id = QuotationId,
            AppointmentId = AppointmentId,
            Status = QuotationStatus.Finalised,
            PaymentStatus = PaymentStatus.Paid,
            PaidAt = DateTimeOffset.UtcNow
        };
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotation);

        var service = CreateService();

        await Assert.ThrowsAsync<BillingConflictException>(() => service.MarkPaidAsync(QuotationId));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // 6. Mark-paid on a non-Finalised quotation is a conflict.
    [Fact]
    public async Task MarkPaidAsync_DraftQuotation_ThrowsConflict()
    {
        _tenant.Setup(t => t.UserId).Returns(OfficerUserId);
        var quotation = new Quotation
        {
            Id = QuotationId,
            AppointmentId = AppointmentId,
            Status = QuotationStatus.Draft,
            PaymentStatus = PaymentStatus.Pending
        };
        _quotationRepository.Setup(r => r.GetByIdAsync(QuotationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotation);

        var service = CreateService();

        await Assert.ThrowsAsync<BillingConflictException>(() => service.MarkPaidAsync(QuotationId));
    }

    // 7. The owner "my bills" view queries by the caller's owner id —
    //    filtering happens in the repository (not org-scoped).
    [Fact]
    public async Task GetQuotationsForOwnerAsync_QueriesByOwnerId()
    {
        var quotations = new List<Quotation>
        {
            new()
            {
                Id = Guid.NewGuid(),
                AppointmentId = Guid.NewGuid(),
                Status = QuotationStatus.Finalised,
                PaymentStatus = PaymentStatus.Paid
            }
        };
        _quotationRepository.Setup(r => r.GetByOwnerAsync("OWN-TEST0001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotations);

        var service = CreateService();
        var result = await service.GetQuotationsForOwnerAsync("OWN-TEST0001");

        Assert.Single(result);
        _quotationRepository.Verify(r => r.GetByOwnerAsync("OWN-TEST0001", It.IsAny<CancellationToken>()), Times.Once);
    }
}
