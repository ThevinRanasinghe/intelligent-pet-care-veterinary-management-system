using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

public interface IPetCareDbContext
{
    DbSet<PetOwner> PetOwners { get; }

    DbSet<Pet> Pets { get; }

    DbSet<ConsultationRequest> ConsultationRequests { get; }

    DbSet<ConsultationStatusHistory> ConsultationStatusHistories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}