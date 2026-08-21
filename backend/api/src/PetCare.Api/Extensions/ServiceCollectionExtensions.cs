using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Approval;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Application.Validators;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Repositories;

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
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    public static IServiceCollection AddPetCareApplication(this IServiceCollection services)
    {
        services.AddScoped<ISchedulingService, SchedulingService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IApprovalService, ApprovalService>();

        services.AddScoped<IValidator<CreateAppointmentRequest>, CreateAppointmentRequestValidator>();
        services.AddScoped<IValidator<UpdateAppointmentRequest>, UpdateAppointmentRequestValidator>();
        services.AddScoped<IValidator<CreateQuotationRequest>, CreateQuotationRequestValidator>();
        services.AddScoped<IValidator<UpdateQuotationRequest>, UpdateQuotationRequestValidator>();
        services.AddScoped<IValidator<ApproveRequest>, ApproveRequestValidator>();
        services.AddScoped<IValidator<RejectRequest>, RejectRequestValidator>();
        services.AddScoped<IValidator<RequestRevisionRequest>, RequestRevisionRequestValidator>();

        return services;
    }
}
