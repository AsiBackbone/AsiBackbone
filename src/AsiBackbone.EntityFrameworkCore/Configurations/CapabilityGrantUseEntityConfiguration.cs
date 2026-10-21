using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AsiBackbone.EntityFrameworkCore.Configurations;

/// <summary>Configures durable capability-grant use records.</summary>
public sealed class CapabilityGrantUseEntityConfiguration : IEntityTypeConfiguration<CapabilityGrantUseEntity>
{
    private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetToUtcTicksConverter = new(
        value => value.ToUniversalTime().Ticks,
        value => new DateTimeOffset(value, TimeSpan.Zero));

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CapabilityGrantUseEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("AsiBackboneCapabilityGrantUses");
        _ = builder.HasKey(entity => entity.Id);
        _ = builder.Property(entity => entity.Id).ValueGeneratedNever();
        _ = builder.Property(entity => entity.ConcurrencyStamp).IsRequired().HasMaxLength(64).IsConcurrencyToken();
        _ = builder.Property(entity => entity.Issuer).IsRequired().HasMaxLength(256);
        _ = builder.Property(entity => entity.TokenId).IsRequired().HasMaxLength(256);
        _ = builder.Property(entity => entity.UseCount).IsRequired();
        _ = builder.Property(entity => entity.State).HasConversion<string>().IsRequired().HasMaxLength(64);
        _ = builder.Property(entity => entity.LastUsedUtc).HasConversion(DateTimeOffsetToUtcTicksConverter).IsRequired();
        _ = builder.Property(entity => entity.RetainUntilUtc).HasConversion(DateTimeOffsetToUtcTicksConverter).IsRequired();
        _ = builder.HasIndex(entity => new { entity.Issuer, entity.TokenId }).IsUnique();
        _ = builder.HasIndex(entity => entity.RetainUntilUtc);
    }
}
