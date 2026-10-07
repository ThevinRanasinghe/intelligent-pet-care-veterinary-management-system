using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Security;

namespace PetCare.Se3110.Tests;

/// <summary>
/// Shared WebApplicationFactory + seeding helpers for the SE3110 discovery
/// suite. Requires PETCARE_TEST_DB_CONNECTION pointing at a migrated,
/// disposable PostgreSQL database. All seeded data uses GUID-suffixed
/// unique names so the suite is re-runnable without resetting the DB.
///
/// Organisation scoping is resolved from Users.OrganizationId (JWT sub ->
/// Users row), so every seeded staff/owner caller has a real User row.
/// </summary>
public class Se3110Fixture : WebApplicationFactory<Program>
{
    // Test-only signing key; never used outside this throwaway test server.
    public const string TestJwtKey =
        "se3110-test-only-signing-key-0123456789abcdef-not-a-real-secret";

    public const string TestIssuer = "PetCareApi";
    public const string TestAudience = "PetCareClient";

    private readonly string _connectionString;

    public Se3110Fixture()
    {
        _connectionString =
            Environment.GetEnvironmentVariable("PETCARE_TEST_DB_CONNECTION")
            ?? Environment.GetEnvironmentVariable("PETCARE_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set PETCARE_TEST_DB_CONNECTION to a migrated test database "
                + "(see raw-results/test-output/00-environment.txt).");

        Environment.SetEnvironmentVariable("PETCARE_DB_CONNECTION", _connectionString);
        Environment.SetEnvironmentVariable("PETCARE_JWT_KEY", TestJwtKey);
    }

    public string ConnectionString => _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:PetCareDb", _connectionString);
        builder.UseSetting("Jwt:Key", TestJwtKey);
        builder.UseSetting("Jwt:Issuer", TestIssuer);
        builder.UseSetting("Jwt:Audience", TestAudience);
        builder.UseSetting("Jwt:ExpiryMinutes", "60");
    }

    // ---------- identity / tokens ----------

    public string TokenFor(User user, int expiryMinutes = 60)
    {
        var generator = new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            Key = TestJwtKey,
            Issuer = TestIssuer,
            Audience = TestAudience,
            ExpiryMinutes = expiryMinutes,
        }));
        return generator.GenerateToken(user).Token;
    }

    /// <summary>A bearer client for an already-seeded User.</summary>
    public HttpClient ClientFor(User user, int expiryMinutes = 60)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenFor(user, expiryMinutes));
        return client;
    }

    /// <summary>A bearer client for a raw token string (tampering tests).</summary>
    public HttpClient ClientForToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ---------- DbContext ----------

    /// <summary>Creates a DI scope; caller disposes it. Use for direct DbContext access.</summary>
    public IServiceScope CreateDbScope() => Services.CreateScope();

    /// <summary>Runs an action against a scoped DbContext.</summary>
    public async Task<T> WithDbAsync<T>(Func<PetCareDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetCareDbContext>();
        return await action(db);
    }

    public Task WithDbAsync(Func<PetCareDbContext, Task> action) =>
        WithDbAsync<object?>(async db => { await action(db); return null; });

    // ---------- seeding helpers (unique per call) ----------

    public static string Unique(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 9, 20)];

    public async Task<Organization> SeedOrgAsync()
    {
        var org = new Organization
        {
            Name = "SE3110 Clinic " + Guid.NewGuid().ToString("N")[..8],
            Email = $"clinic-{Guid.NewGuid():N}@se3110.test",
            Phone = "0112345678",
            Address = "1 Test Lane",
            City = "Colombo",
            Country = "LK",
            Status = OrganizationStatus.Active,
            IsActive = true,
        };
        await WithDbAsync(async db => { db.Organizations.Add(org); await db.SaveChangesAsync(); });
        return org;
    }

    public async Task<User> SeedUserAsync(string role, Guid? organizationId = null,
        string password = "TestPassw0rd!")
    {
        var hasher = new PasswordHasher();
        var user = new User
        {
            Email = $"se3110-{Guid.NewGuid():N}@test.local",
            PasswordHash = hasher.HashPassword(password),
            Name = "SE3110 " + role,
            FirstName = "SE3110",
            LastName = role,
            Role = role,
            Active = true,
            OrganizationId = organizationId,
        };
        await WithDbAsync(async db => { db.Users.Add(user); await db.SaveChangesAsync(); });
        return user;
    }

    public async Task<Veterinarian> SeedVetAsync(Guid organizationId,
        Guid? userId = null, bool active = true)
    {
        var vet = new Veterinarian
        {
            Name = "Dr SE3110 " + Guid.NewGuid().ToString("N")[..8],
            Specialisation = "General",
            Branch = "Main",
            Active = active,
            OrganizationId = organizationId,
            UserId = userId,
        };
        await WithDbAsync(async db => { db.Veterinarians.Add(vet); await db.SaveChangesAsync(); });
        return vet;
    }

    public async Task<(User user, PetOwner owner)> SeedOwnerAsync()
    {
        var user = await SeedUserAsync(Roles.PetOwner);
        var owner = new PetOwner
        {
            Id = "SE3-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            FullName = user.Name,
            Email = user.Email,
            UserId = user.Id,
        };
        await WithDbAsync(async db => { db.PetOwners.Add(owner); await db.SaveChangesAsync(); });
        return (user, owner);
    }

    public async Task<Pet> SeedPetAsync(string ownerId, bool archived = false)
    {
        var pet = new Pet
        {
            Id = "SE3-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            OwnerId = ownerId,
            Name = "Pet" + Guid.NewGuid().ToString("N")[..6],
            Species = "Dog",
            Breed = "Mixed",
            IsArchived = archived,
        };
        await WithDbAsync(async db => { db.Pets.Add(pet); await db.SaveChangesAsync(); });
        return pet;
    }

    /// <summary>Seeds an Available slot on a near-future date for the vet.</summary>
    public async Task<AppointmentSlot> SeedSlotAsync(Guid veterinarianId,
        TimeOnly? start = null, TimeOnly? end = null,
        AppointmentSlotStatus status = AppointmentSlotStatus.Available)
    {
        var slot = new AppointmentSlot
        {
            VeterinarianId = veterinarianId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3)),
            StartTime = start ?? new TimeOnly(9, 0),
            EndTime = end ?? new TimeOnly(9, 30),
            Branch = "Main",
            Status = status,
        };
        await WithDbAsync(async db => { db.AppointmentSlots.Add(slot); await db.SaveChangesAsync(); });
        return slot;
    }

    public async Task<Supplier> SeedSupplierAsync(Guid? organizationId = null)
    {
        var supplier = new Supplier
        {
            Name = "SE3110 Supplier " + Guid.NewGuid().ToString("N")[..8],
            ContactPerson = "Test",
            Phone = "011",
            Email = $"sup-{Guid.NewGuid():N}@se3110.test",
            Address = "x",
            Status = SupplierStatus.Active,
            OrganizationId = organizationId,
        };
        await WithDbAsync(async db => { db.Suppliers.Add(supplier); await db.SaveChangesAsync(); });
        return supplier;
    }

    public async Task<Medicine> SeedMedicineAsync(Guid organizationId, int totalQuantity = 0)
    {
        var med = new Medicine
        {
            Name = "SE3110 Med " + Guid.NewGuid().ToString("N")[..8],
            Category = "Antibiotic",
            Description = "test",
            DosageForm = "Tablet",
            Strength = "250mg",
            UnitPrice = 10m,
            Manufacturer = "TestPharma",
            ReorderLevel = 1,
            Status = MedicineStatus.Active,
            OrganizationId = organizationId,
            TotalQuantity = totalQuantity,
        };
        await WithDbAsync(async db => { db.Medicines.Add(med); await db.SaveChangesAsync(); });
        return med;
    }

    /// <summary>Seeds a medicine with a supplier + batch of the given qty.</summary>
    public async Task<(Medicine med, MedicineBatch batch)> SeedStockAsync(
        Guid organizationId, int quantity)
    {
        var med = await SeedMedicineAsync(organizationId, quantity);
        var supplier = await SeedSupplierAsync(organizationId);
        var batch = new MedicineBatch
        {
            MedicineId = med.Id,
            SupplierId = supplier.Id,
            BatchNumber = "BN-" + Guid.NewGuid().ToString("N")[..8],
            Quantity = quantity,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            ReceivedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = BatchStatus.Active,
        };
        await WithDbAsync(async db => { db.MedicineBatches.Add(batch); await db.SaveChangesAsync(); });
        return (med, batch);
    }

    public async Task<ConsultationRequest> SeedConsultationAsync(
        string petId, string ownerId, Guid organizationId, string status = "Draft")
    {
        var cr = new ConsultationRequest
        {
            Id = "SE3-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            PetId = petId,
            OwnerId = ownerId,
            SymptomsDescription = "SE3110 seeded symptoms",
            PreferredDate = DateTime.UtcNow.AddDays(3),
            BudgetLimit = 100m,
            Status = status,
            OrganizationId = organizationId,
        };
        await WithDbAsync(async db => { db.ConsultationRequests.Add(cr); await db.SaveChangesAsync(); });
        return cr;
    }

    /// <summary>Seeds a slot (Reserved) + Confirmed appointment on it.</summary>
    public async Task<(AppointmentSlot slot, Appointment appt)> SeedBookedAppointmentAsync(
        string petId, Guid veterinarianId)
    {
        var slot = await SeedSlotAsync(veterinarianId,
            status: AppointmentSlotStatus.Reserved);
        var appt = new Appointment
        {
            PetId = petId,
            VeterinarianId = veterinarianId,
            AppointmentSlotId = slot.Id,
            Date = slot.Date,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            Status = AppointmentStatus.Confirmed,
        };
        await WithDbAsync(async db => { db.Appointments.Add(appt); await db.SaveChangesAsync(); });
        return (slot, appt);
    }

    public async Task<(Quotation quotation, Approval approval)> SeedPendingApprovalAsync(
        Guid appointmentId, QuotationStatus quotationStatus = QuotationStatus.Finalised)
    {
        var quotation = new Quotation
        {
            AppointmentId = appointmentId,
            Budget = 100m,
            Subtotal = 80m,
            Total = 80m,
            Status = quotationStatus,
            PaymentStatus = PaymentStatus.Pending,
        };
        var approval = new Approval { Quotation = quotation, Status = ApprovalStatus.Pending };
        await WithDbAsync(async db =>
        {
            db.Quotations.Add(quotation);
            db.Approvals.Add(approval);
            await db.SaveChangesAsync();
        });
        return (quotation, approval);
    }

    public async Task<AgentWorkflow> SeedWorkflowAsync(string consultationRequestId,
        Guid organizationId, string status)
    {
        var wf = new AgentWorkflow
        {
            ConsultationRequestId = consultationRequestId,
            OrganizationId = organizationId,
            Objective = "SE3110 seeded workflow",
            Status = status,
        };
        await WithDbAsync(async db =>
        {
            db.AgentWorkflows.Add(wf);
            await db.SaveChangesAsync();
            db.AgentWorkflowApprovals.Add(new AgentWorkflowApproval
            {
                WorkflowId = wf.Id,
                StepNumber = 1,
                Status = PetCare.Domain.Constants.AgentWorkflowApprovalStatus.Pending,
                ProposalJson = "{}",
            });
            await db.SaveChangesAsync();
        });
        return wf;
    }
}
