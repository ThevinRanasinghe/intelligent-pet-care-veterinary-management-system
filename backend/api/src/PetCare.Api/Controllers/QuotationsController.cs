using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

/// <summary>
/// Billing/Quotation endpoints. All business rules (line item calculation,
/// budget comparison, status transition/lock rules) are enforced by
/// <see cref="IBillingService"/> in PetCare.Application; this controller only
/// handles HTTP concerns (routing, status codes, model binding). See
/// docs/api/scheduling-billing-approval-api-contract.md#billing.
/// </summary>
[ApiController]
[Route("api/quotations")]
[Produces("application/json")]
[Authorize]
public class QuotationsController : ControllerBase
{
    // Billing visibility: clinical staff, management, and the inventory
    // officer (who issues billed medicines).
    private const string ReadRoles =
        $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin}";

    // By-id/self views also allow the owning PetOwner (ownership is checked
    // inside the action).
    private const string OwnerOrStaffReadRoles =
        $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin},{Roles.PetOwner}";

    // Quotations/billing are managed by the Clinic Manager.
    private const string ManageRoles =
        $"{Roles.ClinicManager},{Roles.SuperAdmin}";

    // Recording payment is restricted to the inventory desk / admin —
    // veterinarians and managers cannot mark a bill paid.
    private const string PaymentRoles =
        $"{Roles.InventoryOfficer},{Roles.SuperAdmin}";

    private readonly IBillingService _billingService;
    private readonly IOwnerAccessService _ownerAccess;

    public QuotationsController(
        IBillingService billingService,
        IOwnerAccessService ownerAccess)
    {
        _billingService = billingService;
        _ownerAccess = ownerAccess;
    }

    /// <summary>Gets all quotations.</summary>
    [HttpGet]
    [Authorize(Roles = ReadRoles)]
    [ProducesResponseType(typeof(IReadOnlyList<QuotationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuotationResponse>>> GetQuotations(CancellationToken cancellationToken)
    {
        var quotations = await _billingService.GetQuotationsAsync(cancellationToken);
        return Ok(quotations);
    }

    /// <summary>The caller's (pet owner's) bills.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = Roles.PetOwner)]
    [ProducesResponseType(typeof(IReadOnlyList<QuotationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuotationResponse>>> GetMyQuotations(CancellationToken cancellationToken)
    {
        var ownerId = await _ownerAccess.GetOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Ok(new List<QuotationResponse>());
        }

        var quotations = await _billingService.GetQuotationsForOwnerAsync(ownerId, cancellationToken);
        return Ok(quotations);
    }

    /// <summary>Gets a single quotation by id.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = OwnerOrStaffReadRoles)]
    [ProducesResponseType(typeof(QuotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationResponse>> GetQuotationById(Guid id, CancellationToken cancellationToken)
    {
        var quotation = await _billingService.GetQuotationByIdAsync(id, cancellationToken);
        if (quotation is null)
        {
            return NotFound();
        }

        // A pet owner may only read bills for their own pets.
        if (_ownerAccess.IsPetOwner
            && (quotation.PetId is null || !await _ownerAccess.OwnsPetAsync(quotation.PetId)))
        {
            return NotFound();
        }

        return Ok(quotation);
    }

    /// <summary>
    /// Records payment for a Finalised bill. Restricted to the inventory
    /// desk / administrator — veterinarians and managers get 403.
    /// </summary>
    [HttpPost("{id:guid}/mark-paid")]
    [Authorize(Roles = PaymentRoles)]
    [ProducesResponseType(typeof(QuotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuotationResponse>> MarkPaid(Guid id, CancellationToken cancellationToken)
    {
        var quotation = await _billingService.MarkPaidAsync(id, cancellationToken);
        return Ok(quotation);
    }

    /// <summary>
    /// Creates a new quotation for an appointment. Enforces the 1:1
    /// Appointment&lt;-&gt;Quotation rule and computes Subtotal/Total
    /// server-side from the submitted line items.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(QuotationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuotationResponse>> CreateQuotation(
        [FromBody] CreateQuotationRequest request,
        CancellationToken cancellationToken)
    {
        var quotation = await _billingService.CreateQuotationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetQuotationById), new { id = quotation.Id }, quotation);
    }

    /// <summary>
    /// Replaces a quotation's Budget and full line item set, recomputing
    /// Subtotal/Total. Rejected for Approved/Finalised quotations.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(QuotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuotationResponse>> UpdateQuotation(
        Guid id,
        [FromBody] UpdateQuotationRequest request,
        CancellationToken cancellationToken)
    {
        var quotation = await _billingService.UpdateQuotationAsync(id, request, cancellationToken);
        return Ok(quotation);
    }

    /// <summary>
    /// Recomputes Subtotal/Total from the quotation's current line items and
    /// reports whether Total is within Budget.
    /// </summary>
    [HttpPost("{id:guid}/calculate")]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(QuotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationResponse>> CalculateQuotation(Guid id, CancellationToken cancellationToken)
    {
        var quotation = await _billingService.CalculateQuotationAsync(id, cancellationToken);
        return Ok(quotation);
    }

    /// <summary>
    /// Submits a Draft/RevisionRequested quotation for Clinic Manager
    /// approval. Blocked if Total exceeds Budget.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(QuotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuotationResponse>> SubmitQuotation(Guid id, CancellationToken cancellationToken)
    {
        var quotation = await _billingService.SubmitQuotationForApprovalAsync(id, cancellationToken);
        return Ok(quotation);
    }

    /// <summary>
    /// Locks an Approved quotation as Finalised so it can no longer be edited.
    /// </summary>
    [HttpPost("{id:guid}/finalize")]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(QuotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuotationResponse>> FinalizeQuotation(Guid id, CancellationToken cancellationToken)
    {
        var quotation = await _billingService.FinalizeQuotationAsync(id, cancellationToken);
        return Ok(quotation);
    }
}
