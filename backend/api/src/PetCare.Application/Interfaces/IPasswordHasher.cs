namespace PetCare.Application.Interfaces;

/// <summary>
/// Hashes and verifies passwords. Implemented in PetCare.Infrastructure so
/// the Application layer stays free of a specific cryptography library
/// choice.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}
