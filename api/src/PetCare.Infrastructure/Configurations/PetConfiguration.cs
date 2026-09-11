using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> builder)
    {
        builder.ToTable("Pets");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasMaxLength(30);

        builder.Property(p => p.OwnerId)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Species)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Breed)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Age)
            .IsRequired();

        builder.Property(p => p.Notes)
            .HasMaxLength(2000);

        builder.Property(p => p.PhotoUrl)
            .HasMaxLength(500);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        builder.HasIndex(p => p.OwnerId);

        builder.HasOne(p => p.Owner)
            .WithMany(o => o.Pets)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
