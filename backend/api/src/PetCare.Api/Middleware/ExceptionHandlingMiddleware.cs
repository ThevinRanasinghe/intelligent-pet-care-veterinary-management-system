using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.Exceptions;

namespace PetCare.Api.Middleware;

/// <summary>
/// Global exception handling: translates Application-layer exceptions into
/// the appropriate HTTP status codes so every client (React, Flutter, ...)
/// gets consistent error responses.
/// NotFoundException -&gt; 404, SchedulingConflictException -&gt; 409,
/// FluentValidation.ValidationException -&gt; 400, database slot-unique
/// constraint violations -&gt; 409, anything else -&gt; 500.
/// </summary>
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
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, errors) = exception switch
        {
            NotFoundException notFound => (
                HttpStatusCode.NotFound,
                "The requested resource was not found.",
                (IDictionary<string, string[]>?)null),

            SchedulingConflictException conflict => (
                HttpStatusCode.Conflict,
                "The request conflicts with an existing scheduling business rule.",
                null),

            ValidationException validation => (
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),

            DbUpdateException dbUpdate when IsSlotAlreadyInUse(dbUpdate) => (
                HttpStatusCode.Conflict,
                "The selected appointment slot is already in use.",
                null),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred.",
                null)
        };

        static bool IsSlotAlreadyInUse(DbUpdateException ex)
            => (ex.InnerException?.Message ?? ex.Message).Contains("IX_Appointments_AppointmentSlotId");

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "{ExceptionType} while processing {Method} {Path}", exception.GetType().Name, context.Request.Method, context.Request.Path);
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problemDetails = errors is null
            ? new ProblemDetails
            {
                Status = (int)statusCode,
                Title = title,
                Detail = exception.Message,
                Instance = context.Request.Path
            }
            : new ValidationProblemDetails(errors)
            {
                Status = (int)statusCode,
                Title = title,
                Instance = context.Request.Path
            };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}
