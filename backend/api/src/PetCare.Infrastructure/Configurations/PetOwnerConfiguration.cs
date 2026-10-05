using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class PetOwnerConfiguration : IEntityTypeConfiguration<PetOwner>
{
    public void Configure(EntityTypeBuilder<PetOwner> builder)
    {
        builder.ToTable("PetOwners");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(30);

        // Optional 1:0..1 link to the authentication account. Unique index
        // guarantees one PetOwner profile per User (multiple NULLs allowed
        // for staff-created profiles not yet claimed by registration).
        builder.Property(x => x.UserId);

        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("IX_PetOwners_UserId");

        builder.HasOne(x => x.User)
            .WithOne(u => u.PetOwner)
            .HasForeignKey<PetOwner>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Address)
            .HasMaxLength(250);

        builder.Property(x => x.CreatedAt);

        builder.Property(x => x.UpdatedAt);

        builder.HasMany(x => x.Pets)
            .WithOne(x => x.Owner)
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ConsultationRequests)
            .WithOne(x => x.Owner)
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
