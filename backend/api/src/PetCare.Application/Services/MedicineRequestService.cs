using FluentValidation;
using PetCare.Application.DTOs;
using PetCare.Application.DTOs.Inventory;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Enums;

namespace PetCare.Application.Services;

/// <summary>
/// The inventory officer's medicine-request queue: prescriptions are
/// fulfilled (reserve + dispense) or marked unavailable, and each
/// decision refreshes the appointment's bill. Stock rules live in
/// IInventoryService — this service never touches quantities directly.
/// </summary>
public class MedicineRequestService : IMedicineRequestService
{
    private readonly IPrescriptionRepository _prescriptions;
    private readonly IInventoryService _inventory;
    private readonly IBillingService _billing;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _unitOfWork;

    public MedicineRequestService(
        IPrescriptionRepository prescriptions,
        IInventoryService inventory,
        IBillingService billing,
        ITenantContext tenant,
        IUnitOfWork unitOfWork)
    {
        _prescriptions = prescriptions;
        _inventory = inventory;
        _billing = billing;
        _tenant = tenant;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<PrescriptionResponseDto>> GetRequestsAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        MedicineRequestStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<MedicineRequestStatus>(status, ignoreCase: true, out var parsed))
            {
                throw new ValidationException($"Unknown medicine request status '{status}'.");
            }

            filter = parsed;
        }

        var prescriptions = await _prescriptions.GetRequestsAsync(filter, cancellationToken);
        return prescriptions.Select(PrescriptionMapper.ToDto).ToList();
    }

    public async Task<PrescriptionResponseDto> IssueAsync(Guid prescriptionId, CancellationToken cancellationToken = default)
    {
        var prescription = await _prescriptions.GetByIdAsync(prescriptionId, cancellationToken)
            ?? throw new NotFoundException($"Prescription '{prescriptionId}' does not exist.");

        if (prescription.RequestStatus != MedicineRequestStatus.Pending)
        {
            throw new InventoryConflictException(
                $"Only a Pending medicine request can be issued. Current status: '{prescription.RequestStatus}'.");
        }

        var userId = _tenant.UserId
            ?? throw new ForbiddenException("A signed-in account is required to issue medicine requests.");

        // Reserve first — throws InventoryConflictException (409) when stock
        // is insufficient; the prescription stays Pending in that case.
        var reservation = await _inventory.ReserveMedicineAsync(
            new ReserveMedicineRequest
            {
                MedicineId = prescription.MedicineId,
                Quantity = prescription.Quantity,
                ReferenceType = "Prescription",
                ReferenceId = prescription.Id
            },
            userId,
            cancellationToken);

        try
        {
            await _inventory.DispenseReservationAsync(reservation.Id, userId, cancellationToken);
        }
        catch
        {
            // Reserved but not dispensable — release the reservation so stock
            // is not pinned by a failed issue attempt.
            await _inventory.CancelReservationAsync(reservation.Id, userId, cancellationToken);
            throw;
        }

        prescription.RequestStatus = MedicineRequestStatus.Issued;
        prescription.ReservationId = reservation.Id;
        prescription.ProcessedByUserId = userId;
        prescription.ProcessedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RefreshBillAsync(prescription, userId, cancellationToken);

        return PrescriptionMapper.ToDto(prescription);
    }

    public async Task<PrescriptionResponseDto> MarkUnavailableAsync(
        Guid prescriptionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException("Reason is required.");
        }

        var prescription = await _prescriptions.GetByIdAsync(prescriptionId, cancellationToken)
            ?? throw new NotFoundException($"Prescription '{prescriptionId}' does not exist.");

        if (prescription.RequestStatus != MedicineRequestStatus.Pending)
        {
            throw new InventoryConflictException(
                $"Only a Pending medicine request can be marked unavailable. Current status: '{prescription.RequestStatus}'.");
        }

        var userId = _tenant.UserId
            ?? throw new ForbiddenException("A signed-in account is required to process medicine requests.");

        prescription.RequestStatus = MedicineRequestStatus.Unavailable;
        prescription.UnavailableReason = reason.Trim();
        prescription.ProcessedByUserId = userId;
        prescription.ProcessedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RefreshBillAsync(prescription, userId, cancellationToken);

        return PrescriptionMapper.ToDto(prescription);
    }

    private async Task RefreshBillAsync(Domain.Entities.Prescription prescription, Guid userId, CancellationToken cancellationToken)
    {
        var examinationId = prescription.TreatmentRecord?.Diagnosis?.ExaminationId;
        if (examinationId is not null)
        {
            await _billing.GenerateOrRefreshBillForExaminationAsync(examinationId.Value, userId, cancellationToken);
        }
    }
}
