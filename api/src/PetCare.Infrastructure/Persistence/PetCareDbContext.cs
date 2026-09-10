using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Entities;

namespace PetCare.Infrastructure.Persistence;

/// <summary>
/// Main EF Core DbContext for Beacon Pet Health.
/// Extends IdentityDbContext to include all ASP.NET Core Identity tables
/// plus multi-organization and application entities.
/// </summary>
public sealed class PetCareDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Veterinarian> Veterinarians => Set<Veterinarian>();
    public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<ApprovalHistory> ApprovalHistories => Set<ApprovalHistory>();
    public DbSet<ConsultationRequest> ConsultationRequests => Set<ConsultationRequest>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<PetOwner> PetOwners => Set<PetOwner>();
    public DbSet<AIProposal> AIProposals => Set<AIProposal>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public PetCareDbContext(DbContextOptions<PetCareDbContext> options) : base(options)
    {
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    private void ApplyAuditTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<PetCare.Domain.Common.AuditableEntity>())
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply any entity configurations from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(PetCareDbContext).Assembly);

        // ── Organization configuration ──
        builder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.RegistrationNumber)
                .HasMaxLength(100);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Phone)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Address)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(e => e.City)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Country)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.ApprovedByUserId)
                .HasMaxLength(450);

            entity.Property(e => e.RejectedByUserId)
                .HasMaxLength(450);

            entity.Property(e => e.RejectionReason)
                .HasMaxLength(1000);

            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Status);
        });

        // ── ApplicationUser - Organization relationship & AccountStatus ──
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.AccountStatus)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(u => u.MustChangePassword)
                .HasDefaultValue(false);

            entity.HasIndex(u => u.OrganizationId);

            entity.HasOne(u => u.Organization)
                .WithMany()
                .HasForeignKey(u => u.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

