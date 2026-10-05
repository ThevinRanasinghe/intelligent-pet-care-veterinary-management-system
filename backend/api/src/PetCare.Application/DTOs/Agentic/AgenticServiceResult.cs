namespace PetCare.Application.DTOs.Agentic;

/// <summary>
/// Outcome of a call to the internal Agentic Service. AI calls are
/// advisory, so failures are surfaced as a result object rather than
/// thrown — callers decide how to degrade. <see cref="Content"/> carries
/// the raw JSON payload on success; <see cref="Error"/> carries a stable
/// machine-readable category (never secrets or payloads).
/// </summary>
public class AgenticServiceResult
{
    public bool Success { get; private init; }
    public int? StatusCode { get; private init; }
    public string? Content { get; private init; }
    public string? Error { get; private init; }

    public static AgenticServiceResult Ok(int statusCode, string content) =>
        new() { Success = true, StatusCode = statusCode, Content = content };

    public static AgenticServiceResult Failed(string error, int? statusCode = null) =>
        new() { Success = false, StatusCode = statusCode, Error = error };
}
