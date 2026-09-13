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
    private readonly IUnitOfWork _unitOfWork;

    public InventoryService(
        IMedicineRepository medicines,
        IMedicineBatchRepository batches,
        IMedicineReservationRepository reservations,
        IInventoryTransactionRepository transactions,
        IUnitOfWork unitOfWork)
    {
        _medicines = medicines;
        _batches = batches;
        _reservations = reservations;
        _transactions = transactions;
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
            Quantity = reservation.Quantity,
            Status = reservation.Status.ToString()
        };
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

    // DispenseReservationAsync -> Day 2, step 1 (needs the FEFO batch walk).
}