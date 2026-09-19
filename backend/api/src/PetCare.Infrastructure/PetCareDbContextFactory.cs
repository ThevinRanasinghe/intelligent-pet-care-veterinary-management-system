using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PetCare.Infrastructure;

/// <summary>
/// Design-time factory so `dotnet ef migrations add/update` can construct
/// PetCareDbContext without a startup project (PetCare.Api) wiring DI yet.
/// The connection string here is only used for generating migrations; the
/// real app configures this via appsettings/environment variables.
/// </summary>
public class PetCareDbContextFactory : IDesignTimeDbContextFactory<PetCareDbContext>
{
    public PetCareDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PETCARE_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=petcare;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<PetCareDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new PetCareDbContext(optionsBuilder.Options);
    }
}
