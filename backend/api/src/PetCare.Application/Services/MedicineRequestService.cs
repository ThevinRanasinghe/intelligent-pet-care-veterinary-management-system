using FluentValidation;
using Microsoft.Extensions.Logging;
using PetCare.Application.DTOs;
using PetCare.Application.DTOs.Inventory;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Enums;
using System.Text.Json;

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
    private readonly IAgenticClient? _agenticClient;
    private readonly IAgentWorkflowService? _agentWorkflows;
    private readonly ILogger<MedicineRequestService>? _logger;

    public MedicineRequestService(
        IPrescriptionRepository prescriptions,
        IInventoryService inventory,
        IBillingService billing,
        ITenantContext tenant,
        IUnitOfWork unitOfWork,
        IAgenticClient? agenticClient = null,
        IAgentWorkflowService? agentWorkflows = null,
        ILogger<MedicineRequestService>? logger = null)
    {
        _prescriptions = prescriptions;
        _inventory = inventory;
        _billing = billing;
        _tenant = tenant;
        _unitOfWork = unitOfWork;
        _agenticClient = agenticClient;
        _agentWorkflows = agentWorkflows;
        _logger = logger;
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

    /// <summary>
    /// Advisory AI inventory plan for a medicine request (identified by
    /// treatment record). Read-only: verifies the record's prescriptions
    /// are in the caller's organization before invoking the agent, maps
    /// the agent's validated assessment, and returns Source="unavailable"
    /// on any failure — the officer's manual issue/unavailable workflow
    /// is never blocked. No inventory, prescription, or billing writes.
    /// </summary>
    public async Task<InventoryPlanDto> GetInventoryPlanAsync(
        Guid treatmentRecordId,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        // The treatment record must exist as an in-scope medicine request;
        // out-of-scope (cross-organization) records get the same
        // "unavailable" response so existence is never leaked.
        var exists = await _prescriptions.ExistsForTreatmentRecordAsync(treatmentRecordId, cancellationToken);
        if (!exists)
            return UnavailablePlan(treatmentRecordId);

        // When a supervisor workflow is awaiting this event the specialist
        // runs INSIDE the workflow so the trajectory records the step.
        if (_agentWorkflows is not null)
        {
            var workflow = await _agentWorkflows.GetByClinicalEventReferenceAsync(
                Domain.Constants.AgentWorkflowEventType.PrescriptionCreated,
                treatmentRecordId.ToString(), cancellationToken);
            if (workflow?.Status == Domain.Constants.AgentWorkflowStatus.AwaitingPrescription)
            {
                try
                {
                    var advanced = await _agentWorkflows.AdvanceAsync(
                        workflow.Id,
                        Domain.Constants.AgentWorkflowEventType.PrescriptionCreated,
                        treatmentRecordId.ToString(), bearerToken, cancellationToken);
                    var output = advanced.Steps.LastOrDefault()?.Output;
                    if (output is { } outputJson)
                    {
                        var routed = outputJson.Deserialize<InventoryPlanDto>();
                        if (routed is not null && !string.IsNullOrWhiteSpace(routed.PlanningNotes))
                        {
                            routed.Source = "agentic-ai";
                            routed.RequestId = treatmentRecordId.ToString();
                            routed.Confidence = NormalizeConfidence(routed.Confidence);
                            routed.AlternativeMedicines ??= new List<InventoryAlternativeMedicineDto>();
                            return routed;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex,
                        "Workflow advance failed for treatment record {TreatmentRecordId}; falling back to direct agent call",
                        treatmentRecordId);
                }
            }
        }

        if (_agenticClient == null)
            return UnavailablePlan(treatmentRecordId);

        var result = await _agenticClient.PlanInventoryAsync(
            treatmentRecordId.ToString(), bearerToken, cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            return UnavailablePlan(treatmentRecordId);

        InventoryPlanDto? plan;
        try
        {
            plan = JsonSerializer.Deserialize<InventoryPlanDto>(result.Content);
        }
        catch (JsonException)
        {
            _logger?.LogWarning("Inventory agent returned malformed JSON for treatment record {TreatmentRecordId}", treatmentRecordId);
            return UnavailablePlan(treatmentRecordId);
        }

        if (plan == null || string.IsNullOrWhiteSpace(plan.PlanningNotes))
            return UnavailablePlan(treatmentRecordId);

        plan.Source = "agentic-ai";
        plan.RequestId = treatmentRecordId.ToString();
        plan.Confidence = NormalizeConfidence(plan.Confidence);
        plan.AlternativeMedicines ??= new List<InventoryAlternativeMedicineDto>();
        return plan;
    }

    private static string NormalizeConfidence(string? confidence) =>
        confidence?.Trim().ToLowerInvariant() switch
        {
            "moderate" => "Moderate",
            "high" => "High",
            _ => "Low"
        };

    private static InventoryPlanDto UnavailablePlan(Guid treatmentRecordId) => new()
    {
        Source = "unavailable",
        RequestId = treatmentRecordId.ToString(),
        MedicineRecommendation = null,
        RecommendedBatch = null,
        AlternativeMedicines = new List<InventoryAlternativeMedicineDto>(),
        InventorySummary = new InventoryPlanSummaryDto(),
        Confidence = "Low",
        PlanningNotes = "AI inventory analysis is currently unavailable — process the medicine request normally.",
        Disclaimer = "AI-generated medicine and inventory recommendation — deterministic backend validation and authorized staff review are required before any inventory action."
    };

    private async Task RefreshBillAsync(Domain.Entities.Prescription prescription, Guid userId, CancellationToken cancellationToken)
    {
        var examinationId = prescription.TreatmentRecord?.Diagnosis?.ExaminationId;
        if (examinationId is not null)
        {
            await _billing.GenerateOrRefreshBillForExaminationAsync(examinationId.Value, userId, cancellationToken);
        }
    }
}
