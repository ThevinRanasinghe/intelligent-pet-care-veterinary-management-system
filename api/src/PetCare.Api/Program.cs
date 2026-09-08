using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PetCare.Api.Extensions;
using PetCare.Api.Middleware;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.Validators.Auth;
using PetCare.Infrastructure.Entities;
using PetCare.Infrastructure.Extensions;
using PetCare.Infrastructure.Persistence;
using PetCare.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════════
// 1. Infrastructure (DbContext + Identity + Application Services)
// ═══════════════════════════════════════════════════════════════════════════════
builder.Services.AddInfrastructure(builder.Configuration);

// ═══════════════════════════════════════════════════════════════════════════════
// 2. FluentValidation
// ═══════════════════════════════════════════════════════════════════════════════
builder.Services.AddScoped<IValidator<LoginRequestDto>,            LoginRequestValidator>();
builder.Services.AddScoped<IValidator<RegisterPetOwnerRequestDto>, RegisterPetOwnerRequestValidator>();

// ═══════════════════════════════════════════════════════════════════════════════
// 3. JWT Bearer Authentication
// ═══════════════════════════════════════════════════════════════════════════════
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key must be configured.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ValidateIssuer   = true,
            ValidIssuer      = builder.Configuration["Jwt:Issuer"],

            ValidateAudience = true,
            ValidAudience    = builder.Configuration["Jwt:Audience"],

            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero,
            RoleClaimType            = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
        };
    });

// ═══════════════════════════════════════════════════════════════════════════════
// 4. Authorization
// ═══════════════════════════════════════════════════════════════════════════════
builder.Services.AddAuthorization();

// ═══════════════════════════════════════════════════════════════════════════════
// 5. Controllers + JSON
// ═══════════════════════════════════════════════════════════════════════════════
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// ═══════════════════════════════════════════════════════════════════════════════
// 6. Swagger / OpenAPI with JWT
// ═══════════════════════════════════════════════════════════════════════════════
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

// ═══════════════════════════════════════════════════════════════════════════════
// 7. CORS — allow only the React dev origin
// ═══════════════════════════════════════════════════════════════════════════════
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactDevPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ═══════════════════════════════════════════════════════════════════════════════
// BUILD
// ═══════════════════════════════════════════════════════════════════════════════
var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════════════════
// 8. Seed roles and SuperAdmin (idempotent — safe on every startup)
// ═══════════════════════════════════════════════════════════════════════════════
using (var scope = app.Services.CreateScope())
{
    var services   = scope.ServiceProvider;
    var logger     = services.GetRequiredService<ILogger<Program>>();
    var config     = services.GetRequiredService<IConfiguration>();

    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await IdentityRoleSeeder.SeedAsync(roleManager, logger);

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await SuperAdminSeeder.SeedAsync(userManager, config, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database seeding.");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// 9. Middleware pipeline — ORDER MATTERS
// ═══════════════════════════════════════════════════════════════════════════════
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PetCare AI v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("ReactDevPolicy");

// UseAuthentication BEFORE UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make the Program class accessible to integration test factory
public partial class Program { }
