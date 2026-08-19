using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Data;

public class PetCareDbContext : DbContext
{
    public PetCareDbContext(DbContextOptions<PetCareDbContext> options) : base(options)
    {
    }

    public DbSet<Pet> Pets { get; set; } = null!;
    public DbSet<ConsultationRequest> ConsultationRequests { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Pet configuration
        modelBuilder.Entity<Pet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Species).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Breed).HasMaxLength(50);
        });

        // ConsultationRequest configuration
        modelBuilder.Entity<ConsultationRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SymptomsDescription).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50);

            // Relationship: Pet has many ConsultationRequests
            entity.HasOne(c => c.Pet)
                  .WithMany(p => p.ConsultationRequests)
                  .HasForeignKey(c => c.PetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}