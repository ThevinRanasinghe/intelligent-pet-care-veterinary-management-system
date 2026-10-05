using Moq;
using PetCare.Application.DTOs.Inventory;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// Unit tests for MedicineRequestService: the inventory officer's
/// issue/unavailable workflow over Pending prescriptions — reserve then
/// dispense through IInventoryService, status/reservation bookkeeping, and
/// bill refresh. All dependencies are mocked.
/// </summary>
public class MedicineRequestServiceTests
{
    private readonly Mock<IPrescriptionRepository> _prescriptions = new();
    private readonly Mock<IInventoryService> _inventory = new();
    private readonly Mock<IBillingService> _billing = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static readonly Guid PrescriptionId = Guid.NewGuid();
    private static readonly Guid MedicineId = Guid.NewGuid();
    private static readonly Guid ReservationId = Guid.NewGuid();
    private static readonly Guid OfficerUserId = Guid.NewGuid();
    private static readonly Guid ExaminationId = Guid.NewGuid();

    public MedicineRequestServiceTests()
    {
        _tenant.Setup(t => t.UserId).Returns(OfficerUserId);
    }

    private MedicineRequestService CreateService() => new(
        _prescriptions.Object,
        _inventory.Object,
        _billing.Object,
        _tenant.Object,
        _unitOfWork.Object);

    private static Prescription PendingPrescription() => new()
    {
        Id = PrescriptionId,
        MedicineId = MedicineId,
        Dosage = "1 pill daily",
        DurationDays = 7,
        Quantity = 3,
        RequestStatus = MedicineRequestStatus.Pending,
        TreatmentRecord = new TreatmentRecord
        {
            Id = Guid.NewGuid(),
            DiagnosisId = Guid.NewGuid(),
            ProcedureName = "Check",
            Diagnosis = new Diagnosis
            {
                Id = Guid.NewGuid(),
                ExaminationId = ExaminationId,
                ConditionName = "Condition"
            }
        }
    };

    // 1. Issue reserves then dispenses, marks the prescription Issued and
    //    links the reservation, then refreshes the bill.
    [Fact]
    public async Task IssueAsync_Pending_ReservesDispensesAndMarksIssued()
    {
        var prescription = PendingPrescription();
        _prescriptions.Setup(r => r.GetByIdAsync(PrescriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prescription);
        _inventory
            .Setup(i => i.ReserveMedicineAsync(
                It.Is<ReserveMedicineRequest>(r =>
                    r.MedicineId == MedicineId &&
                    r.Quantity == prescription.Quantity &&
                    r.ReferenceType == "Prescription" &&
                    r.ReferenceId == PrescriptionId),
                OfficerUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReservationResponse { Id = ReservationId, MedicineId = MedicineId, Quantity = 3 });

        var service = CreateService();
        var result = await service.IssueAsync(PrescriptionId);

        Assert.Equal(MedicineRequestStatus.Issued, prescription.RequestStatus);
        Assert.Equal(ReservationId, prescription.ReservationId);
        Assert.Equal(OfficerUserId, prescription.ProcessedByUserId);
        Assert.NotNull(prescription.ProcessedAt);
        Assert.Equal("Issued", result.RequestStatus);
        Assert.Equal(ReservationId, result.ReservationId);

        // Reserve happens before Dispense.
        _inventory.Verify(i => i.ReserveMedicineAsync(
            It.IsAny<ReserveMedicineRequest>(), OfficerUserId, It.IsAny<CancellationToken>()), Times.Once);
        _inventory.Verify(i => i.DispenseReservationAsync(ReservationId, OfficerUserId, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _billing.Verify(b => b.GenerateOrRefreshBillForExaminationAsync(
            ExaminationId, OfficerUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // 2. Insufficient stock: the reservation throws, the exception
    //    propagates and the prescription stays Pending (no dispense, no
    //    status change, no bill refresh).
    [Fact]
    public async Task IssueAsync_InsufficientStock_PropagatesConflictAndStaysPending()
    {
        var prescription = PendingPrescription();
        _prescriptions.Setup(r => r.GetByIdAsync(PrescriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prescription);
        _inventory
            .Setup(i => i.ReserveMedicineAsync(
                It.IsAny<ReserveMedicineRequest>(), OfficerUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryConflictException("Insufficient stock."));

        var service = CreateService();

        await Assert.ThrowsAsync<InventoryConflictException>(() => service.IssueAsync(PrescriptionId));

        Assert.Equal(MedicineRequestStatus.Pending, prescription.RequestStatus);
        _inventory.Verify(i => i.DispenseReservationAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _billing.Verify(b => b.GenerateOrRefreshBillForExaminationAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 3. Only Pending requests can be issued — an already-issued request is
    //    rejected with a conflict.
    [Theory]
    [InlineData(MedicineRequestStatus.Issued)]
    [InlineData(MedicineRequestStatus.Unavailable)]
    public async Task IssueAsync_NonPending_ThrowsConflict(MedicineRequestStatus status)
    {
        var prescription = PendingPrescription();
        prescription.RequestStatus = status;
        _prescriptions.Setup(r => r.GetByIdAsync(PrescriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prescription);

        var service = CreateService();

        await Assert.ThrowsAsync<InventoryConflictException>(() => service.IssueAsync(PrescriptionId));

        _inventory.Verify(i => i.ReserveMedicineAsync(
            It.IsAny<ReserveMedicineRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 4. Marking unavailable requires a non-empty reason.
    [Fact]
    public async Task MarkUnavailableAsync_EmptyReason_ThrowsValidation()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => service.MarkUnavailableAsync(PrescriptionId, "   "));

        _prescriptions.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 5. A pending request marked unavailable stores the reason and
    //    processing audit, then refreshes the bill.
    [Fact]
    public async Task MarkUnavailableAsync_Pending_SetsUnavailableAndReason()
    {
        var prescription = PendingPrescription();
        _prescriptions.Setup(r => r.GetByIdAsync(PrescriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prescription);

        var service = CreateService();
        var result = await service.MarkUnavailableAsync(PrescriptionId, "Out of stock until next week");

        Assert.Equal(MedicineRequestStatus.Unavailable, prescription.RequestStatus);
        Assert.Equal("Out of stock until next week", prescription.UnavailableReason);
        Assert.Equal(OfficerUserId, prescription.ProcessedByUserId);
        Assert.NotNull(prescription.ProcessedAt);
        Assert.Equal("Unavailable", result.RequestStatus);
        Assert.Equal("Out of stock until next week", result.UnavailableReason);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _billing.Verify(b => b.GenerateOrRefreshBillForExaminationAsync(
            ExaminationId, OfficerUserId, It.IsAny<CancellationToken>()), Times.Once);
        _inventory.Verify(i => i.ReserveMedicineAsync(
            It.IsAny<ReserveMedicineRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 6. The request list maps the filtered repository result.
    [Fact]
    public async Task GetRequestsAsync_ParsesStatusAndMapsResults()
    {
        _prescriptions.Setup(r => r.GetRequestsAsync(MedicineRequestStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Prescription> { PendingPrescription() });

        var service = CreateService();
        var results = await service.GetRequestsAsync("pending");

        Assert.Single(results);
        Assert.Equal("Pending", results[0].RequestStatus);
        _prescriptions.Verify(r => r.GetRequestsAsync(MedicineRequestStatus.Pending, It.IsAny<CancellationToken>()), Times.Once);
    }
}
