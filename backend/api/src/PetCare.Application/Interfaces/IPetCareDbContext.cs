using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

public interface IPetCareDbContext
{
    DbSet<PetOwner> PetOwners { get; }

    DbSet<Pet> Pets { get; }

    DbSet<ConsultationRequest> ConsultationRequests { get; }

    DbSet<ConsultationStatusHistory> ConsultationStatusHistories { get; }

    DbSet<Organization> Organizations { get; }

    DbSet<Veterinarian> Veterinarians { get; }

    DbSet<Appointment> Appointments { get; }

    DbSet<Examination> Examinations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}