using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> builder)
    {
        builder.ToTable("Pets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.OwnerId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Species)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Breed)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Gender)
            .HasMaxLength(50);

        builder.Property(x => x.DateOfBirth);

        builder.Property(x => x.Weight)
            .HasPrecision(18, 2);

        builder.Property(x => x.Age);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.PhotoUrl)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt);

        builder.Property(x => x.UpdatedAt);

        builder.HasOne(x => x.Owner)
            .WithMany(x => x.Pets)
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ConsultationRequests)
            .WithOne(x => x.Pet)
            .HasForeignKey(x => x.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
