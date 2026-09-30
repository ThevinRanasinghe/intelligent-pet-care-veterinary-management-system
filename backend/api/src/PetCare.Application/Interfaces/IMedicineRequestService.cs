using PetCare.Application.DTOs;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Medicine-request workflow over prescriptions: the inventory officer's
/// queue, issuing stock against a prescription (reserve + dispense), and
/// marking a request unavailable. Issuing/marking regenerates the bill for
/// the underlying examination's appointment.
/// </summary>
public interface IMedicineRequestService
{
    /// <summary>
    /// Prescriptions in the caller's organization, optionally filtered by
    /// request status ("Pending" | "Issued" | "Unavailable"), newest first.
    /// </summary>
    Task<IReadOnlyList<PrescriptionResponseDto>> GetRequestsAsync(
        string? status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues stock for a Pending prescription: atomically reserves then
    /// dispenses via IInventoryService, marks the request Issued and links
    /// the reservation, then refreshes the appointment bill.
    /// </summary>
    Task<PrescriptionResponseDto> IssueAsync(Guid prescriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a Pending prescription Unavailable with a required reason, then
    /// refreshes the appointment bill.
    /// </summary>
    Task<PrescriptionResponseDto> MarkUnavailableAsync(
        Guid prescriptionId,
        string reason,
        CancellationToken cancellationToken = default);
}
