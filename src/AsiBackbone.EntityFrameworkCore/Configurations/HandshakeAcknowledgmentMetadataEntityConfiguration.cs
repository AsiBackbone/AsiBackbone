using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsiBackbone.EntityFrameworkCore.Configurations;

/// <summary>
/// Configures the Entity Framework Core persistence mapping for
/// <see cref="HandshakeAcknowledgmentMetadataEntity" />.
/// </summary>
public sealed class HandshakeAcknowledgmentMetadataEntityConfiguration
    : IEntityTypeConfiguration<HandshakeAcknowledgmentMetadataEntity>
{
    private const int MetadataKeyMaxLength = 256;
    private const int MetadataValueMaxLength = 4096;
    private const int ConcurrencyStampMaxLength = 64;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HandshakeAcknowledgmentMetadataEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable("AsiBackboneHandshakeAcknowledgmentMetadata");

        _ = builder.HasKey(metadata => metadata.Id);

        _ = builder.Property(metadata => metadata.Id)
            .ValueGeneratedNever();

        _ = builder.Property(metadata => metadata.ConcurrencyStamp)
            .IsRequired()
            .HasMaxLength(ConcurrencyStampMaxLength)
            .IsConcurrencyToken();

        _ = builder.Property(metadata => metadata.HandshakeAcknowledgmentId)
            .IsRequired();

        _ = builder.Property(metadata => metadata.MetadataKey)
            .IsRequired()
            .HasMaxLength(MetadataKeyMaxLength);

        _ = builder.Property(metadata => metadata.MetadataValue)
            .IsRequired()
            .HasMaxLength(MetadataValueMaxLength);

        _ = builder.HasOne(metadata => metadata.HandshakeAcknowledgment)
            .WithMany()
            .HasForeignKey(metadata => metadata.HandshakeAcknowledgmentId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasIndex(metadata => metadata.HandshakeAcknowledgmentId);

        _ = builder.HasIndex(metadata => metadata.MetadataKey);

        _ = builder.HasIndex(metadata => new
        {
            metadata.HandshakeAcknowledgmentId,
            metadata.MetadataKey
        })
        .IsUnique();
    }
}
