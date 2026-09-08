namespace PetCare.Api.DTOs;

/// <summary>
/// Standardized API response envelope.
/// </summary>
public sealed record ApiResponse<T>(
    bool   Success,
    string Message,
    T?     Data   = default
)
{
    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new(true, message, data);

    public static ApiResponse<T> Fail(string message) =>
        new(false, message, default);
}

public sealed record ApiResponse(bool Success, string Message)
{
    public static ApiResponse Ok(string message = "Success")   => new(true,  message);
    public static ApiResponse Fail(string message)             => new(false, message);
}

/// <summary>
/// Validation error response with per-field error details.
/// </summary>
public sealed record ValidationErrorResponse(
    bool                         Success,
    string                       Message,
    IDictionary<string, string[]> Errors
);
