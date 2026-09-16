using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Data;

public class PetCareDbContext : DbContext, IPetCareDbContext
{
    public PetCareDbContext(DbContextOptions<PetCareDbContext> options)
        : base(options)
    {
    }

    public DbSet<PetOwner> PetOwners { get; set; } = null!;

    public DbSet<Pet> Pets { get; set; } = null!;

    public DbSet<ConsultationRequest> ConsultationRequests { get; set; } = null!;

    public DbSet<ConsultationStatusHistory> ConsultationStatusHistories { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =========================
        // PetOwner
        // =========================
        modelBuilder.Entity<PetOwner>(entity =>
        {
            entity.ToTable("PetOwners");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.FullName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Email)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.PhoneNumber)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Address)
                .HasMaxLength(250);
        });

        // =========================
        // Pet
        // =========================
        modelBuilder.Entity<Pet>(entity =>
        {
            entity.ToTable("Pets");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.OwnerId)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Species)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Breed)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Notes)
                .HasMaxLength(2000);

            entity.Property(x => x.PhotoUrl)
                .HasMaxLength(500);

            entity.HasOne(x => x.Owner)
                .WithMany(x => x.Pets)
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =========================
        // ConsultationRequest
        // =========================
        modelBuilder.Entity<ConsultationRequest>(entity =>
        {
            entity.ToTable("ConsultationRequests");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.PetId)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.OwnerId)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.SymptomsDescription)
                .HasMaxLength(4000)
                .IsRequired();

            entity.Property(x => x.PhotoUrl)
                .HasMaxLength(500);

            entity.Property(x => x.PreferredDate)
                .IsRequired();

            entity.Property(x => x.BudgetLimit)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(x => x.PreferredBranch)
                .HasMaxLength(100);

            entity.Property(x => x.Status)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.StatusNotes)
                .HasMaxLength(1000);

            entity.HasOne(x => x.Pet)
                .WithMany(x => x.ConsultationRequests)
                .HasForeignKey(x => x.PetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Owner)
                .WithMany(x => x.ConsultationRequests)
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =========================
        // ConsultationStatusHistory
        // =========================
        modelBuilder.Entity<ConsultationStatusHistory>(entity =>
        {
            entity.ToTable("ConsultationStatusHistories");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .ValueGeneratedOnAdd();

            entity.Property(x => x.ConsultationRequestId)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Comments)
                .HasMaxLength(1000);

            entity.HasOne(x => x.ConsultationRequest)
                .WithMany(x => x.StatusHistories)
                .HasForeignKey(x => x.ConsultationRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}