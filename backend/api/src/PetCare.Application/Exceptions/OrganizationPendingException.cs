namespace PetCare.Application.Exceptions;

/// <summary>
/// Thrown when a user whose organization is not yet approved/active
/// attempts to log in. The API layer should map this to HTTP 403 Forbidden
/// so the client can display a "pending verification" message distinct
/// from the generic "invalid credentials" 401.
/// </summary>
public class OrganizationPendingException : Exception
{
    public OrganizationPendingException(string message) : base(message)
    {
    }
}
