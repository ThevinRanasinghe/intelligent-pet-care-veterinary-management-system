using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PetCare.Api.Middleware;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Infrastructure.Data;
using PetCare.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers with Enum string converter
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PetCare Veterinary Management API",
        Version = "v1",
        Description = "Clean Architecture API for Component 1: Pet & Consultation Request Management (UC-05 to UC-17)."
    });
});

// Configure PostgreSQL DbContext & Interface mapping
builder.Services.AddDbContext<PetCareDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPetCareDbContext>(sp => sp.GetRequiredService<PetCareDbContext>());

// Register Application Services & Utilities
builder.Services.AddScoped<IIdGenerator, SequentialIdGenerator>();
builder.Services.AddScoped<IPetService, PetService>();
builder.Services.AddScoped<IConsultationService, ConsultationService>();

// Configure CORS for Frontend Integration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PetCare API v1");
    });
    app.UseCors("AllowAll");

    // Attempt to seed data if database is reachable
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetCareDbContext>();
        if (db.Database.CanConnect())
        {
            await DbInitializer.SeedAsync(db);
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Database connection/seeding skipped during startup: {Message}", ex.Message);
    }
}
else
{
    app.UseHttpsRedirection();
    app.UseCors("AllowFrontend");
}

app.UseAuthorization();
app.MapControllers();

app.Run();

// Export Program for WebApplicationFactory in integration tests
public partial class Program { }