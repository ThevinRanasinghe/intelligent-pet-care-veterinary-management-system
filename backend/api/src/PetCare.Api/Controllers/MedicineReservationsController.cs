using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Api.Extensions;
using PetCare.Application.DTOs.Inventory;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;

namespace PetCare.Api.Controllers;

[ApiController]
[Route("api/medicine-reservations")]
[Produces("application/json")]
public class MedicineReservationsController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public MedicineReservationsController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Lists all medicine reservations, newest first.
    /// </summary>
    [Authorize(Roles = $"{Roles.Veterinarian},{Roles.InventoryOfficer},{Roles.ClinicManager},{Roles.SuperAdmin}")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReservationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ReservationResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var reservations = await _inventoryService.GetReservationsAsync(cancellationToken);
        return Ok(reservations);
    }

    /// <summary>
    /// Creates a reservation for a quantity of medicine.
    /// Concurrency-safe atomic reservation against available inventory.
    /// </summary>
    [Authorize(Roles = $"{Roles.Veterinarian},{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpPost]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> ReserveMedicine(
        [FromBody] ReserveMedicineRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var response = await _inventoryService.ReserveMedicineAsync(request, userId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Cancels an active reservation and restores available stock.
    /// </summary>
    [Authorize(Roles = $"{Roles.Veterinarian},{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelReservation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await _inventoryService.CancelReservationAsync(id, userId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Dispenses an active reservation, deducting physical batches using FEFO.
    /// </summary>
    [Authorize(Roles = $"{Roles.InventoryOfficer},{Roles.SuperAdmin}")]
    [HttpPost("{id:guid}/dispense")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DispenseReservation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await _inventoryService.DispenseReservationAsync(id, userId, cancellationToken);
        return NoContent();
    }
}
