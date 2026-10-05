using PetCare.Application.DTOs;
using PetCare.Application.DTOs.Inventory;

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

    /// <summary>
    /// Business-specific operation: advisory AI inventory/medicine
    /// analysis via the agentic inventory agent. The identifier is a
    /// treatment record id — the agent analyses that request's
    /// prescription items over the existing API and returns a validated
    /// advisory plan. Nothing is persisted or issued: the inventory
    /// officer reviews the plan and still performs the manual
    /// issue/unavailable action, which the backend validates
    /// authoritatively. Any agent failure returns a safe placeholder
    /// marked Source="unavailable" so fulfillment is never blocked.
    /// </summary>
    Task<InventoryPlanDto> GetInventoryPlanAsync(
        Guid treatmentRecordId,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);
}
