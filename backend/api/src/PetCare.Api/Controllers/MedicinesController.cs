using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Api.Extensions;
using PetCare.Application.DTOs.Inventory;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/medicines")]
[Produces("application/json")]
public class MedicinesController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public MedicinesController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>Searches and paginates medicines.</summary>
    [Authorize(Roles = $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MedicineResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<MedicineResponse>>> GetMedicines(
        [FromQuery] SearchMedicinesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.SearchMedicinesAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets all medicines currently at or below their reorder level.</summary>
    [Authorize(Roles = $"{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(IReadOnlyList<MedicineResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<MedicineResponse>>> GetLowStock(CancellationToken cancellationToken)
    {
        var medicines = await _inventoryService.GetLowStockAsync(cancellationToken);
        return Ok(medicines);
    }

    /// <summary>Gets medicine batches expiring within the specified number of days.</summary>
    [Authorize(Roles = $"{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpGet("expiring")]
    [ProducesResponseType(typeof(IReadOnlyList<MedicineBatchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<MedicineBatchResponse>>> GetExpiringSoon(
        [FromQuery] int withinDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (withinDays <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Parameter",
                Detail = "withinDays must be a positive integer greater than zero."
            });
        }

        var batches = await _inventoryService.GetExpiringSoonAsync(withinDays, cancellationToken);
        return Ok(batches);
    }

    /// <summary>Gets a single medicine by id.</summary>
    [Authorize(Roles = $"{Roles.Veterinarian},{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MedicineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MedicineResponse>> GetMedicineById(Guid id, CancellationToken cancellationToken)
    {
        var medicine = await _inventoryService.GetMedicineByIdAsync(id, cancellationToken);
        return medicine is null ? NotFound() : Ok(medicine);
    }

    /// <summary>Creates a new medicine in the catalogue.</summary>
    [Authorize(Roles = $"{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpPost]
    [ProducesResponseType(typeof(MedicineResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MedicineResponse>> CreateMedicine(
        [FromBody] CreateMedicineRequest request,
        CancellationToken cancellationToken)
    {
        var medicine = await _inventoryService.CreateMedicineAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMedicineById), new { id = medicine.Id }, medicine);
    }

    /// <summary>Receives a new batch of stock for a medicine.</summary>
    [Authorize(Roles = $"{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpPost("{id:guid}/stock-in")]
    [ProducesResponseType(typeof(MedicineBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MedicineBatchResponse>> ReceiveStock(
        Guid id,
        [FromBody] ReceiveStockRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var batch = await _inventoryService.ReceiveStockAsync(id, request, userId, cancellationToken);
        return Ok(batch);
    }

    /// <summary>Gets all stock batches for a specific medicine.</summary>
    [Authorize(Roles = $"{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpGet("{id:guid}/batches")]
    [ProducesResponseType(typeof(IReadOnlyList<MedicineBatchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MedicineBatchResponse>>> GetBatchesForMedicine(
        Guid id,
        CancellationToken cancellationToken)
    {
        var medicine = await _inventoryService.GetMedicineByIdAsync(id, cancellationToken);
        if (medicine is null)
        {
            return NotFound();
        }

        var batches = await _inventoryService.GetBatchesForMedicineAsync(id, cancellationToken);
        return Ok(batches);
    }

    /// <summary>Gets transaction history for a specific medicine.</summary>
    [Authorize(Roles = $"{Roles.ClinicManager},{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpGet("{id:guid}/transactions")]
    [ProducesResponseType(typeof(IReadOnlyList<InventoryTransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<InventoryTransactionResponse>>> GetTransactions(
        Guid id,
        CancellationToken cancellationToken)
    {
        var medicine = await _inventoryService.GetMedicineByIdAsync(id, cancellationToken);
        if (medicine is null)
        {
            return NotFound();
        }

        var transactions = await _inventoryService.GetTransactionsAsync(id, cancellationToken);
        return Ok(transactions);
    }
}
