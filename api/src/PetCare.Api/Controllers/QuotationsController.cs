using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Api.DTOs;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

/// <summary>
/// Quotation and billing management endpoints.
/// Supports server-side price calculation and tenant-isolated operations.
/// </summary>
[ApiController]
[Route("api/quotations")]
[Produces("application/json")]
[Authorize(Roles = $"{Roles.ClinicManager},{Roles.SuperAdmin}")]
public sealed class QuotationsController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly ICurrentUserService _currentUser;

    public QuotationsController(
        IBillingService billingService,
        ICurrentUserService currentUser)
    {
        _billingService = billingService;
        _currentUser = currentUser;
    }

    private Guid? GetOrganizationId() => _currentUser.OrganizationId;

    /// <summary>
    /// READ ALL: Lists quotations for the organization.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<QuotationResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var quotations = await _billingService.GetQuotationsAsync(orgId.Value, ct);
        return Ok(ApiResponse<IReadOnlyList<QuotationResponseDto>>.Ok(quotations));
    }

    /// <summary>
    /// READ ONE: Gets quotation details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<QuotationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        var quotation = await _billingService.GetQuotationByIdAsync(id, orgId.Value, ct);
        if (quotation is null)
            return NotFound(ApiResponse.Fail("Quotation not found."));

        return Ok(ApiResponse<QuotationResponseDto>.Ok(quotation));
    }

    /// <summary>
    /// CREATE: Creates a new quotation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<QuotationResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateQuotationDto dto, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var quotation = await _billingService.CreateQuotationAsync(orgId.Value, dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = quotation.Id }, ApiResponse<QuotationResponseDto>.Ok(quotation, "Quotation created successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// UPDATE: Updates an existing quotation.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<QuotationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuotationDto dto, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var quotation = await _billingService.UpdateQuotationAsync(id, orgId.Value, dto, ct);
            return Ok(ApiResponse<QuotationResponseDto>.Ok(quotation, "Quotation updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// CALCULATE: Server-side recalculation of quotation line items.
    /// </summary>
    [HttpPost("{id:guid}/calculate")]
    [ProducesResponseType(typeof(ApiResponse<QuotationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var result = await _billingService.CalculateQuotationAsync(id, orgId.Value, ct);
            return Ok(ApiResponse<QuotationResponseDto>.Ok(result, "Quotation calculated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// SUBMIT: Submits quotation for clinic manager approval.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(ApiResponse<QuotationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var orgId = GetOrganizationId();
        if (!orgId.HasValue)
            return BadRequest(ApiResponse.Fail("User is not associated with an organization."));

        try
        {
            var quotation = await _billingService.SubmitQuotationForApprovalAsync(id, orgId.Value, ct);
            return Ok(ApiResponse<QuotationResponseDto>.Ok(quotation, "Quotation submitted for approval."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }
}
