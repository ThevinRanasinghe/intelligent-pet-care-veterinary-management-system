namespace PetCare.Infrastructure.Agentic;

/// <summary>
/// Configuration for the internal Agentic AI service.
/// BaseUrl and InternalKey come from the "AgenticService" config section;
/// InternalKey may also be supplied via the PETCARE_AGENTIC_INTERNAL_KEY
/// environment variable (user-secrets/env only — never committed).
/// </summary>
public class AgenticServiceOptions
{
    public const string SectionName = "AgenticService";

    /// <summary>Base URL of the agentic service, e.g. http://localhost:8000</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Shared secret sent as the X-Internal-Key header.</summary>
    public string InternalKey { get; set; } = string.Empty;

    /// <summary>Per-request timeout in seconds (default 15).</summary>
    public int TimeoutSeconds { get; set; } = 15;
}
