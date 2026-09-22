using PetCare.Application.DTOs.Inventory;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IMedicineRepository _medicines;
    private readonly IMedicineBatchRepository _batches;
    private readonly IMedicineReservationRepository _reservations;
    private readonly IInventoryTransactionRepository _transactions;
    private readonly ISupplierRepository _suppliers;
    private readonly IUnitOfWork _unitOfWork;

    public InventoryService(
        IMedicineRepository medicines,
        IMedicineBatchRepository batches,
        IMedicineReservationRepository reservations,
        IInventoryTransactionRepository transactions,
        ISupplierRepository suppliers,
        IUnitOfWork unitOfWork)
    {
        _medicines = medicines;
        _batches = batches;
        _reservations = reservations;
        _transactions = transactions;
        _suppliers = suppliers;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReservationResponse> ReserveMedicineAsync(
        ReserveMedicineRequest request, Guid requestedByUserId, CancellationToken ct = default)
    {
        var medicine = await _medicines.GetByIdAsync(request.MedicineId, ct)
            ?? throw new NotFoundException($"Medicine '{request.MedicineId}' does not exist.");

        if (medicine.Status != MedicineStatus.Active)
        {
            throw new InventoryConflictException($"Medicine '{medicine.Name}' is not active.");
        }

        // Atomic, single UPDATE ... WHERE — this is what makes rule #10 hold
        // under concurrency: two simultaneous requests for the last 5 units
        // cannot both succeed, because the WHERE clause re-checks available
        // stock against the *current* row, not a value read moments earlier
        // in this request. If the WHERE doesn't match (stock moved under us),
        // affected == 0 and we reject instead of silently reserving anyway.
        var affected = await _medicines.TryReserveAsync(
            medicine.Id, request.Quantity, ct);

        if (affected == 0)
        {
            throw new InventoryConflictException(
                $"Insufficient available stock for '{medicine.Name}': requested {request.Quantity}.");
        }

        var reservation = new MedicineReservation
        {
            MedicineId = medicine.Id,
            Quantity = request.Quantity,
            Status = ReservationStatus.Reserved,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
            RequestedByUserId = requestedByUserId
        };
        await _reservations.AddAsync(reservation, ct);

        await _transactions.AddAsync(new InventoryTransaction
        {
            MedicineId = medicine.Id,
            ReservationId = reservation.Id,
            Reservation = reservation,
            Type = InventoryTransactionType.Reservation,
            QuantityChange = -request.Quantity, // reduces what's available, not physical stock yet
            PerformedByUserId = requestedByUserId,
            OccurredAt = DateTimeOffset.UtcNow
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return new ReservationResponse
        {
            Id = reservation.Id,
            MedicineId = medicine.Id,
            MedicineName = medicine.Name,
            Quantity = reservation.Quantity,
            Status = reservation.Status.ToString(),
            ReferenceType = reservation.ReferenceType,
            ReferenceId = reservation.ReferenceId,
            RequestedByUserId = reservation.RequestedByUserId,
            CreatedAt = reservation.CreatedAt
        };
    }

    public async Task<IReadOnlyList<ReservationResponse>> GetReservationsAsync(CancellationToken ct = default)
    {
        var reservations = await _reservations.GetAllAsync(ct);
        return reservations.Select(r => new ReservationResponse
        {
            Id = r.Id,
            MedicineId = r.MedicineId,
            MedicineName = r.Medicine?.Name,
            Quantity = r.Quantity,
            Status = r.Status.ToString(),
            ReferenceType = r.ReferenceType,
            ReferenceId = r.ReferenceId,
            RequestedByUserId = r.RequestedByUserId,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    public async Task CancelReservationAsync(Guid reservationId, Guid cancelledByUserId, CancellationToken ct = default)
    {
        var reservation = await _reservations.GetByIdAsync(reservationId, ct)
            ?? throw new NotFoundException($"Reservation '{reservationId}' does not exist.");

        if (reservation.Status != ReservationStatus.Reserved)
        {
            throw new InventoryConflictException("Only an active reservation can be cancelled.");
        }

        // Symmetric atomic release — same pattern as reserve, just adding back.
        await _medicines.ReleaseReservedAsync(reservation.MedicineId, reservation.Quantity, ct);

        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancelledByUserId = cancelledByUserId;
        reservation.CancelledAt = DateTimeOffset.UtcNow;

        await _transactions.AddAsync(new InventoryTransaction
        {
            MedicineId = reservation.MedicineId,
            ReservationId = reservation.Id,
            Type = InventoryTransactionType.ReservationRelease,
            QuantityChange = reservation.Quantity,
            PerformedByUserId = cancelledByUserId,
            OccurredAt = DateTimeOffset.UtcNow
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<MedicineBatchResponse> ReceiveStockAsync(
        Guid medicineId, ReceiveStockRequest request, Guid performedByUserId, CancellationToken ct = default)
    {
        var medicine = await _medicines.GetByIdAsync(medicineId, ct)
            ?? throw new NotFoundException($"Medicine '{medicineId}' does not exist.");

        var supplier = await _suppliers.GetByIdAsync(request.SupplierId, ct)
            ?? throw new NotFoundException($"Supplier '{request.SupplierId}' does not exist.");

        if (supplier.Status != SupplierStatus.Active)
        {
            throw new InventoryConflictException($"Supplier '{supplier.Name}' is not active.");
        }

        var batch = new MedicineBatch
        {
            MedicineId = medicineId,
            SupplierId = request.SupplierId,
            BatchNumber = request.BatchNumber,
            Quantity = request.Quantity,
            ExpiryDate = request.ExpiryDate,
            ReceivedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = BatchStatus.Active
        };
        await _batches.AddAsync(batch, ct);

        medicine.TotalQuantity += request.Quantity;

        await _transactions.AddAsync(new InventoryTransaction
        {
            MedicineId = medicineId,
            BatchId = batch.Id,
            Batch = batch,
            Type = InventoryTransactionType.StockIn,
            QuantityChange = request.Quantity,
            PerformedByUserId = performedByUserId,
            OccurredAt = DateTimeOffset.UtcNow
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return new MedicineBatchResponse
        {
            Id = batch.Id,
            MedicineId = medicineId,
            BatchNumber = batch.BatchNumber,
            Quantity = batch.Quantity,
            ExpiryDate = batch.ExpiryDate
        };
    }

    // CreateMedicineAsync / SearchMedicinesAsync / GetLowStockAsync / GetExpiringSoonAsync:
    // straightforward repository calls + mapping, same shape as SchedulingService's
    // GetAppointmentsAsync/GetAppointmentByIdAsync — omitted here since they're pure CRUD.

    public async Task DispenseReservationAsync(Guid reservationId, Guid performedByUserId, CancellationToken ct = default)
{
    var reservation = await _reservations.GetByIdAsync(reservationId, ct)
        ?? throw new NotFoundException($"Reservation '{reservationId}' does not exist.");

    if (reservation.Status != ReservationStatus.Reserved)
    {
        throw new InventoryConflictException("Only an active reservation can be dispensed.");
    }

    // FEFO: usable batches ordered earliest-expiry-first.
    var batches = await _batches.GetUsableByMedicineOrderedByExpiryAsync(reservation.MedicineId, ct);

    var remaining = reservation.Quantity;
    foreach (var batch in batches)
    {
        if (remaining == 0) break;

        var take = Math.Min(remaining, batch.Quantity);
        batch.Quantity -= take;
        remaining -= take;

        await _transactions.AddAsync(new InventoryTransaction
        {
            MedicineId = reservation.MedicineId,
            BatchId = batch.Id,
            ReservationId = reservation.Id,
            Type = InventoryTransactionType.Dispense,
            QuantityChange = -take,
            PerformedByUserId = performedByUserId,
            OccurredAt = DateTimeOffset.UtcNow
        }, ct);
    }

    if (remaining > 0)
    {
        // Reserved count and real batch stock disagree — a data problem,
        // not something the caller can fix by retrying. Surface it loudly
        // rather than silently under-dispensing.
        throw new InventoryConflictException(
            $"Reservation quantity exceeds usable batch stock by {remaining} units.");
    }

    var medicine = await _medicines.GetByIdAsync(reservation.MedicineId, ct)!;
    medicine!.TotalQuantity -= reservation.Quantity;
    medicine.ReservedQuantity -= reservation.Quantity;

    reservation.Status = ReservationStatus.Dispensed;

    await _unitOfWork.SaveChangesAsync(ct); // one transaction: batch decrements + medicine counters + status
}

public async Task<MedicineResponse> CreateMedicineAsync(CreateMedicineRequest request, CancellationToken ct = default)
{
    var medicine = new Medicine
    {
        Name = request.Name,
        Category = request.Category,
        Description = request.Description,
        DosageForm = request.DosageForm,
        Strength = request.Strength,
        UnitPrice = request.UnitPrice,
        Manufacturer = request.Manufacturer,
        ReorderLevel = request.ReorderLevel,
        Status = MedicineStatus.Active
    };

    await _medicines.AddAsync(medicine, ct);
    await _unitOfWork.SaveChangesAsync(ct);

    return ToResponse(medicine);
}

public async Task<MedicineResponse?> GetMedicineByIdAsync(Guid id, CancellationToken ct = default)
{
    var medicine = await _medicines.GetByIdAsync(id, ct);
    return medicine is null ? null : ToResponse(medicine);
}

public async Task<PagedResult<MedicineResponse>> SearchMedicinesAsync(SearchMedicinesRequest request, CancellationToken ct = default)
{
    var (items, total) = await _medicines.SearchAsync(
        request.Search, request.Category, request.LowStockOnly,
        request.Page, request.PageSize, ct);

    return new PagedResult<MedicineResponse>
    {
        Items = items.Select(ToResponse).ToList(),
        Total = total,
        Page = request.Page,
        PageSize = request.PageSize
    };
}

public async Task<IReadOnlyList<MedicineResponse>> GetLowStockAsync(CancellationToken ct = default)
{
    var (items, _) = await _medicines.SearchAsync(
        search: null, category: null, lowStockOnly: true,
        page: 1, pageSize: int.MaxValue, ct);

    return items.Select(ToResponse).ToList();
}

public async Task<IReadOnlyList<MedicineBatchResponse>> GetExpiringSoonAsync(int withinDays, CancellationToken ct = default)
{
    var batches = await _batches.GetExpiringSoonAsync(withinDays, ct);
    return batches.Select(ToResponse).ToList();
}

public async Task<IReadOnlyList<MedicineBatchResponse>> GetBatchesForMedicineAsync(Guid medicineId, CancellationToken ct = default)
{
    var batches = await _batches.GetByMedicineAsync(medicineId, ct);
    return batches.Select(ToResponse).ToList();
}

public async Task<IReadOnlyList<InventoryTransactionResponse>> GetTransactionsAsync(Guid medicineId, CancellationToken ct = default)
{
    var transactions = await _transactions.GetByMedicineAsync(medicineId, ct);
    return transactions.Select(t => new InventoryTransactionResponse
    {
        Id = t.Id,
        MedicineId = t.MedicineId,
        BatchId = t.BatchId,
        ReservationId = t.ReservationId,
        Type = t.Type.ToString(),
        QuantityChange = t.QuantityChange,
        PerformedByUserId = t.PerformedByUserId,
        Notes = t.Notes,
        OccurredAt = t.OccurredAt
    }).ToList();
}

public async Task<SupplierResponse> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken ct = default)
{
    var supplier = new Supplier
    {
        Name = request.Name,
        ContactPerson = request.ContactPerson,
        Phone = request.Phone,
        Email = request.Email,
        Address = request.Address,
        Status = SupplierStatus.Active
    };

    await _suppliers.AddAsync(supplier, ct);
    await _unitOfWork.SaveChangesAsync(ct);

    return ToResponse(supplier);
}

public async Task<IReadOnlyList<SupplierResponse>> GetSuppliersAsync(CancellationToken ct = default)
{
    var suppliers = await _suppliers.GetAllAsync(ct);
    return suppliers.Select(ToResponse).ToList();
}

public async Task<SupplierResponse?> GetSupplierByIdAsync(Guid id, CancellationToken ct = default)
{
    var supplier = await _suppliers.GetByIdAsync(id, ct);
    return supplier is null ? null : ToResponse(supplier);
}

private static MedicineResponse ToResponse(Medicine m) => new()
{
    Id = m.Id,
    Name = m.Name,
    Category = m.Category,
    Description = m.Description,
    DosageForm = m.DosageForm,
    Strength = m.Strength,
    UnitPrice = m.UnitPrice,
    Manufacturer = m.Manufacturer,
    Status = m.Status.ToString(),
    ReorderLevel = m.ReorderLevel,
    TotalQuantity = m.TotalQuantity,
    ReservedQuantity = m.ReservedQuantity,
    AvailableQuantity = m.AvailableQuantity,
    IsLowStock = m.IsLowStock,
    CreatedAt = m.CreatedAt,
    UpdatedAt = m.UpdatedAt
};

private static MedicineBatchResponse ToResponse(MedicineBatch b) => new()
{
    Id = b.Id,
    MedicineId = b.MedicineId,
    SupplierId = b.SupplierId,
    BatchNumber = b.BatchNumber,
    Quantity = b.Quantity,
    ExpiryDate = b.ExpiryDate,
    ReceivedDate = b.ReceivedDate,
    Status = b.Status.ToString(),
    IsExpired = b.ExpiryDate <= DateOnly.FromDateTime(DateTime.UtcNow)
};

private static SupplierResponse ToResponse(Supplier s) => new()
{
    Id = s.Id,
    Name = s.Name,
    ContactPerson = s.ContactPerson,
    Phone = s.Phone,
    Email = s.Email,
    Address = s.Address,
    Status = s.Status.ToString()
};
}