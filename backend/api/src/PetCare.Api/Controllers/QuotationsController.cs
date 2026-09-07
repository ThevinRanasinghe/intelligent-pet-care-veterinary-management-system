using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.Interfaces;

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
public class QuotationsController : ControllerBase
{
    private readonly IBillingService _billingService;

    public QuotationsController(IBillingService billingService)
    {
        _billingService = billingService;
    }

    /// <summary>Gets all quotations.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<QuotationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuotationResponse>>> GetQuotations(CancellationToken cancellationToken)
    {
        var quotations = await _billingService.GetQuotationsAsync(cancellationToken);
        return Ok(quotations);
    }

    /// <summary>Gets a single quotation by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(QuotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationResponse>> GetQuotationById(Guid id, CancellationToken cancellationToken)
    {
        var quotation = await _billingService.GetQuotationByIdAsync(id, cancellationToken);
        return quotation is null ? NotFound() : Ok(quotation);
    }

    /// <summary>
    /// Creates a new quotation for an appointment. Enforces the 1:1
    /// Appointment&lt;-&gt;Quotation rule and computes Subtotal/Total
    /// server-side from the submitted line items.
    /// </summary>
    [HttpPost]
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
    [ProducesResponseType(typeof(QuotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuotationResponse>> FinalizeQuotation(Guid id, CancellationToken cancellationToken)
    {
        var quotation = await _billingService.FinalizeQuotationAsync(id, cancellationToken);
        return Ok(quotation);
    }
}
