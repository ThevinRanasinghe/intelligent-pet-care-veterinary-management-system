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
        var currentDir = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(currentDir, "..", "PetCare.Api");
        if (!Directory.Exists(apiPath))
            apiPath = Path.Combine(currentDir, "api", "src", "PetCare.Api");
        if (!Directory.Exists(apiPath))
            apiPath = Path.Combine(currentDir, "src", "PetCare.Api");
        if (!Directory.Exists(apiPath))
            apiPath = currentDir;

        // Build config from the Api project's appsettings and user secrets for design-time usage
        var config = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets("053cff44-34fc-44ea-8006-29b202949ead")
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString) || connectionString.StartsWith("CONFIGURE_VIA"))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection not configured. " +
                "Set it via dotnet user-secrets in PetCare.Api or as an environment variable.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<PetCareDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new PetCareDbContext(optionsBuilder.Options);
    }
}
