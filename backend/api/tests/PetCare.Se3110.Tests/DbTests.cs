using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure;
using Xunit;

namespace PetCare.Se3110.Tests;

/// <summary>
/// SE3110 database-level discovery tests (catalogue TC-DB-*): migrations,
/// referential integrity, constraints and booking concurrency. Assertions
/// are taken from the catalogue's expected values — failures are recorded,
/// not fixed.
/// </summary>
public class DbTests : IClassFixture<Se3110Fixture>
{
    private readonly Se3110Fixture _fx;

    public DbTests(Se3110Fixture fx) => _fx = fx;

    // ---------- TC-DB-001 ----------
    [Fact]
    public async Task Migrations_ApplyToEmptyDatabase()
    {
        var builder = new NpgsqlConnectionStringBuilder(_fx.ConnectionString);
        var adminCs = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
        { Database = "postgres" }.ConnectionString;
        var dbName = "se3110_mig_" + Guid.NewGuid().ToString("N")[..8];
        var testCs = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
        { Database = dbName }.ConnectionString;

        var expectedMigrations = typeof(PetCareDbContext).Assembly
            .GetTypes()
            .Count(t => t.IsClass && !t.IsAbstract
                && t.IsSubclassOf(typeof(Microsoft.EntityFrameworkCore.Migrations.Migration)));

        await using (var admin = new NpgsqlConnection(adminCs))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{dbName}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<PetCareDbContext>()
                .UseNpgsql(testCs)
                .Options;
            await using var db = new PetCareDbContext(options);
            await db.Database.MigrateAsync();

            await using var conn = new NpgsqlConnection(testCs);
            await conn.OpenAsync();
            await using var count = conn.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\"";
            var applied = Convert.ToInt32(await count.ExecuteScalarAsync());

            Assert.Equal(expectedMigrations, applied);
        }
        finally
        {
            await using var admin = new NpgsqlConnection(adminCs);
            await admin.OpenAsync();
            await using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }
    }

    // ---------- TC-DB-002 ----------
    [Fact]
    public async Task Pet_WithUnknownOwner_FkViolation()
    {
        var petId = "SE3-FK-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _fx.WithDbAsync(async db =>
        {
            db.Pets.Add(new Pet
            {
                Id = petId,
                OwnerId = "SE3-OWNER-NOPE",
                Name = "FkTest",
                Species = "Dog",
                Breed = "Mixed",
            });
            await db.SaveChangesAsync();
        }));

        // The row must not have persisted.
        var persisted = await _fx.WithDbAsync(db =>
            db.Pets.AnyAsync(p => p.Id == petId));
        Assert.False(persisted);
    }

    // ---------- TC-DB-003 ----------
    [Fact]
    public async Task User_DuplicateEmail_UniqueViolation()
    {
        var email = $"dup-{Guid.NewGuid():N}@se3110.test";

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _fx.WithDbAsync(async db =>
        {
            db.Users.Add(new User
            {
                Email = email, PasswordHash = "x", Name = "A", Role = Roles.PetOwner,
            });
            db.Users.Add(new User
            {
                Email = email, PasswordHash = "x", Name = "B", Role = Roles.PetOwner,
            });
            await db.SaveChangesAsync();
        }));
    }

    // ---------- TC-DB-004 ----------
    [Fact]
    public async Task Batch_NegativeQuantity_DbRejectsOrObservation()
    {
        var org = await _fx.SeedOrgAsync();
        var med = await _fx.SeedMedicineAsync(org.Id);
        var supplier = await _fx.SeedSupplierAsync(org.Id);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _fx.WithDbAsync(async db =>
        {
            db.MedicineBatches.Add(new MedicineBatch
            {
                MedicineId = med.Id,
                SupplierId = supplier.Id,
                BatchNumber = "BN-NEG-" + Guid.NewGuid().ToString("N")[..6],
                Quantity = -5,
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
                ReceivedDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = BatchStatus.Active,
            });
            await db.SaveChangesAsync();
        }));
    }

    // ---------- TC-DB-005 ----------
    [Fact]
    public async Task FailedBooking_DoesNotMutateSlot()
    {
        var org = await _fx.SeedOrgAsync();
        var manager = await _fx.SeedUserAsync(Roles.ClinicManager, org.Id);
        var vetUser = await _fx.SeedUserAsync(Roles.Veterinarian, org.Id);
        var vet = await _fx.SeedVetAsync(org.Id, vetUser.Id);
        var (_, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        var client = _fx.ClientFor(manager);

        // Book slot1 successfully.
        var day = DateTime.UtcNow.Date.AddDays(3);
        var slot1 = await _fx.SeedSlotAsync(vet.Id, new TimeOnly(9, 0), new TimeOnly(9, 30));
        var booked = await client.PostAsJsonAsync("/api/appointments", new
        {
            petId = pet.Id,
            veterinarianId = vet.Id,
            appointmentSlotId = slot1.Id,
            scheduledStart = day.AddHours(9),
            scheduledEnd = day.AddHours(9.5),
        });
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);

        // Failed booking attempt on the same slot.
        var failed = await client.PostAsJsonAsync("/api/appointments", new
        {
            petId = pet.Id,
            veterinarianId = vet.Id,
            appointmentSlotId = slot1.Id,
            scheduledStart = day.AddHours(9),
            scheduledEnd = day.AddHours(9.5),
        });
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);

        // Slot row must be exactly as the successful booking left it.
        var (status, apptCount) = await _fx.WithDbAsync(async db => (
            (await db.AppointmentSlots.FindAsync(slot1.Id))!.Status,
            await db.Appointments.CountAsync(a => a.AppointmentSlotId == slot1.Id)));
        Assert.Equal(AppointmentSlotStatus.Reserved, status);
        Assert.Equal(1, apptCount);
    }

    // ---------- booking concurrency helpers ----------

    private async Task<(HttpClient client, PetCare.Domain.Entities.Veterinarian vet,
        PetCare.Domain.Entities.Pet pet, AppointmentSlot slot, DateTime day)> ConcurrencySeedAsync()
    {
        var org = await _fx.SeedOrgAsync();
        var manager = await _fx.SeedUserAsync(Roles.ClinicManager, org.Id);
        var vetUser = await _fx.SeedUserAsync(Roles.Veterinarian, org.Id);
        var vet = await _fx.SeedVetAsync(org.Id, vetUser.Id);
        var (_, owner) = await _fx.SeedOwnerAsync();
        var pet = await _fx.SeedPetAsync(owner.Id);
        var slot = await _fx.SeedSlotAsync(vet.Id, new TimeOnly(9, 0), new TimeOnly(9, 30));
        return (_fx.ClientFor(manager), vet, pet, slot,
            DateTime.UtcNow.Date.AddDays(3));
    }

    private Task<HttpStatusCode> PostBookingAsync(HttpClient client,
        PetCare.Domain.Entities.Pet pet, PetCare.Domain.Entities.Veterinarian vet,
        AppointmentSlot slot, DateTime day) =>
        client.PostAsJsonAsync("/api/appointments", new
        {
            petId = pet.Id,
            veterinarianId = vet.Id,
            appointmentSlotId = slot.Id,
            scheduledStart = day.AddHours(9),
            scheduledEnd = day.AddHours(9.5),
        }).ContinueWith(t => t.Result.StatusCode);

    // ---------- TC-DB-006 ----------
    [Fact]
    public async Task ConcurrentBooking_SameSlot_OneWins()
    {
        var (client, vet, pet, slot, day) = await ConcurrencySeedAsync();

        var results = await Task.WhenAll(
            PostBookingAsync(client, pet, vet, slot, day),
            PostBookingAsync(client, pet, vet, slot, day));

        Assert.Equal(1, results.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(1, results.Count(s => s == HttpStatusCode.Conflict));

        var apptCount = await _fx.WithDbAsync(db =>
            db.Appointments.CountAsync(a => a.AppointmentSlotId == slot.Id));
        Assert.Equal(1, apptCount);
    }

    // ---------- TC-DB-007 ----------
    [Fact]
    public async Task ConcurrentBooking_TenParallel_OneWins()
    {
        var (client, vet, pet, slot, day) = await ConcurrencySeedAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => PostBookingAsync(client, pet, vet, slot, day)));

        Assert.Equal(1, results.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(9, results.Count(s => s == HttpStatusCode.Conflict));
        Assert.Equal(0, results.Count(s => (int)s >= 500));
    }
}
