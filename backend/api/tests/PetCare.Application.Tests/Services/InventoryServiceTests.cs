using Moq;
using PetCare.Application.DTOs.Inventory;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Services;

public class InventoryServiceTests
{
    private readonly Mock<IMedicineRepository> _medicineRepository = new();
    private readonly Mock<IMedicineBatchRepository> _batchRepository = new();
    private readonly Mock<IMedicineReservationRepository> _reservationRepository = new();
    private readonly Mock<IInventoryTransactionRepository> _transactionRepository = new();
    private readonly Mock<ISupplierRepository> _supplierRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITenantContext> _tenant = new();

    private static readonly Guid MedicineId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid ReservationId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrganizationId = Guid.NewGuid();

    public InventoryServiceTests()
    {
        // Default: the atomic Reserved→Cancelled/Dispensed transition wins.
        // Conflict tests override this per-test with ReturnsAsync(0) to
        // simulate a concurrent transition having already succeeded.
        _reservationRepository.Setup(r => r.TryTransitionAsync(
                It.IsAny<Guid>(), It.IsAny<ReservationStatus>(), It.IsAny<ReservationStatus>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private InventoryService CreateService()
    {
        _tenant.Setup(t => t.GetOrganizationIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrganizationId);
        _tenant.Setup(t => t.IsOrganizationScoped).Returns(true);

        return new InventoryService(
            _medicineRepository.Object,
            _batchRepository.Object,
            _reservationRepository.Object,
            _transactionRepository.Object,
            _supplierRepository.Object,
            _unitOfWork.Object,
            _tenant.Object);
    }

    #region Reservation Tests

    [Fact]
    public async Task ReserveMedicine_ValidRequest_CreatesReservationAndTransaction()
    {
        var medicine = new Medicine
        {
            Id = MedicineId,
            Name = "Amoxicillin",
            Status = MedicineStatus.Active,
            TotalQuantity = 100,
            ReservedQuantity = 10
        };

        _medicineRepository.Setup(r => r.GetByIdAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicine);
        _medicineRepository.Setup(r => r.TryReserveAsync(MedicineId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService();
        var request = new ReserveMedicineRequest
        {
            MedicineId = MedicineId,
            Quantity = 5,
            ReferenceType = "Appointment",
            ReferenceId = Guid.NewGuid()
        };

        var response = await service.ReserveMedicineAsync(request, UserId);

        Assert.NotNull(response);
        Assert.Equal(MedicineId, response.MedicineId);
        Assert.Equal(5, response.Quantity);
        Assert.Equal(ReservationStatus.Reserved.ToString(), response.Status);

        _reservationRepository.Verify(r => r.AddAsync(It.Is<MedicineReservation>(res =>
            res.MedicineId == MedicineId &&
            res.Quantity == 5 &&
            res.Status == ReservationStatus.Reserved &&
            res.RequestedByUserId == UserId), It.IsAny<CancellationToken>()), Times.Once);

        _transactionRepository.Verify(t => t.AddAsync(It.Is<InventoryTransaction>(tx =>
            tx.MedicineId == MedicineId &&
            tx.Type == InventoryTransactionType.Reservation &&
            tx.QuantityChange == -5 &&
            tx.PerformedByUserId == UserId), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveMedicine_MedicineNotFound_ThrowsNotFoundException()
    {
        _medicineRepository.Setup(r => r.GetByIdAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medicine?)null);

        var service = CreateService();
        var request = new ReserveMedicineRequest { MedicineId = MedicineId, Quantity = 5 };

        await Assert.ThrowsAsync<NotFoundException>(() => service.ReserveMedicineAsync(request, UserId));
    }

    [Fact]
    public async Task ReserveMedicine_InactiveMedicine_ThrowsInventoryConflictException()
    {
        var medicine = new Medicine
        {
            Id = MedicineId,
            Name = "Amoxicillin",
            Status = MedicineStatus.Discontinued,
            TotalQuantity = 100,
            ReservedQuantity = 0
        };

        _medicineRepository.Setup(r => r.GetByIdAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicine);

        var service = CreateService();
        var request = new ReserveMedicineRequest { MedicineId = MedicineId, Quantity = 5 };

        await Assert.ThrowsAsync<InventoryConflictException>(() => service.ReserveMedicineAsync(request, UserId));
    }

    [Fact]
    public async Task ReserveMedicine_InsufficientStockAtomicFailure_ThrowsInventoryConflictException()
    {
        var medicine = new Medicine
        {
            Id = MedicineId,
            Name = "Amoxicillin",
            Status = MedicineStatus.Active,
            TotalQuantity = 10,
            ReservedQuantity = 8
        };

        _medicineRepository.Setup(r => r.GetByIdAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicine);
        // TryReserveAsync returns 0 rows affected when condition (available >= requested) fails
        _medicineRepository.Setup(r => r.TryReserveAsync(MedicineId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateService();
        var request = new ReserveMedicineRequest { MedicineId = MedicineId, Quantity = 5 };

        var ex = await Assert.ThrowsAsync<InventoryConflictException>(() => service.ReserveMedicineAsync(request, UserId));
        Assert.Contains("Insufficient available stock", ex.Message);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task CancelReservation_ActiveReservation_CancelsAndReleasesStock()
    {
        var reservation = new MedicineReservation
        {
            Id = ReservationId,
            MedicineId = MedicineId,
            Quantity = 10,
            Status = ReservationStatus.Reserved
        };

        _reservationRepository.Setup(r => r.GetByIdAsync(ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        _medicineRepository.Setup(r => r.ReleaseReservedAsync(MedicineId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService();
        await service.CancelReservationAsync(ReservationId, UserId);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(UserId, reservation.CancelledByUserId);
        Assert.NotNull(reservation.CancelledAt);

        _medicineRepository.Verify(r => r.ReleaseReservedAsync(MedicineId, 10, It.IsAny<CancellationToken>()), Times.Once);

        _transactionRepository.Verify(t => t.AddAsync(It.Is<InventoryTransaction>(tx =>
            tx.MedicineId == MedicineId &&
            tx.ReservationId == ReservationId &&
            tx.Type == InventoryTransactionType.ReservationRelease &&
            tx.QuantityChange == 10 &&
            tx.PerformedByUserId == UserId), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelReservation_NotFound_ThrowsNotFoundException()
    {
        _reservationRepository.Setup(r => r.GetByIdAsync(ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicineReservation?)null);

        var service = CreateService();
        await Assert.ThrowsAsync<NotFoundException>(() => service.CancelReservationAsync(ReservationId, UserId));
    }

    [Fact]
    public async Task CancelReservation_AlreadyCancelled_ThrowsInventoryConflictException()
    {
        var reservation = new MedicineReservation
        {
            Id = ReservationId,
            MedicineId = MedicineId,
            Quantity = 10,
            Status = ReservationStatus.Cancelled
        };

        _reservationRepository.Setup(r => r.GetByIdAsync(ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        _reservationRepository.Setup(r => r.TryTransitionAsync(
                ReservationId, ReservationStatus.Reserved, ReservationStatus.Cancelled,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateService();
        var ex = await Assert.ThrowsAsync<InventoryConflictException>(() => service.CancelReservationAsync(ReservationId, UserId));
        Assert.Contains("Only an active reservation can be cancelled", ex.Message);
    }

    [Fact]
    public async Task CancelReservation_AlreadyDispensed_ThrowsInventoryConflictException()
    {
        var reservation = new MedicineReservation
        {
            Id = ReservationId,
            MedicineId = MedicineId,
            Quantity = 10,
            Status = ReservationStatus.Dispensed
        };

        _reservationRepository.Setup(r => r.GetByIdAsync(ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        _reservationRepository.Setup(r => r.TryTransitionAsync(
                ReservationId, ReservationStatus.Reserved, ReservationStatus.Cancelled,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateService();
        await Assert.ThrowsAsync<InventoryConflictException>(() => service.CancelReservationAsync(ReservationId, UserId));
    }

    #endregion

    #region Dispense Tests

    [Fact]
    public async Task DispenseReservation_SingleBatch_DispensesSuccessfully()
    {
        var reservation = new MedicineReservation
        {
            Id = ReservationId,
            MedicineId = MedicineId,
            Quantity = 10,
            Status = ReservationStatus.Reserved
        };

        var batch = new MedicineBatch
        {
            Id = Guid.NewGuid(),
            MedicineId = MedicineId,
            Quantity = 20,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            Status = BatchStatus.Active
        };

        var medicine = new Medicine
        {
            Id = MedicineId,
            TotalQuantity = 20,
            ReservedQuantity = 10
        };

        _reservationRepository.Setup(r => r.GetByIdAsync(ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        _batchRepository.Setup(b => b.GetUsableByMedicineOrderedByExpiryAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicineBatch> { batch });
        _medicineRepository.Setup(m => m.GetByIdAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicine);

        var service = CreateService();
        await service.DispenseReservationAsync(ReservationId, UserId);

        Assert.Equal(ReservationStatus.Dispensed, reservation.Status);
        Assert.Equal(10, batch.Quantity); // 20 - 10
        Assert.Equal(10, medicine.TotalQuantity); // 20 - 10
        Assert.Equal(0, medicine.ReservedQuantity); // 10 - 10

        _transactionRepository.Verify(t => t.AddAsync(It.Is<InventoryTransaction>(tx =>
            tx.MedicineId == MedicineId &&
            tx.BatchId == batch.Id &&
            tx.ReservationId == ReservationId &&
            tx.Type == InventoryTransactionType.Dispense &&
            tx.QuantityChange == -10), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispenseReservation_MultipleBatchesFEFO_DispensesFromEarliestFirst()
    {
        var reservation = new MedicineReservation
        {
            Id = ReservationId,
            MedicineId = MedicineId,
            Quantity = 15,
            Status = ReservationStatus.Reserved
        };

        var batch1 = new MedicineBatch
        {
            Id = Guid.NewGuid(),
            MedicineId = MedicineId,
            Quantity = 10,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), // Earlier expiry
            Status = BatchStatus.Active
        };

        var batch2 = new MedicineBatch
        {
            Id = Guid.NewGuid(),
            MedicineId = MedicineId,
            Quantity = 20,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)), // Later expiry
            Status = BatchStatus.Active
        };

        var medicine = new Medicine
        {
            Id = MedicineId,
            TotalQuantity = 30,
            ReservedQuantity = 15
        };

        _reservationRepository.Setup(r => r.GetByIdAsync(ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        _batchRepository.Setup(b => b.GetUsableByMedicineOrderedByExpiryAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicineBatch> { batch1, batch2 });
        _medicineRepository.Setup(m => m.GetByIdAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicine);

        var service = CreateService();
        await service.DispenseReservationAsync(ReservationId, UserId);

        Assert.Equal(ReservationStatus.Dispensed, reservation.Status);
        Assert.Equal(0, batch1.Quantity); // Exhausted (10 taken)
        Assert.Equal(15, batch2.Quantity); // 5 taken (20 - 5)
        Assert.Equal(15, medicine.TotalQuantity); // 30 - 15
        Assert.Equal(0, medicine.ReservedQuantity); // 15 - 15

        _transactionRepository.Verify(t => t.AddAsync(It.Is<InventoryTransaction>(tx =>
            tx.BatchId == batch1.Id && tx.QuantityChange == -10), It.IsAny<CancellationToken>()), Times.Once);

        _transactionRepository.Verify(t => t.AddAsync(It.Is<InventoryTransaction>(tx =>
            tx.BatchId == batch2.Id && tx.QuantityChange == -5), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispenseReservation_InsufficientUsableBatchStock_ThrowsConflictException()
    {
        var reservation = new MedicineReservation
        {
            Id = ReservationId,
            MedicineId = MedicineId,
            Quantity = 25,
            Status = ReservationStatus.Reserved
        };

        var batch1 = new MedicineBatch
        {
            Id = Guid.NewGuid(),
            MedicineId = MedicineId,
            Quantity = 10,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = BatchStatus.Active
        };

        _reservationRepository.Setup(r => r.GetByIdAsync(ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        _batchRepository.Setup(b => b.GetUsableByMedicineOrderedByExpiryAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicineBatch> { batch1 });

        var service = CreateService();
        var ex = await Assert.ThrowsAsync<InventoryConflictException>(() => service.DispenseReservationAsync(ReservationId, UserId));
        Assert.Contains("Reservation quantity exceeds usable batch stock", ex.Message);
    }

    [Fact]
    public async Task DispenseReservation_NotFound_ThrowsNotFoundException()
    {
        _reservationRepository.Setup(r => r.GetByIdAsync(ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicineReservation?)null);

        var service = CreateService();
        await Assert.ThrowsAsync<NotFoundException>(() => service.DispenseReservationAsync(ReservationId, UserId));
    }

    [Fact]
    public async Task DispenseReservation_NotReserved_ThrowsInventoryConflictException()
    {
        var reservation = new MedicineReservation
        {
            Id = ReservationId,
            MedicineId = MedicineId,
            Quantity = 10,
            Status = ReservationStatus.Dispensed
        };

        _reservationRepository.Setup(r => r.GetByIdAsync(ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        _reservationRepository.Setup(r => r.TryTransitionAsync(
                ReservationId, ReservationStatus.Reserved, ReservationStatus.Dispensed,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateService();
        await Assert.ThrowsAsync<InventoryConflictException>(() => service.DispenseReservationAsync(ReservationId, UserId));
    }

    #endregion

    #region Stock In Tests

    [Fact]
    public async Task ReceiveStock_ValidRequest_CreatesBatchAndUpdatesMedicine()
    {
        var medicine = new Medicine
        {
            Id = MedicineId,
            Name = "Cephalexin",
            TotalQuantity = 20
        };

        var supplier = new Supplier
        {
            Id = SupplierId,
            Name = "PharmaCorp",
            Status = SupplierStatus.Active
        };

        _medicineRepository.Setup(r => r.GetByIdAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicine);
        _supplierRepository.Setup(s => s.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

        var request = new ReceiveStockRequest
        {
            SupplierId = SupplierId,
            BatchNumber = "BATCH-001",
            Quantity = 50,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
        };

        var service = CreateService();
        var response = await service.ReceiveStockAsync(MedicineId, request, UserId);

        Assert.NotNull(response);
        Assert.Equal(MedicineId, response.MedicineId);
        Assert.Equal("BATCH-001", response.BatchNumber);
        Assert.Equal(50, response.Quantity);
        Assert.Equal(70, medicine.TotalQuantity); // 20 + 50

        _batchRepository.Verify(b => b.AddAsync(It.Is<MedicineBatch>(batch =>
            batch.MedicineId == MedicineId &&
            batch.SupplierId == SupplierId &&
            batch.BatchNumber == "BATCH-001" &&
            batch.Quantity == 50 &&
            batch.Status == BatchStatus.Active), It.IsAny<CancellationToken>()), Times.Once);

        _transactionRepository.Verify(t => t.AddAsync(It.Is<InventoryTransaction>(tx =>
            tx.MedicineId == MedicineId &&
            tx.Type == InventoryTransactionType.StockIn &&
            tx.QuantityChange == 50 &&
            tx.PerformedByUserId == UserId), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveStock_MedicineNotFound_ThrowsNotFoundException()
    {
        _medicineRepository.Setup(r => r.GetByIdAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medicine?)null);

        var request = new ReceiveStockRequest { SupplierId = SupplierId, Quantity = 10 };
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.ReceiveStockAsync(MedicineId, request, UserId));
    }

    [Fact]
    public async Task ReceiveStock_SupplierNotFound_ThrowsNotFoundException()
    {
        var medicine = new Medicine { Id = MedicineId };
        _medicineRepository.Setup(r => r.GetByIdAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicine);
        _supplierRepository.Setup(s => s.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Supplier?)null);

        var request = new ReceiveStockRequest { SupplierId = SupplierId, Quantity = 10 };
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.ReceiveStockAsync(MedicineId, request, UserId));
    }

    [Fact]
    public async Task ReceiveStock_InactiveSupplier_ThrowsConflictException()
    {
        var medicine = new Medicine { Id = MedicineId };
        var supplier = new Supplier { Id = SupplierId, Status = SupplierStatus.Inactive };

        _medicineRepository.Setup(r => r.GetByIdAsync(MedicineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicine);
        _supplierRepository.Setup(s => s.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

        var request = new ReceiveStockRequest { SupplierId = SupplierId, Quantity = 10 };
        var service = CreateService();

        await Assert.ThrowsAsync<InventoryConflictException>(() => service.ReceiveStockAsync(MedicineId, request, UserId));
    }

    #endregion

    #region CRUD and Queries

    [Fact]
    public async Task CreateMedicine_AddsMedicineAndReturnsResponse()
    {
        var request = new CreateMedicineRequest
        {
            Name = "Metronidazole",
            Category = "Antibiotic",
            Description = "500mg tablets",
            DosageForm = "Tablet",
            Strength = "500mg",
            UnitPrice = 15.50m,
            Manufacturer = "Pfizer",
            ReorderLevel = 10
        };

        var service = CreateService();
        var response = await service.CreateMedicineAsync(request);

        Assert.NotNull(response);
        Assert.Equal("Metronidazole", response.Name);
        Assert.Equal("Antibiotic", response.Category);
        Assert.Equal(15.50m, response.UnitPrice);

        _medicineRepository.Verify(m => m.AddAsync(It.Is<Medicine>(med =>
            med.Name == "Metronidazole" &&
            med.Status == MedicineStatus.Active), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSupplier_AddsSupplierAndReturnsResponse()
    {
        var request = new CreateSupplierRequest
        {
            Name = "MedVet Supplies",
            ContactPerson = "John Doe",
            Phone = "0771234567",
            Email = "john@medvet.lk",
            Address = "Colombo, Sri Lanka"
        };

        var service = CreateService();
        var response = await service.CreateSupplierAsync(request);

        Assert.NotNull(response);
        Assert.Equal("MedVet Supplies", response.Name);
        Assert.Equal("Active", response.Status);

        _supplierRepository.Verify(s => s.AddAsync(It.Is<Supplier>(sup =>
            sup.Name == "MedVet Supplies" &&
            sup.Status == SupplierStatus.Active), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
