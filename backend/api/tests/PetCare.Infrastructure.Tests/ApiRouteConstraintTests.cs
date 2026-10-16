using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PetCare.Infrastructure.Tests;

public class ApiRouteConstraintTests
{
    // TC-BE-024: An appointment ID that is not a GUID must not match
    // the [HttpGet("{id:guid}")] route. It should return 404, not 500.
    [Fact]
    [Trait("TestCase", "TC-BE-024")]
    public async Task GetAppointment_InvalidGuidInUrl_Returns404()
    {
        // ARRANGE: Start the real ASP.NET Core API in an in-memory test host.
        // These values satisfy startup configuration only. This route test
        // does not connect to PostgreSQL or use a real JWT.
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "ConnectionStrings:PetCareDb",
                    "Host=127.0.0.1;Port=1;Database=unused_route_test;Username=unused;Password=unused");
                builder.UseSetting(
                    "Jwt:Key",
                    "test-only-signing-key-at-least-32-bytes-long-for-routing");
                builder.UseSetting("Jwt:Issuer", "PetCareApi");
                builder.UseSetting("Jwt:Audience", "PetCareClient");
            });

        using var client = factory.CreateClient();

        // ACT: The {id:guid} route constraint should reject this URL.
        using var response = await client.GetAsync(
            "/api/appointments/not-a-guid");

        // ASSERT: No controller action runs; ASP.NET Core responds with 404.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
