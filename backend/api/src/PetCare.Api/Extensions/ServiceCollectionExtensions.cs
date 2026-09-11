using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PetCare.Application.DTOs.Approval;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Application.Validators;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Repositories;
using PetCare.Infrastructure.Security;

namespace PetCare.Api.Extensions;

/// <summary>
/// Composition-root wiring for PetCare.Application and PetCare.Infrastructure.
/// Reuses the existing repository/service/validator implementations from
/// those two projects; no duplicate classes are created here.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPetCareInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolve from app configuration first; if missing or empty, fall back
        // to the PETCARE_DB_CONNECTION environment variable. No hardcoded
        // fallback is provided so authentication failures are caught at startup
        // rather than silently trying a default password that may not exist.
        var configured = configuration.GetConnectionString("PetCareDb");
        var connectionString = !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Environment.GetEnvironmentVariable("PETCARE_DB_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A PostgreSQL connection string must be configured. " +
                "Set it via user secrets: dotnet user-secrets set \"ConnectionStrings:PetCareDb\" \"<connection-string>\" " +
                "or via the environment variable PETCARE_DB_CONNECTION.");
        }

        services.AddDbContext<PetCareDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IVeterinarianRepository, VeterinarianRepository>();
        services.AddScoped<IAppointmentSlotRepository, AppointmentSlotRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IQuotationRepository, QuotationRepository>();
        services.AddScoped<IApprovalRepository, ApprovalRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.PostConfigure<JwtOptions>(options =>
        {
            // The signing key must never be hardcoded/committed. Resolve from
            // configuration first (appsettings/user-secrets), falling back to
            // the PETCARE_JWT_KEY environment variable, mirroring how the DB
            // connection string is resolved above.
            if (string.IsNullOrWhiteSpace(options.Key))
            {
                options.Key = Environment.GetEnvironmentVariable("PETCARE_JWT_KEY") ?? string.Empty;
            }
        });

        return services;
    }

    public static IServiceCollection AddPetCareApplication(this IServiceCollection services)
    {
        services.AddScoped<ISchedulingService, SchedulingService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<IValidator<CreateAppointmentRequest>, CreateAppointmentRequestValidator>();
        services.AddScoped<IValidator<UpdateAppointmentRequest>, UpdateAppointmentRequestValidator>();
        services.AddScoped<IValidator<CreateQuotationRequest>, CreateQuotationRequestValidator>();
        services.AddScoped<IValidator<UpdateQuotationRequest>, UpdateQuotationRequestValidator>();
        services.AddScoped<IValidator<ApproveRequest>, ApproveRequestValidator>();
        services.AddScoped<IValidator<RejectRequest>, RejectRequestValidator>();
        services.AddScoped<IValidator<RequestRevisionRequest>, RequestRevisionRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();

        return services;
    }

    /// <summary>
    /// Configures JWT bearer authentication and role-based authorization.
    /// The signing key comes from configuration ("Jwt:Key", sourced from
    /// user-secrets or the PETCARE_JWT_KEY environment variable) — never
    /// hardcoded here. Throws at startup if no key is configured, so a
    /// missing secret fails fast instead of silently accepting unsigned/
    /// unverifiable tokens.
    /// </summary>
    public static IServiceCollection AddPetCareAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection["Issuer"] ?? "PetCareApi";
        var audience = jwtSection["Audience"] ?? "PetCareClient";
        var key = jwtSection["Key"] ?? Environment.GetEnvironmentVariable("PETCARE_JWT_KEY");

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "A JWT signing key must be configured. Set it via user secrets: " +
                "dotnet user-secrets set \"Jwt:Key\" \"<a long random secret>\" " +
                "or via the environment variable PETCARE_JWT_KEY.");
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
