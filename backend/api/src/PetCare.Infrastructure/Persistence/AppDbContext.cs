using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Yours (Diagnosis & Treatment Management)
    public DbSet<Examination> Examinations { get; set; }
    public DbSet<Diagnosis> Diagnoses { get; set; }
    public DbSet<TreatmentRecord> TreatmentRecords { get; set; }
    public DbSet<Prescription> Prescriptions { get; set; }

    // Temporary stubs - other members' entities (merge walata replace wenawa)
    public DbSet<Pet> Pets { get; set; }
    public DbSet<ConsultationRequest> ConsultationRequests { get; set; }
    public DbSet<Medicine> Medicines { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Diagnosis - Examination: 1-to-1
        modelBuilder.Entity<Examination>()
            .HasOne(e => e.Diagnosis)
            .WithOne(d => d.Examination)
            .HasForeignKey<Diagnosis>(d => d.ExaminationId);

        // TreatmentRecord - Diagnosis: many-to-1
        modelBuilder.Entity<TreatmentRecord>()
            .HasOne(t => t.Diagnosis)
            .WithMany(d => d.TreatmentRecords)
            .HasForeignKey(t => t.DiagnosisId);

        // Prescription - TreatmentRecord: many-to-1
        modelBuilder.Entity<Prescription>()
            .HasOne(p => p.TreatmentRecord)
            .WithMany(t => t.Prescriptions)
            .HasForeignKey(p => p.TreatmentRecordId);

        // Prescription - Medicine: many-to-1
        modelBuilder.Entity<Prescription>()
            .HasOne(p => p.Medicine)
            .WithMany()
            .HasForeignKey(p => p.MedicineId);

        // Examination - Pet: many-to-1
        modelBuilder.Entity<Examination>()
            .HasOne(e => e.Pet)
            .WithMany()
            .HasForeignKey(e => e.PetId);
    }
}