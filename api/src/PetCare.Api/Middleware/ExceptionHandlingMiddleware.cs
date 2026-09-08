using System.Net;
using System.Text.Json;
using PetCare.Api.DTOs;

namespace PetCare.Api.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Catches all unhandled exceptions and returns a structured JSON response.
/// Never exposes stack traces, connection strings, passwords, or JWT keys.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access: {Message}", ex.Message);
            await WriteResponseAsync(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Bad request: {Message}", ex.Message);
            await WriteResponseAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            // Log full details server-side, return generic message to client
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteResponseAsync(
                context,
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again later.");
        }
    }

    private static async Task WriteResponseAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string message)
    {
        context.Response.StatusCode  = (int)statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(ApiResponse.Fail(message), _jsonOptions);
        await context.Response.WriteAsync(body);
    }
}
