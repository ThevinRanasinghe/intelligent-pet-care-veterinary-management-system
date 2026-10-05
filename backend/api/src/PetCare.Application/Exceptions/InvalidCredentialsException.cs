namespace PetCare.Application.Exceptions;

/// <summary>
/// Thrown when login credentials are invalid (unknown email, wrong
/// password, or inactive account). The API layer should map this to HTTP
/// 401 Unauthorized. Deliberately does not distinguish "unknown email" from
/// "wrong password" in the message, to avoid leaking account existence.
/// </summary>
public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message) : base(message)
    {
    }
}
