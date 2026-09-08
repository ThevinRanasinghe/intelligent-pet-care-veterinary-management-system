using Microsoft.OpenApi.Models;

namespace PetCare.Api.Extensions;

/// <summary>
/// Configures Swagger with JWT Bearer token support.
/// Developer flow: POST /api/auth/login → copy token → Swagger Authorize → test endpoints.
/// </summary>
public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title       = "PetCare AI API",
                Version     = "v1",
                Description = "Intelligent Pet Care & Veterinary Service Management System"
            });

            // JWT Bearer security definition
            var securityScheme = new OpenApiSecurityScheme
            {
                Name         = "Authorization",
                Description  = "Enter: **Bearer {your JWT token}**",
                In           = ParameterLocation.Header,
                Type         = SecuritySchemeType.Http,
                Scheme       = "Bearer",
                BearerFormat = "JWT",
                Reference    = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            };

            options.AddSecurityDefinition("Bearer", securityScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { securityScheme, Array.Empty<string>() }
            });
        });

        return services;
    }
}
