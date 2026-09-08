using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(PetCareDbContext context)
    {
        // Only seed if empty
        if (await context.PetOwners.AnyAsync())
        {
            return;
        }

        var owner1 = new PetOwner
        {
            Id = "OWN-2001",
            FullName = "Sarah Jenkins",
            Email = "sarah.jenkins@example.com",
            PhoneNumber = "+1-555-0199",
            Address = "742 Evergreen Terrace, Springfield",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var owner2 = new PetOwner
        {
            Id = "OWN-2002",
            FullName = "Michael Chen",
            Email = "michael.chen@example.com",
            PhoneNumber = "+1-555-0142",
            Address = "100 Main Street, Suite 4B, Metro City",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var pet1 = new Pet
        {
            Id = "PET-1001",
            OwnerId = "OWN-2001",
            Name = "Buddy",
            Species = "Dog",
            Breed = "Golden Retriever",
            DateOfBirth = DateTime.UtcNow.AddYears(-3),
            Age = 3,
            Notes = "Allergic to certain chicken-based dry food brands. Very energetic.",
            PhotoUrl = "https://images.unsplash.com/photo-1552053831-71594a27632d",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var pet2 = new Pet
        {
            Id = "PET-1002",
            OwnerId = "OWN-2001",
            Name = "Milo",
            Species = "Cat",
            Breed = "British Shorthair",
            DateOfBirth = DateTime.UtcNow.AddYears(-2),
            Age = 2,
            Notes = "Indoor cat only. Calm temperament.",
            PhotoUrl = "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var pet3 = new Pet
        {
            Id = "PET-1003",
            OwnerId = "OWN-2002",
            Name = "Luna",
            Species = "Dog",
            Breed = "German Shepherd",
            DateOfBirth = DateTime.UtcNow.AddYears(-4),
            Age = 4,
            Notes = "Trained guard dog. Up to date on all core vaccinations.",
            PhotoUrl = "https://images.unsplash.com/photo-1589941013453-ec89f33b5e95",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var med1 = new MedicalRecord
        {
            Id = "MED-3001",
            PetId = "PET-1001",
            RecordDate = DateTime.UtcNow.AddMonths(-6),
            Diagnosis = "Mild Ear Infection (Otitis Externa)",
            Treatment = "Prescribed antibiotic ear drops for 7 days. Ear cleaned in clinic.",
            VeterinarianName = "Dr. Emily Hayes, DVM",
            ClinicalNotes = "Follow-up examination showed clear auditory canals with no residual inflammation.",
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        var vac1 = new VaccinationRecord
        {
            Id = "VAC-4001",
            PetId = "PET-1001",
            VaccineName = "Rabies (3-Year Booster)",
            DateAdministered = DateTime.UtcNow.AddYears(-1),
            NextDueDate = DateTime.UtcNow.AddYears(2),
            VeterinarianName = "Dr. Emily Hayes, DVM",
            BatchNumber = "RAB-2025-99B",
            CreatedAt = DateTime.UtcNow.AddYears(-1)
        };

        var vac2 = new VaccinationRecord
        {
            Id = "VAC-4002",
            PetId = "PET-1001",
            VaccineName = "DHPP (Distemper, Hepatitis, Parvovirus, Parainfluenza)",
            DateAdministered = DateTime.UtcNow.AddMonths(-3),
            NextDueDate = DateTime.UtcNow.AddMonths(9),
            VeterinarianName = "Dr. Emily Hayes, DVM",
            BatchNumber = "DHPP-8841-A",
            CreatedAt = DateTime.UtcNow.AddMonths(-3)
        };

        var req1 = new ConsultationRequest
        {
            Id = "REQ-5001",
            PetId = "PET-1001",
            OwnerId = "OWN-2001",
            SymptomsDescription = "Buddy has been scratching his right ear persistently since yesterday evening and whimpers when touched.",
            PhotoUrl = "https://images.unsplash.com/photo-1552053831-71594a27632d",
            PreferredDate = DateTime.UtcNow.AddDays(2),
            BudgetLimit = 150.00m,
            PreferredClinicLocationLat = 37.7749,
            PreferredClinicLocationLong = -122.4194,
            PreferredBranch = "Downtown Central Vet Clinic",
            Status = ConsultationStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var hist1 = new ConsultationStatusHistory
        {
            ConsultationRequestId = "REQ-5001",
            Status = ConsultationStatus.Submitted,
            Comments = "Initial consultation request submitted by owner.",
            ChangedAt = DateTime.UtcNow
        };

        context.PetOwners.AddRange(owner1, owner2);
        context.Pets.AddRange(pet1, pet2, pet3);
        context.MedicalRecords.Add(med1);
        context.VaccinationRecords.AddRange(vac1, vac2);
        context.ConsultationRequests.Add(req1);
        context.ConsultationStatusHistories.Add(hist1);

        await context.SaveChangesAsync();
    }
}
