namespace PetCare.Application.Exceptions;

/// <summary>
/// Thrown when a registration collides with an existing account or
/// organization (duplicate email/name). The API layer should map this to
/// HTTP 409 Conflict.
/// </summary>
public class DuplicateAccountException : Exception
{
    public DuplicateAccountException(string message) : base(message)
    {
    }
}
