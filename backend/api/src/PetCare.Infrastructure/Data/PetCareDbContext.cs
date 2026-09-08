using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Data;

public class PetCareDbContext : DbContext, IPetCareDbContext
{
    public PetCareDbContext(DbContextOptions<PetCareDbContext> options) : base(options)
    {
    }

    public DbSet<Pet> Pets { get; set; } = null!;
    public DbSet<PetOwner> PetOwners { get; set; } = null!;
    public DbSet<ConsultationRequest> ConsultationRequests { get; set; } = null!;
    public DbSet<ConsultationStatusHistory> ConsultationStatusHistories { get; set; } = null!;
    public DbSet<MedicalRecord> MedicalRecords { get; set; } = null!;
    public DbSet<VaccinationRecord> VaccinationRecords { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PetCareDbContext).Assembly);
    }
}