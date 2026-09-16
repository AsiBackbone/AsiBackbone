using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsiBackbone.EntityFrameworkCore.Configurations;

/// <summary>
/// Configures the Entity Framework Core persistence mapping for <see cref="HandshakeAcknowledgmentEntity" />.
/// </summary>
public sealed class HandshakeAcknowledgmentEntityConfiguration
    : IEntityTypeConfiguration<HandshakeAcknowledgmentEntity>
{
    private const int IdentifierMaxLength = 128;
    private const int SchemaVersionMaxLength = 64;
    private const int DisplayNameMaxLength = 256;
    private const int ActorTypeMaxLength = 64;
    private const int AcknowledgmentCodeMaxLength = 128;
    private const int CorrelationMaxLength = 128;
    private const int ConcurrencyStampMaxLength = 64;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HandshakeAcknowledgmentEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable("AsiBackboneHandshakeAcknowledgments");

        _ = builder.HasKey(acknowledgment => acknowledgment.Id);

        _ = builder.Property(acknowledgment => acknowledgment.Id)
            .ValueGeneratedNever();

        _ = builder.Property(acknowledgment => acknowledgment.ConcurrencyStamp)
            .IsRequired()
            .HasMaxLength(ConcurrencyStampMaxLength)
            .IsConcurrencyToken();

        _ = builder.Property(acknowledgment => acknowledgment.AcknowledgmentId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(acknowledgment => acknowledgment.SchemaVersion)
            .IsRequired()
            .HasMaxLength(SchemaVersionMaxLength);

        _ = builder.Property(acknowledgment => acknowledgment.HandshakeId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(acknowledgment => acknowledgment.ActorId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(acknowledgment => acknowledgment.ActorType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(ActorTypeMaxLength);

        _ = builder.Property(acknowledgment => acknowledgment.ActorDisplayName)
            .HasMaxLength(DisplayNameMaxLength);

        _ = builder.Property(acknowledgment => acknowledgment.AcknowledgmentCode)
            .IsRequired()
            .HasMaxLength(AcknowledgmentCodeMaxLength);

        _ = builder.Property(acknowledgment => acknowledgment.Acknowledged)
            .IsRequired();

        _ = builder.Property(acknowledgment => acknowledgment.OccurredUtc)
            .IsRequired();

        _ = builder.Property(acknowledgment => acknowledgment.CorrelationId)
            .HasMaxLength(CorrelationMaxLength);

        _ = builder.Property(acknowledgment => acknowledgment.TraceId)
            .HasMaxLength(CorrelationMaxLength);

        _ = builder.HasIndex(acknowledgment => acknowledgment.AcknowledgmentId)
            .IsUnique();

        _ = builder.HasIndex(acknowledgment => acknowledgment.SchemaVersion);

        _ = builder.HasIndex(acknowledgment => acknowledgment.HandshakeId);

        _ = builder.HasIndex(acknowledgment => acknowledgment.ActorId);

        _ = builder.HasIndex(acknowledgment => acknowledgment.ActorType);

        _ = builder.HasIndex(acknowledgment => acknowledgment.AcknowledgmentCode);

        _ = builder.HasIndex(acknowledgment => acknowledgment.Acknowledged);

        _ = builder.HasIndex(acknowledgment => acknowledgment.OccurredUtc);

        _ = builder.HasIndex(acknowledgment => acknowledgment.CorrelationId);

        _ = builder.HasIndex(acknowledgment => acknowledgment.TraceId);

        _ = builder.HasIndex(acknowledgment => new
        {
            acknowledgment.HandshakeId,
            acknowledgment.OccurredUtc
        });

        _ = builder.HasIndex(acknowledgment => new
        {
            acknowledgment.ActorId,
            acknowledgment.OccurredUtc
        });

        _ = builder.HasIndex(acknowledgment => new
        {
            acknowledgment.CorrelationId,
            acknowledgment.OccurredUtc
        });
    }
}
