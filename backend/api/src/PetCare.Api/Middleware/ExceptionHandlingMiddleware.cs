using System.Net;
using System.Text.Json;
using PetCare.Domain.Exceptions;

namespace PetCare.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;
        var message = exception.Message;
        string errorCode = "INTERNAL_SERVER_ERROR";

        switch (exception)
        {
            case NotFoundException:
                statusCode = HttpStatusCode.NotFound;
                errorCode = "NOT_FOUND";
                break;

            case OwnershipValidationException:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "PET_OWNERSHIP_VALIDATION_FAILED";
                break;

            case ClinicalRecordModificationForbiddenException:
                statusCode = HttpStatusCode.Forbidden;
                errorCode = "CLINICAL_RECORD_MODIFICATION_FORBIDDEN";
                break;

            case ArgumentException:
            case DomainException:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "BAD_REQUEST";
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            statusCode = (int)statusCode,
            errorCode,
            message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
