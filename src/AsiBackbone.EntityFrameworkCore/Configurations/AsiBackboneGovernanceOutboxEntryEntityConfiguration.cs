using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AsiBackbone.EntityFrameworkCore.Configurations;

/// <summary>
/// Configures the Entity Framework Core persistence mapping for <see cref="AsiBackboneGovernanceOutboxEntryEntity" />.
/// </summary>
public sealed class AsiBackboneGovernanceOutboxEntryEntityConfiguration
    : IEntityTypeConfiguration<AsiBackboneGovernanceOutboxEntryEntity>
{
    private const int IdentifierMaxLength = 128;
    private const int SchemaVersionMaxLength = 64;
    private const int StatusMaxLength = 128;
    private const int ProviderMaxLength = 128;
    private const int ErrorCodeMaxLength = 128;
    private const int ProviderErrorCodeMaxLength = 128;
    private const int StageMaxLength = 128;
    private const int OperationNameMaxLength = 256;
    private const int OutcomeMaxLength = 128;
    private const int PolicyVersionMaxLength = 128;
    private const int HashMaxLength = 512;
    private const int ContentTypeMaxLength = 256;
    private const int PayloadTypeMaxLength = 128;
    private const int DiagnosticTextMaxLength = 4096;
    private const int SerializedJsonMaxLength = 65536;
    private const int ConcurrencyStampMaxLength = 64;

    private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetToUtcTicksConverter = new(
        value => value.ToUniversalTime().Ticks,
        value => new DateTimeOffset(value, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetToUtcTicksConverter = new(
        value => value.HasValue ? value.Value.ToUniversalTime().Ticks : null,
        value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AsiBackboneGovernanceOutboxEntryEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable("AsiBackboneGovernanceOutboxEntries");

        _ = builder.HasKey(outboxEntry => outboxEntry.Id);

        _ = builder.Property(outboxEntry => outboxEntry.Id)
            .ValueGeneratedNever();

        _ = builder.Property(outboxEntry => outboxEntry.ConcurrencyStamp)
            .IsRequired()
            .HasMaxLength(ConcurrencyStampMaxLength)
            .IsConcurrencyToken();

        _ = builder.Property(outboxEntry => outboxEntry.OutboxEntryId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(StatusMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.CreatedUtc)
            .HasConversion(DateTimeOffsetToUtcTicksConverter)
            .IsRequired();

        _ = builder.Property(outboxEntry => outboxEntry.UpdatedUtc)
            .HasConversion(DateTimeOffsetToUtcTicksConverter)
            .IsRequired();

        _ = builder.Property(outboxEntry => outboxEntry.DeliveredUtc)
            .HasConversion(NullableDateTimeOffsetToUtcTicksConverter);

        _ = builder.Property(outboxEntry => outboxEntry.NextRetryUtc)
            .HasConversion(NullableDateTimeOffsetToUtcTicksConverter);

        _ = builder.Property(outboxEntry => outboxEntry.RetryCount)
            .IsRequired();

        _ = builder.Property(outboxEntry => outboxEntry.MaxRetryCount)
            .IsRequired();

        _ = builder.Property(outboxEntry => outboxEntry.ProviderName)
            .HasMaxLength(ProviderMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.ProviderRecordId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.DeadLetterReason)
            .HasMaxLength(DiagnosticTextMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.LastErrorCode)
            .HasMaxLength(ErrorCodeMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.LastErrorMessage)
            .HasMaxLength(DiagnosticTextMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.LastErrorProviderName)
            .HasMaxLength(ProviderMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.LastErrorProviderErrorCode)
            .HasMaxLength(ProviderErrorCodeMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.MetadataJson)
            .IsRequired()
            .HasMaxLength(SerializedJsonMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.ClaimOwner)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.ClaimToken)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.ClaimedUtc)
            .HasConversion(NullableDateTimeOffsetToUtcTicksConverter);

        _ = builder.Property(outboxEntry => outboxEntry.ClaimExpiresUtc)
            .HasConversion(NullableDateTimeOffsetToUtcTicksConverter);

        _ = builder.Property(outboxEntry => outboxEntry.ClaimAttemptCount)
            .IsRequired();

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeSchemaVersion)
            .IsRequired()
            .HasMaxLength(SchemaVersionMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeEventType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(StatusMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeEventId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeOccurredUtc)
            .HasConversion(DateTimeOffsetToUtcTicksConverter)
            .IsRequired();

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeCreatedUtc)
            .HasConversion(DateTimeOffsetToUtcTicksConverter)
            .IsRequired();

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeCorrelationId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeAuditResidueId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeLifecycleStage)
            .HasConversion<string>()
            .HasMaxLength(StageMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopePolicyVersion)
            .HasMaxLength(PolicyVersionMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopePolicyHash)
            .HasMaxLength(HashMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeTraceId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeSpanId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeParentSpanId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeOperationName)
            .HasMaxLength(OperationNameMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeOutcome)
            .HasMaxLength(OutcomeMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeActorId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeEmitterStatus)
            .HasMaxLength(StatusMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeEmitterProvider)
            .HasMaxLength(ProviderMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeGatewayExecutionId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeDecisionStage)
            .HasMaxLength(StageMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopeMetadataJson)
            .IsRequired()
            .HasMaxLength(SerializedJsonMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopePayloadType)
            .HasMaxLength(PayloadTypeMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopePayloadSchemaVersion)
            .HasMaxLength(SchemaVersionMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopePayloadContentType)
            .HasMaxLength(ContentTypeMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopePayloadContentHash)
            .HasMaxLength(HashMaxLength);

        _ = builder.Property(outboxEntry => outboxEntry.EnvelopePayloadMetadataJson)
            .IsRequired()
            .HasMaxLength(SerializedJsonMaxLength);

        _ = builder.HasIndex(outboxEntry => outboxEntry.OutboxEntryId)
            .IsUnique();

        _ = builder.HasIndex(outboxEntry => outboxEntry.ClaimToken);

        _ = builder.HasIndex(outboxEntry => new
        {
            outboxEntry.Status,
            outboxEntry.ClaimExpiresUtc,
            outboxEntry.CreatedUtc,
            outboxEntry.OutboxEntryId
        });

        _ = builder.HasIndex(outboxEntry => new
        {
            outboxEntry.Status,
            outboxEntry.ClaimExpiresUtc,
            outboxEntry.NextRetryUtc,
            outboxEntry.UpdatedUtc,
            outboxEntry.OutboxEntryId
        });

        _ = builder.HasIndex(outboxEntry => new
        {
            outboxEntry.Status,
            outboxEntry.NextRetryUtc,
            outboxEntry.UpdatedUtc,
            outboxEntry.OutboxEntryId
        });

        _ = builder.HasIndex(outboxEntry => new
        {
            outboxEntry.Status,
            outboxEntry.CreatedUtc,
            outboxEntry.OutboxEntryId
        });

    }
}
