using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Security;
using Xunit;

namespace PetCare.Infrastructure.Tests;

public class AppointmentProblemDetailsTests
{
    private const string JwtKey =
        "tc-be-025-test-only-key-at-least-thirty-two-bytes-long";

    // TC-BE-025: Invalid appointments must return HTTP 400 as ProblemDetails.
    [Theory]
    [Trait("TestCase", "TC-BE-025")]
    [InlineData(10, 10)] // Start equals end.
    [InlineData(11, 10)] // Start is later than end.
    public async Task CreateAppointment_InvalidTimes_Returns400ProblemDetails(
        int startHour, int endHour)
    {
        // ARRANGE: Referenced vet and slot exist, so the failure is about time.
        var date = new DateOnly(2030, 1, 10);
        var veterinarianId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        var vetRepository = new Mock<IVeterinarianRepository>();
        vetRepository
            .Setup(r => r.GetByIdAsync(
                veterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Veterinarian
            {
                Id = veterinarianId,
                Name = "Dr. Test Vet",
                Specialisation = "General",
                Branch = "Colombo",
                Active = true
            });
            var slotRepository = new Mock<IAppointmentSlotRepository>();
        slotRepository
            .Setup(r => r.GetByIdAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppointmentSlot
            {
                Id = slotId,
                VeterinarianId = veterinarianId,
                Date = date,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(12, 0),
                Branch = "Colombo",
                Status = AppointmentSlotStatus.Available
            });

        // In-memory API server with mock repositories; no live database needed
        // for an invalid request that the real validator rejects immediately.
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting(
                    "ConnectionStrings:PetCareDb",
                    "Host=127.0.0.1;Port=1;Database=unused_tcbe025;Username=unused;Password=unused");
                builder.UseSetting("Jwt:Key", JwtKey);
                builder.UseSetting("Jwt:Issuer", "PetCareApi");
                builder.UseSetting("Jwt:Audience", "PetCareClient");

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IVeterinarianRepository>();
                    services.RemoveAll<IAppointmentSlotRepository>();
                    services.AddSingleton<IVeterinarianRepository>(vetRepository.Object);
                    services.AddSingleton<IAppointmentSlotRepository>(slotRepository.Object);
                });
            });
using var client = factory.CreateClient();

        // The endpoint requires ClinicManager (or SuperAdmin) authorization.
        var tokenGenerator = new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            Key = JwtKey,
            Issuer = "PetCareApi",
            Audience = "PetCareClient",
            ExpiryMinutes = 60
        }));
        var token = tokenGenerator.GenerateToken(new User
        {
            Id = Guid.NewGuid(),
            Name = "Test Clinic Manager",
            Email = "manager-tcbe025@example.test",
            Role = Roles.ClinicManager,
            Active = true
        }).Token;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateAppointmentRequest
        {
            PetId = "PET-TEST0001",
            VeterinarianId = veterinarianId,
            AppointmentSlotId = slotId,
            ScheduledStart = date.ToDateTime(new TimeOnly(startHour, 0)),
            ScheduledEnd = date.ToDateTime(new TimeOnly(endHour, 0))
        };

        // ACT: Send a real HTTP request through routing, authorization,
        // the scheduling service, validator, and exception middleware.
        using var response = await client.PostAsJsonAsync(
            "/api/appointments", request);

        // ASSERT: Both invalid times produce validation ProblemDetails, not 500.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var properties = body.RootElement.EnumerateObject().ToArray();
        var errorsProperty = Assert.Single(properties.Where(p =>
            string.Equals(p.Name, "errors", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(JsonValueKind.Object, errorsProperty.Value.ValueKind);

        Assert.True(
            errorsProperty.Value.TryGetProperty("ScheduledStart", out var startErrors),
            "Expected an error for ScheduledStart in the ProblemDetails response.");
        Assert.Contains(startErrors.EnumerateArray(), error =>
            error.GetString() == "ScheduledStart must be before ScheduledEnd.");
    }
}
