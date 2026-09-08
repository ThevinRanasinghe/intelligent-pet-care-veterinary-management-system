using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class PetOwnerConfiguration : IEntityTypeConfiguration<PetOwner>
{
    public void Configure(EntityTypeBuilder<PetOwner> builder)
    {
        builder.ToTable("PetOwners");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(e => e.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(e => e.Address)
            .HasMaxLength(250);
    }
}
