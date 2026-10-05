using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Inventory;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Produces("application/json")]
public class SuppliersController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public SuppliersController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>Gets all active suppliers.</summary>
    [Authorize(Roles = $"{Roles.InventoryOfficer},{Roles.ClinicManager},{Roles.SuperAdmin}")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SupplierResponse>>> GetSuppliers(CancellationToken cancellationToken)
    {
        var suppliers = await _inventoryService.GetSuppliersAsync(cancellationToken);
        return Ok(suppliers);
    }

    /// <summary>Gets a single supplier by id.</summary>
    [Authorize(Roles = $"{Roles.InventoryOfficer},{Roles.ClinicManager},{Roles.SuperAdmin}")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierResponse>> GetSupplierById(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await _inventoryService.GetSupplierByIdAsync(id, cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    /// <summary>Creates a new supplier.</summary>
    [Authorize(Roles = $"{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpPost]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SupplierResponse>> CreateSupplier(
        [FromBody] CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = await _inventoryService.CreateSupplierAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetSupplierById), new { id = supplier.Id }, supplier);
    }
}
