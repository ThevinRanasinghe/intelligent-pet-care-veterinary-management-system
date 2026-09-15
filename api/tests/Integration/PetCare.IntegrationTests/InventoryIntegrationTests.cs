using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using PetCare.Application.DTOs.Inventory;
using PetCare.Domain.Constants;
using Xunit;

namespace PetCare.IntegrationTests;

public class InventoryIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly string TestJwtKey = "integration-test-signing-key-at-least-32-bytes-long-super-secret";
    private static readonly string DbConnection = "Host=localhost;Port=5432;Database=Pet Care Management System;Username=thevin;Password=thevin123";

    public InventoryIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", DbConnection);
            builder.UseSetting("Jwt:Key", TestJwtKey);
            builder.UseSetting("Jwt:Issuer", "PetCareApi");
            builder.UseSetting("Jwt:Audience", "PetCareClient");
        });

        _client = _factory.CreateClient();
    }

    private string GenerateToken(string role, Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", role),
            new Claim(JwtRegisteredClaimNames.Email, $"{role.ToLowerInvariant()}@test.com"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "PetCareApi",
            audience: "PetCareClient",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task GetMedicines_Unauthenticated_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/medicines");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMedicine_AsVeterinarian_Returns403Forbidden()
    {
        var vetToken = GenerateToken(Roles.Veterinarian);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/medicines")
        {
            Content = JsonContent.Create(new CreateMedicineRequest
            {
                Name = "VetForbiddenMed",
                Category = "Antibiotic",
                UnitPrice = 10,
                ReorderLevel = 5
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", vetToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BasicInventoryFlow_EndToEnd_Succeeds()
    {
        var officerToken = GenerateToken(Roles.InventoryOfficer);
        var vetToken = GenerateToken(Roles.Veterinarian);

        // 1. Create Supplier
        var createSupplierReq = new HttpRequestMessage(HttpMethod.Post, "/api/suppliers")
        {
            Content = JsonContent.Create(new CreateSupplierRequest
            {
                Name = $"Flow Supplier {Guid.NewGuid().ToString()[..6]}",
                ContactPerson = "Supplier Contact",
                Phone = "0771234567",
                Email = "flow@supplier.lk",
                Address = "Kandy, Sri Lanka"
            })
        };
        createSupplierReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", officerToken);
        var supplierRes = await _client.SendAsync(createSupplierReq);
        Assert.Equal(HttpStatusCode.Created, supplierRes.StatusCode);
        var supplier = await supplierRes.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.NotNull(supplier);

        // 2. Create Medicine
        var createMedReq = new HttpRequestMessage(HttpMethod.Post, "/api/medicines")
        {
            Content = JsonContent.Create(new CreateMedicineRequest
            {
                Name = $"Flow Med {Guid.NewGuid().ToString()[..6]}",
                Category = "Vaccine",
                Description = "Flow test vaccine",
                DosageForm = "Vial",
                Strength = "10ml",
                UnitPrice = 45.00m,
                Manufacturer = "FlowPharma",
                ReorderLevel = 5
            })
        };
        createMedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", officerToken);
        var medRes = await _client.SendAsync(createMedReq);
        Assert.Equal(HttpStatusCode.Created, medRes.StatusCode);
        var medicine = await medRes.Content.ReadFromJsonAsync<MedicineResponse>();
        Assert.NotNull(medicine);

        // 3. Receive Stock
        var stockInReq = new HttpRequestMessage(HttpMethod.Post, $"/api/medicines/{medicine.Id}/stock-in")
        {
            Content = JsonContent.Create(new ReceiveStockRequest
            {
                SupplierId = supplier.Id,
                BatchNumber = "FLOW-B1",
                Quantity = 50,
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6))
            })
        };
        stockInReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", officerToken);
        var stockInRes = await _client.SendAsync(stockInReq);
        Assert.Equal(HttpStatusCode.OK, stockInRes.StatusCode);
        var batch = await stockInRes.Content.ReadFromJsonAsync<MedicineBatchResponse>();
        Assert.NotNull(batch);
        Assert.Equal(50, batch.Quantity);

        // 4. Retrieve Medicine & verify quantities
        var getMedReq = new HttpRequestMessage(HttpMethod.Get, $"/api/medicines/{medicine.Id}");
        getMedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", vetToken);
        var getMedRes = await _client.SendAsync(getMedReq);
        Assert.Equal(HttpStatusCode.OK, getMedRes.StatusCode);
        var updatedMed = await getMedRes.Content.ReadFromJsonAsync<MedicineResponse>();
        Assert.NotNull(updatedMed);
        Assert.Equal(50, updatedMed.TotalQuantity);
        Assert.Equal(50, updatedMed.AvailableQuantity);

        // 5. Batches list
        var getBatchesReq = new HttpRequestMessage(HttpMethod.Get, $"/api/medicines/{medicine.Id}/batches");
        getBatchesReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", officerToken);
        var getBatchesRes = await _client.SendAsync(getBatchesReq);
        Assert.Equal(HttpStatusCode.OK, getBatchesRes.StatusCode);
        var batches = await getBatchesRes.Content.ReadFromJsonAsync<List<MedicineBatchResponse>>();
        Assert.NotNull(batches);
        Assert.Contains(batches, b => b.BatchNumber == "FLOW-B1");

        // 6. Reserve medicine (as Veterinarian)
        var reserveReq = new HttpRequestMessage(HttpMethod.Post, "/api/medicine-reservations")
        {
            Content = JsonContent.Create(new ReserveMedicineRequest
            {
                MedicineId = medicine.Id,
                Quantity = 10,
                ReferenceType = "Treatment",
                ReferenceId = Guid.NewGuid()
            })
        };
        reserveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", vetToken);
        var reserveRes = await _client.SendAsync(reserveReq);
        Assert.Equal(HttpStatusCode.Created, reserveRes.StatusCode);
        var reservation = await reserveRes.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(reservation);
        Assert.Equal(10, reservation.Quantity);

        // 7. Dispense reservation (as InventoryOfficer)
        var dispenseReq = new HttpRequestMessage(HttpMethod.Post, $"/api/medicine-reservations/{reservation.Id}/dispense");
        dispenseReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", officerToken);
        var dispenseRes = await _client.SendAsync(dispenseReq);
        Assert.Equal(HttpStatusCode.NoContent, dispenseRes.StatusCode);

        // 8. Verify transactions history
        var txReq = new HttpRequestMessage(HttpMethod.Get, $"/api/medicines/{medicine.Id}/transactions");
        txReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", officerToken);
        var txRes = await _client.SendAsync(txReq);
        Assert.Equal(HttpStatusCode.OK, txRes.StatusCode);
        var transactions = await txRes.Content.ReadFromJsonAsync<List<InventoryTransactionResponse>>();
        Assert.NotNull(transactions);
        Assert.True(transactions.Count >= 3); // StockIn, Reservation, Dispense
    }

    [Fact]
    public async Task ConcurrentReservations_ExactlyFiveAvailable_OneSucceedsOneConflicts()
    {
        var officerToken = GenerateToken(Roles.InventoryOfficer);
        var vet1Token = GenerateToken(Roles.Veterinarian);
        var vet2Token = GenerateToken(Roles.Veterinarian);

        // Create supplier
        var createSupplierReq = new HttpRequestMessage(HttpMethod.Post, "/api/suppliers")
        {
            Content = JsonContent.Create(new CreateSupplierRequest
            {
                Name = $"Conc Supplier {Guid.NewGuid().ToString()[..6]}"
            })
        };
        createSupplierReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", officerToken);
        var sRes = await _client.SendAsync(createSupplierReq);
        var supplier = await sRes.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.NotNull(supplier);

        // Create medicine with 0 stock
        var createMedReq = new HttpRequestMessage(HttpMethod.Post, "/api/medicines")
        {
            Content = JsonContent.Create(new CreateMedicineRequest
            {
                Name = $"Conc Med {Guid.NewGuid().ToString()[..6]}",
                Category = "Analgesic",
                UnitPrice = 12.00m,
                ReorderLevel = 2
            })
        };
        createMedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", officerToken);
        var medRes = await _client.SendAsync(createMedReq);
        var medicine = await medRes.Content.ReadFromJsonAsync<MedicineResponse>();
        Assert.NotNull(medicine);

        // Stock-in EXACTLY 5 units
        var stockInReq = new HttpRequestMessage(HttpMethod.Post, $"/api/medicines/{medicine.Id}/stock-in")
        {
            Content = JsonContent.Create(new ReceiveStockRequest
            {
                SupplierId = supplier.Id,
                BatchNumber = "CONC-B5",
                Quantity = 5,
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
            })
        };
        stockInReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", officerToken);
        var sInRes = await _client.SendAsync(stockInReq);
        Assert.Equal(HttpStatusCode.OK, sInRes.StatusCode);

        // Now prepare two simultaneous reservation requests for all 5 units
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/medicine-reservations")
        {
            Content = JsonContent.Create(new ReserveMedicineRequest
            {
                MedicineId = medicine.Id,
                Quantity = 5,
                ReferenceType = "Appointment"
            })
        };
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", vet1Token);

        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/medicine-reservations")
        {
            Content = JsonContent.Create(new ReserveMedicineRequest
            {
                MedicineId = medicine.Id,
                Quantity = 5,
                ReferenceType = "Appointment"
            })
        };
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", vet2Token);

        // Fire both requests concurrently
        var task1 = _client.SendAsync(req1);
        var task2 = _client.SendAsync(req2);
        var responses = await Task.WhenAll(task1, task2);

        var statusCodes = responses.Select(r => r.StatusCode).ToList();

        // Exactly one request must return 201 Created and exactly one must return 409 Conflict
        Assert.Contains(HttpStatusCode.Created, statusCodes);
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);

        // Verify final inventory state: Available = 0, Reserved = 5, Total = 5
        var checkReq = new HttpRequestMessage(HttpMethod.Get, $"/api/medicines/{medicine.Id}");
        checkReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", officerToken);
        var checkRes = await _client.SendAsync(checkReq);
        var finalMed = await checkRes.Content.ReadFromJsonAsync<MedicineResponse>();
        Assert.NotNull(finalMed);
        Assert.Equal(5, finalMed.TotalQuantity);
        Assert.Equal(5, finalMed.ReservedQuantity);
        Assert.Equal(0, finalMed.AvailableQuantity);
    }
}
