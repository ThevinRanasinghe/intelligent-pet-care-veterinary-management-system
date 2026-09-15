namespace PetCare.Application.Exceptions;

/// <summary>Thrown for inventory business-rule violations (insufficient stock,
/// expired/inactive medicine, invalid state transition). Maps to HTTP 409.</summary>
public class InventoryConflictException : Exception
{
    public InventoryConflictException(string message) : base(message) { }
}