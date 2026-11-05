using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.PhoneNumber)
            .IsRequired()
            .HasMaxLength(15);

        builder.Property(c => c.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Email)
            .HasMaxLength(150);

        // Kasiyer telefon numarasıyla milisaniyeler içinde arama yapacak
        builder.HasIndex(c => c.PhoneNumber)
            .IsUnique();

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}