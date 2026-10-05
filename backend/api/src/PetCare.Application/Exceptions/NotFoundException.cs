namespace PetCare.Application.Exceptions;

/// <summary>
/// Thrown when a referenced entity (veterinarian, slot, appointment, ...)
/// does not exist. The API layer should map this to HTTP 404.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
