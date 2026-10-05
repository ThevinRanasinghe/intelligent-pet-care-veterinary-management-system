namespace PetCare.Infrastructure.Security;

/// <summary>
/// Bound from configuration section "Jwt". The signing key never has a
/// hardcoded fallback: it must come from user-secrets
/// (dotnet user-secrets set "Jwt:Key" "...") or the PETCARE_JWT_KEY
/// environment variable, mirroring how the DB connection string is
/// resolved (see ServiceCollectionExtensions.AddPetCareInfrastructure).
/// </summary>
public class JwtOptions
{
    public string Issuer { get; set; } = "PetCareApi";

    public string Audience { get; set; } = "PetCareClient";

    public int ExpiryMinutes { get; set; } = 60;

    public string Key { get; set; } = string.Empty;
}
