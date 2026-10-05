namespace PetCare.Application.Exceptions;

/// <summary>
/// The caller is authenticated but the action is forbidden for their
/// tenant/account state (e.g. a ClinicManager whose account is not linked
/// to an organization, or whose organization is no longer Active). Maps to
/// HTTP 403 in the exception middleware.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
