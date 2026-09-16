using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PetCare.Application.Interfaces;
using PetCare.Infrastructure.Data;
using PetCare.Infrastructure.Services;

namespace PetCare.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<PetCareDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Database context
        services.AddScoped<IPetCareDbContext>(
            provider => provider.GetRequiredService<PetCareDbContext>());

        // Pet Owner service
        services.AddScoped<IPetOwnerService, PetOwnerService>();

        // Pet service
        services.AddScoped<IPetService, PetService>();

        // Consultation Request service
        services.AddScoped<
            IConsultationRequestService,
            ConsultationRequestService>();

        return services;
    }
}