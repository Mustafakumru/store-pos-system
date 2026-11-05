using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class CodeSequenceConfiguration : IEntityTypeConfiguration<CodeSequence>
{
    public void Configure(EntityTypeBuilder<CodeSequence> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Scope).IsRequired().HasMaxLength(50);
        builder.Property(s => s.CurrentValue).IsRequired().HasDefaultValue(0);
        builder.HasIndex(s => s.Scope).IsUnique();
    }
}
