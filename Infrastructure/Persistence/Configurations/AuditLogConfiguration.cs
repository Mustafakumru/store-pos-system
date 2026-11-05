using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.UserId).HasMaxLength(450);
        builder.Property(a => a.PersonelId).HasMaxLength(20);
        builder.Property(a => a.UserName).HasMaxLength(200);
        builder.Property(a => a.Roles).HasMaxLength(200);
        builder.Property(a => a.EntityType).HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.Summary).HasMaxLength(500);
        builder.Property(a => a.Details).HasMaxLength(4000);
        builder.Property(a => a.HttpMethod).HasMaxLength(10);
        builder.Property(a => a.Path).HasMaxLength(300);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.CorrelationId).HasMaxLength(100);

        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.StoreId, a.OccurredAt });
        builder.HasIndex(a => new { a.UserId, a.OccurredAt });
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.CorrelationId);
    }
}

public class BackupRecordConfiguration : IEntityTypeConfiguration<BackupRecord>
{
    public void Configure(EntityTypeBuilder<BackupRecord> builder)
    {
        builder.ToTable("BackupRecords");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Trigger).IsRequired().HasMaxLength(50);
        builder.Property(b => b.RequestedBy).HasMaxLength(200);
        builder.Property(b => b.FilePath).IsRequired().HasMaxLength(500);
        builder.Property(b => b.Error).HasMaxLength(2000);
        builder.HasIndex(b => b.StartedAt);
    }
}
