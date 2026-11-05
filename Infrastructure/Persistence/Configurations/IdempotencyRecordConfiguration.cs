using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Key).IsRequired().HasMaxLength(100);
        builder.Property(r => r.UserId).IsRequired().HasMaxLength(450);
        builder.Property(r => r.RequestPath).IsRequired().HasMaxLength(300);
        builder.Property(r => r.ResponseContentType).HasMaxLength(100);
        // Ayni anahtar (kullanici bazinda) tek kez islenir: yaris durumunda
        // ikinci INSERT bu index'e takilir.
        builder.HasIndex(r => new { r.UserId, r.Key }).IsUnique();
        builder.HasIndex(r => r.CreatedAt);
    }
}
