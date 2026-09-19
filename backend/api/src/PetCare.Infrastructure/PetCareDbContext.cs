using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Common;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure;

/// <summary>
/// EF Core DbContext for the Scheduling, Billing, Approval, Pet, and
/// Consultation modules.
/// See docs/database/scheduling-billing-approval-domain-model.md.
/// </summary>
public class PetCareDbContext : DbContext, IPetCareDbContext
{
    public PetCareDbContext(DbContextOptions<PetCareDbContext> options)
        : base(options)
    {
    }

    // Scheduling / Billing / Approval entities
    public DbSet<Veterinarian> Veterinarians => Set<Veterinarian>();

    public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Quotation> Quotations => Set<Quotation>();

    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();

    public DbSet<Approval> Approvals => Set<Approval>();

    public DbSet<ApprovalHistory> ApprovalHistories => Set<ApprovalHistory>();

    public DbSet<User> Users => Set<User>();

    // Pet / Consultation entities
    public DbSet<PetOwner> PetOwners { get; set; } = null!;

    public DbSet<Pet> Pets { get; set; } = null!;

    public DbSet<ConsultationRequest> ConsultationRequests { get; set; } = null!;

    public DbSet<ConsultationStatusHistory> ConsultationStatusHistories { get; set; } = null!;

    // Diagnosis / Treatment entities
    public DbSet<Examination> Examinations { get; set; } = null!;

    public DbSet<Diagnosis> Diagnoses { get; set; } = null!;

    public DbSet<TreatmentRecord> TreatmentRecords { get; set; } = null!;

    public DbSet<Prescription> Prescriptions { get; set; } = null!;

    public DbSet<Medicine> Medicines { get; set; } = null!;

    // Medicine & Inventory entities
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<MedicineBatch> MedicineBatches => Set<MedicineBatch>();

    public DbSet<MedicineReservation> MedicineReservations => Set<MedicineReservation>();

    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

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
