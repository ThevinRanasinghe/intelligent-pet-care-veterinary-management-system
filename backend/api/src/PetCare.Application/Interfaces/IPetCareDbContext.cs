using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

public interface IPetCareDbContext
{
    DbSet<Pet> Pets { get; }
    DbSet<PetOwner> PetOwners { get; }
    DbSet<ConsultationRequest> ConsultationRequests { get; }
    DbSet<ConsultationStatusHistory> ConsultationStatusHistories { get; }
    DbSet<MedicalRecord> MedicalRecords { get; }
    DbSet<VaccinationRecord> VaccinationRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
