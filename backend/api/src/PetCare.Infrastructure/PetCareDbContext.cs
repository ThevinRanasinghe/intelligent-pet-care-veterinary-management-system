using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Common;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure;

/// <summary>
/// EF Core DbContext for the Scheduling, Billing and Approval modules.
/// See docs/database/scheduling-billing-approval-domain-model.md.
/// </summary>
public class PetCareDbContext : DbContext
{
    public PetCareDbContext(DbContextOptions<PetCareDbContext> options)
        : base(options)
    {
    }

    public DbSet<Veterinarian> Veterinarians => Set<Veterinarian>();

    public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Quotation> Quotations => Set<Quotation>();

    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();

    public DbSet<Approval> Approvals => Set<Approval>();

    public DbSet<ApprovalHistory> ApprovalHistories => Set<ApprovalHistory>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PetCareDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Sets CreatedAt/UpdatedAt for AuditableEntity rows so clients can never
    /// spoof these values; see the "EF Core migrations" section of the domain model doc.
    /// </summary>
    private void ApplyAuditTimestamps()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
