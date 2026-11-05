using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ReceiptSequenceConfiguration
    : IEntityTypeConfiguration<ReceiptSequence>
{
    public void Configure(EntityTypeBuilder<ReceiptSequence> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SequenceDate)
            .IsRequired();

        builder.Property(s => s.CurrentValue)
            .IsRequired()
            .HasDefaultValue(0);

        // Mağaza + gün kombinasyonu unique olmalı
        builder.HasIndex(s => new { s.StoreId, s.SequenceDate })
            .IsUnique();

        builder.HasQueryFilter(s => !s.IsDeleted);

        builder.HasOne(s => s.Store)
            .WithMany()
            .HasForeignKey(s => s.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}