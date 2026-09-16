using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsiBackbone.EntityFrameworkCore.Configurations;

/// <summary>
/// Configures the Entity Framework Core persistence mapping for <see cref="AuditLedgerMetadataEntity" />.
/// </summary>
public sealed class AuditLedgerMetadataEntityConfiguration
    : IEntityTypeConfiguration<AuditLedgerMetadataEntity>
{
    private const int MetadataKeyMaxLength = 256;
    private const int MetadataValueMaxLength = 4096;
    private const int ConcurrencyStampMaxLength = 64;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLedgerMetadataEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable("AsiBackboneAuditLedgerMetadata");

        _ = builder.HasKey(metadata => metadata.Id);

        _ = builder.Property(metadata => metadata.Id)
            .ValueGeneratedNever();

        _ = builder.Property(metadata => metadata.ConcurrencyStamp)
            .IsRequired()
            .HasMaxLength(ConcurrencyStampMaxLength)
            .IsConcurrencyToken();

        _ = builder.Property(metadata => metadata.AuditLedgerRecordId)
            .IsRequired();

        _ = builder.Property(metadata => metadata.MetadataKey)
            .IsRequired()
            .HasMaxLength(MetadataKeyMaxLength);

        _ = builder.Property(metadata => metadata.MetadataValue)
            .IsRequired()
            .HasMaxLength(MetadataValueMaxLength);

        _ = builder.HasOne(metadata => metadata.AuditLedgerRecord)
            .WithMany()
            .HasForeignKey(metadata => metadata.AuditLedgerRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasIndex(metadata => metadata.AuditLedgerRecordId);

        _ = builder.HasIndex(metadata => metadata.MetadataKey);

        _ = builder.HasIndex(metadata => new
        {
            metadata.AuditLedgerRecordId,
            metadata.MetadataKey
        })
        .IsUnique();
    }
}
