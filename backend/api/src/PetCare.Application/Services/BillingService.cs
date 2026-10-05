using FluentValidation;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Application.Services;

/// <summary>
/// Billing/Quotation business logic. This is the single source of truth for
/// quotation creation/update/calculation/submission/finalisation rules,
/// shared by every client through ASP.NET Core. See
/// docs/database/scheduling-billing-approval-domain-model.md#billing for the
/// underlying business rules.
///
/// The backend is authoritative for Subtotal/Total: these are always
/// recomputed here from the persisted/submitted line items and are never
/// trusted from client input.
/// </summary>
public class BillingService : IBillingService
{
    private readonly IQuotationRepository _quotationRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IExaminationRepository _examinationRepository;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateQuotationRequest> _createValidator;
    private readonly IValidator<UpdateQuotationRequest> _updateValidator;

    public BillingService(
        IQuotationRepository quotationRepository,
        IAppointmentRepository appointmentRepository,
        IExaminationRepository examinationRepository,
        ITenantContext tenant,
        IUnitOfWork unitOfWork,
        IValidator<CreateQuotationRequest> createValidator,
        IValidator<UpdateQuotationRequest> updateValidator)
    {
        _quotationRepository = quotationRepository;
        _appointmentRepository = appointmentRepository;
        _examinationRepository = examinationRepository;
        _tenant = tenant;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<QuotationResponse>> GetQuotationsAsync(CancellationToken cancellationToken = default)
    {
        var quotations = await _quotationRepository.GetAllAsync(cancellationToken);
        return quotations.Select(ToResponse).ToList();
    }

    public async Task<QuotationResponse?> GetQuotationByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await _quotationRepository.GetByIdAsync(id, cancellationToken);
        return quotation is null ? null : ToResponse(quotation);
    }

    public async Task<QuotationResponse> CreateQuotationAsync(CreateQuotationRequest request, CancellationToken cancellationToken = default)
    {
        // Structural/referential validation (appointment exists, 1:1 rule,
        // budget/item shape).
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var items = BuildItems(request.Items);
        var subtotal = CalculateSubtotal(items);

        var quotation = new Quotation
        {
            AppointmentId = request.AppointmentId,
            Budget = request.Budget,
            Subtotal = subtotal,
            Total = subtotal,
            Status = QuotationStatus.Draft,
            Items = items
        };

        await _quotationRepository.AddAsync(quotation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(quotation);
    }

    public async Task<QuotationResponse> UpdateQuotationAsync(Guid id, UpdateQuotationRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var quotation = await _quotationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Quotation '{id}' does not exist.");

        EnsureEditable(quotation);

        // Replace the full line item set; EF Core's configured cascade delete
        // (QuotationConfiguration: HasMany(Items).OnDelete(Cascade)) removes
        // the old rows when the collection is cleared.
        quotation.Items.Clear();
        foreach (var item in BuildItems(request.Items))
        {
            quotation.Items.Add(item);
        }

        quotation.Budget = request.Budget;
        quotation.Subtotal = CalculateSubtotal(quotation.Items);
        quotation.Total = quotation.Subtotal;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(quotation);
    }

    public async Task<QuotationResponse> CalculateQuotationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await _quotationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Quotation '{id}' does not exist.");

        quotation.Subtotal = CalculateSubtotal(quotation.Items);
        quotation.Total = quotation.Subtotal;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(quotation);
    }

    public async Task<QuotationResponse> SubmitQuotationForApprovalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await _quotationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Quotation '{id}' does not exist.");

        if (quotation.Status != QuotationStatus.Draft && quotation.Status != QuotationStatus.RevisionRequested)
        {
            throw new BillingConflictException(
                $"Only quotations in Draft or RevisionRequested status can be submitted for approval. Current status: '{quotation.Status}'.");
        }

        // Recompute before checking the budget so the comparison always
        // reflects the current line items, not a stale cached total.
        quotation.Subtotal = CalculateSubtotal(quotation.Items);
        quotation.Total = quotation.Subtotal;

        if (quotation.Total > quotation.Budget)
        {
            throw new BillingConflictException(
                $"Quotation total ({quotation.Total}) exceeds the owner's budget ({quotation.Budget}) and cannot be submitted for approval.");
        }

        quotation.Status = QuotationStatus.PendingApproval;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(quotation);
    }

    public async Task<QuotationResponse> FinalizeQuotationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await _quotationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Quotation '{id}' does not exist.");

        if (quotation.Status != QuotationStatus.Approved)
        {
            throw new BillingConflictException(
                $"Only Approved quotations can be finalised. Current status: '{quotation.Status}'.");
        }

        quotation.Status = QuotationStatus.Finalised;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(quotation);
    }

    public async Task<QuotationResponse?> GenerateOrRefreshBillForExaminationAsync(
        Guid examinationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var examination = await _examinationRepository.GetByIdAsync(examinationId, cancellationToken);

        // No appointment means there is nothing to bill against — this is a
        // normal outcome for standalone examinations, not an error.
        if (examination?.AppointmentId is null)
        {
            return null;
        }

        var quotation = await _quotationRepository.GetByAppointmentIdAsync(
            examination.AppointmentId.Value, cancellationToken);

        // A paid bill is a financial record — never mutate it afterwards.
        if (quotation is not null && quotation.PaymentStatus == PaymentStatus.Paid)
        {
            return ToResponse(quotation);
        }

        var items = BuildBillItems(examination);

        if (quotation is null)
        {
            quotation = new Quotation
            {
                AppointmentId = examination.AppointmentId.Value,
                Items = items
            };
            await _quotationRepository.AddAsync(quotation, cancellationToken);
        }
        else
        {
            // Replace the line items; cascade delete removes the old rows
            // (same pattern as UpdateQuotationAsync).
            quotation.Items.Clear();
            foreach (var item in items)
            {
                quotation.Items.Add(item);
            }
        }

        quotation.Subtotal = CalculateSubtotal(quotation.Items);
        quotation.Total = quotation.Subtotal;
        if (quotation.Budget == 0)
        {
            quotation.Budget = quotation.Total;
        }

        quotation.Status = QuotationStatus.Finalised;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(quotation);
    }

    public async Task<QuotationResponse> MarkPaidAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await _quotationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Quotation '{id}' does not exist.");

        if (quotation.Status != QuotationStatus.Finalised || quotation.PaymentStatus != PaymentStatus.Pending)
        {
            throw new BillingConflictException(
                $"Only a Finalised quotation with a Pending payment can be marked as paid. " +
                $"Current status: '{quotation.Status}', payment status: '{quotation.PaymentStatus}'.");
        }

        var userId = _tenant.UserId
            ?? throw new ForbiddenException("A signed-in account is required to record a payment.");

        quotation.PaymentStatus = PaymentStatus.Paid;
        quotation.PaidAt = DateTimeOffset.UtcNow;
        quotation.PaidByUserId = userId;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(quotation);
    }

    public async Task<IReadOnlyList<QuotationResponse>> GetQuotationsForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var quotations = await _quotationRepository.GetByOwnerAsync(ownerId, cancellationToken);
        return quotations.Select(ToResponse).ToList();
    }

    /// <summary>
    /// One Examination line for the veterinarian charge plus one Medicine
    /// line per Issued prescription under this examination. Pending and
    /// Unavailable requests are never billed.
    /// </summary>
    private static List<QuotationItem> BuildBillItems(Examination examination)
    {
        var items = new List<QuotationItem>();

        if (examination.VeterinarianCharge > 0)
        {
            items.Add(new QuotationItem
            {
                Category = "Examination",
                Description = $"Veterinarian charge — Dr. {examination.Veterinarian?.Name}",
                Quantity = 1,
                UnitPrice = examination.VeterinarianCharge,
                TotalPrice = examination.VeterinarianCharge
            });
        }

        var issuedPrescriptions = examination.Diagnosis?.TreatmentRecords
            .SelectMany(t => t.Prescriptions)
            .Where(p => p.RequestStatus == MedicineRequestStatus.Issued)
            ?? Enumerable.Empty<Prescription>();

        foreach (var prescription in issuedPrescriptions)
        {
            var unitPrice = prescription.Medicine?.UnitPrice ?? 0m;
            items.Add(new QuotationItem
            {
                Category = "Medicine",
                Description = $"{prescription.Medicine?.Name ?? "Medicine"} ({prescription.Dosage})",
                Quantity = prescription.Quantity,
                UnitPrice = unitPrice,
                TotalPrice = prescription.Quantity * unitPrice
            });
        }

        return items;
    }

    /// <summary>
    /// Approved/Finalised quotations are read-only per the domain model's
    /// Approval business rule 5: "Approved quotations cannot be casually
    /// edited afterward."
    /// </summary>
    private static void EnsureEditable(Quotation quotation)
    {
        if (quotation.Status is QuotationStatus.Approved or QuotationStatus.Finalised)
        {
            throw new BillingConflictException(
                $"Quotation '{quotation.Id}' is '{quotation.Status}' and can no longer be edited.");
        }
    }

    /// <summary>
    /// Maps request items to entities with TotalPrice always recomputed as
    /// Quantity * UnitPrice; the client-authoritative rule is enforced here.
    /// </summary>
    private static List<QuotationItem> BuildItems(IEnumerable<DTOs.Billing.QuotationItemRequest> requestItems)
    {
        return requestItems
            .Select(i => new QuotationItem
            {
                Category = i.Category,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.Quantity * i.UnitPrice
            })
            .ToList();
    }

    private static decimal CalculateSubtotal(IEnumerable<QuotationItem> items)
    {
        return items.Sum(i => i.Quantity * i.UnitPrice);
    }

    private static QuotationResponse ToResponse(Quotation quotation) =>
        QuotationMapper.ToResponse(quotation);
}
