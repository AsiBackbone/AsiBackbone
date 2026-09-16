using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsiBackbone.EntityFrameworkCore.Configurations;

/// <summary>
/// Configures the Entity Framework Core persistence mapping for <see cref="DecisionReceiptLifecycleEventEntity" />.
/// </summary>
public sealed class DecisionReceiptLifecycleEventEntityConfiguration
    : IEntityTypeConfiguration<DecisionReceiptLifecycleEventEntity>
{
    private const int IdentifierMaxLength = 128;
    private const int StageMaxLength = 128;
    private const int OperationNameMaxLength = 256;
    private const int OutcomeMaxLength = 128;
    private const int ConcurrencyStampMaxLength = 64;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DecisionReceiptLifecycleEventEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable("AsiBackboneAuditResidueLifecycleEvents");

        _ = builder.HasKey(lifecycleEvent => lifecycleEvent.Id);

        _ = builder.Property(lifecycleEvent => lifecycleEvent.Id)
            .ValueGeneratedNever();

        _ = builder.Property(lifecycleEvent => lifecycleEvent.ConcurrencyStamp)
            .IsRequired()
            .HasMaxLength(ConcurrencyStampMaxLength)
            .IsConcurrencyToken();

        _ = builder.Property(lifecycleEvent => lifecycleEvent.EventId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(lifecycleEvent => lifecycleEvent.Stage)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(StageMaxLength);

        _ = builder.Property(lifecycleEvent => lifecycleEvent.StageSequence)
            .IsRequired();

        _ = builder.Property(lifecycleEvent => lifecycleEvent.OccurredUtc)
            .IsRequired();

        _ = builder.Property(lifecycleEvent => lifecycleEvent.CorrelationId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(lifecycleEvent => lifecycleEvent.AuditResidueId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(lifecycleEvent => lifecycleEvent.TraceId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(lifecycleEvent => lifecycleEvent.OperationName)
            .HasMaxLength(OperationNameMaxLength);

        _ = builder.Property(lifecycleEvent => lifecycleEvent.Outcome)
            .HasMaxLength(OutcomeMaxLength);

        _ = builder.Property(lifecycleEvent => lifecycleEvent.MetadataJson)
            .IsRequired();

        _ = builder.HasIndex(lifecycleEvent => lifecycleEvent.EventId)
            .IsUnique();

        _ = builder.HasIndex(lifecycleEvent => lifecycleEvent.Stage);

        _ = builder.HasIndex(lifecycleEvent => lifecycleEvent.StageSequence);

        _ = builder.HasIndex(lifecycleEvent => lifecycleEvent.OccurredUtc);

        _ = builder.HasIndex(lifecycleEvent => lifecycleEvent.CorrelationId);

        _ = builder.HasIndex(lifecycleEvent => lifecycleEvent.AuditResidueId);

        _ = builder.HasIndex(lifecycleEvent => lifecycleEvent.TraceId);

        _ = builder.HasIndex(lifecycleEvent => lifecycleEvent.OperationName);

        _ = builder.HasIndex(lifecycleEvent => lifecycleEvent.Outcome);

        _ = builder.HasIndex(lifecycleEvent => new
        {
            lifecycleEvent.CorrelationId,
            lifecycleEvent.OccurredUtc
        });

        _ = builder.HasIndex(lifecycleEvent => new
        {
            lifecycleEvent.AuditResidueId,
            lifecycleEvent.OccurredUtc
        });
    }
}
