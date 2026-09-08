using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PetCare.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI tools (dotnet ef migrations add, etc.).
/// Reads connection string from environment variable or a local appsettings.Development.json.
/// </summary>
public sealed class PetCareDbContextFactory : IDesignTimeDbContextFactory<PetCareDbContext>
{
    public PetCareDbContext CreateDbContext(string[] args)
    {
        // Build config from the Api project's appsettings for design-time usage
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "PetCare.Api"))
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection not found. " +
                "Set it in appsettings.Development.json or as an environment variable.");

        var optionsBuilder = new DbContextOptionsBuilder<PetCareDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new PetCareDbContext(optionsBuilder.Options);
    }
}
