using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class PetOwnerConfiguration : IEntityTypeConfiguration<PetOwner>
{
    public void Configure(EntityTypeBuilder<PetOwner> builder)
    {
        builder.ToTable("PetOwners");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasMaxLength(30);

        builder.Property(o => o.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(o => o.Email)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(o => o.PhoneNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(o => o.Address)
            .HasMaxLength(250);

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .IsRequired();
    }
}
