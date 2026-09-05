using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsiBackbone.EntityFrameworkCore.Configurations;

/// <summary>
/// Configures the Entity Framework Core persistence mapping for <see cref="AsiBackboneAuditLedgerRecordEntity" />.
/// </summary>
public sealed class AsiBackboneAuditLedgerRecordEntityConfiguration
    : IEntityTypeConfiguration<AsiBackboneAuditLedgerRecordEntity>
{
    private const int IdentifierMaxLength = 128;
    private const int SchemaVersionMaxLength = 64;
    private const int DisplayNameMaxLength = 256;
    private const int OperationNameMaxLength = 256;
    private const int OutcomeMaxLength = 128;
    private const int ActorTypeMaxLength = 64;
    private const int CorrelationMaxLength = 128;
    private const int PolicyVersionMaxLength = 128;
    private const int PolicyScopeMaxLength = 256;
    private const int StatusMaxLength = 128;
    private const int ProviderMaxLength = 128;
    private const int StageMaxLength = 128;
    private const int HashMaxLength = 512;
    private const int SignatureKeyIdMaxLength = 128;
    private const int SignatureKeyVersionMaxLength = 128;
    private const int SignatureAlgorithmMaxLength = 128;
    private const int SignatureProviderMaxLength = 128;
    private const int SignatureValueMaxLength = 16384;
    private const int SerializedJsonMaxLength = 65536;
    private const int ConcurrencyStampMaxLength = 64;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AsiBackboneAuditLedgerRecordEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable("AsiBackboneAuditLedgerRecords");

        _ = builder.HasKey(record => record.Id);

        _ = builder.Property(record => record.Id)
            .ValueGeneratedNever();

        _ = builder.Property(record => record.ConcurrencyStamp)
            .IsRequired()
            .HasMaxLength(ConcurrencyStampMaxLength)
            .IsConcurrencyToken();

        _ = builder.Property(record => record.RecordId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(record => record.SchemaVersion)
            .IsRequired()
            .HasMaxLength(SchemaVersionMaxLength);

        _ = builder.Property(record => record.EventId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(record => record.AuditResidueId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(record => record.OccurredUtc)
            .IsRequired();

        _ = builder.Property(record => record.RecordedUtc)
            .IsRequired();

        _ = builder.Property(record => record.ActorId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(record => record.ActorType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(ActorTypeMaxLength);

        _ = builder.Property(record => record.ActorDisplayName)
            .HasMaxLength(DisplayNameMaxLength);

        _ = builder.Property(record => record.OperationName)
            .IsRequired()
            .HasMaxLength(OperationNameMaxLength);

        _ = builder.Property(record => record.Outcome)
            .IsRequired()
            .HasMaxLength(OutcomeMaxLength);

        _ = builder.Property(record => record.ReasonCodesJson)
            .IsRequired()
            .HasMaxLength(SerializedJsonMaxLength);

        _ = builder.Property(record => record.CorrelationId)
            .HasMaxLength(CorrelationMaxLength);

        _ = builder.Property(record => record.TraceId)
            .HasMaxLength(CorrelationMaxLength);

        _ = builder.Property(record => record.SpanId)
            .HasMaxLength(CorrelationMaxLength);

        _ = builder.Property(record => record.ParentSpanId)
            .HasMaxLength(CorrelationMaxLength);

        _ = builder.Property(record => record.ConstraintSetHash)
            .HasMaxLength(HashMaxLength);

        _ = builder.Property(record => record.PolicyScope)
            .HasMaxLength(PolicyScopeMaxLength);

        _ = builder.Property(record => record.TenantHash)
            .HasMaxLength(HashMaxLength);

        _ = builder.Property(record => record.OrganizationHash)
            .HasMaxLength(HashMaxLength);

        _ = builder.Property(record => record.EmitterStatus)
            .HasMaxLength(StatusMaxLength);

        _ = builder.Property(record => record.EmitterProvider)
            .HasMaxLength(ProviderMaxLength);

        _ = builder.Property(record => record.GatewayExecutionId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(record => record.DecisionStage)
            .HasMaxLength(StageMaxLength);

        _ = builder.Property(record => record.PolicyVersion)
            .HasMaxLength(PolicyVersionMaxLength);

        _ = builder.Property(record => record.PolicyHash)
            .HasMaxLength(HashMaxLength);

        _ = builder.Property(record => record.HandshakeId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(record => record.AcknowledgmentId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(record => record.CapabilityTokenId)
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(record => record.PreviousRecordHash)
            .HasMaxLength(HashMaxLength);

        _ = builder.Property(record => record.RecordHash)
            .HasMaxLength(HashMaxLength);

        _ = builder.Property(record => record.SigningHash)
            .HasMaxLength(HashMaxLength);

        _ = builder.Property(record => record.SignatureKeyId)
            .HasMaxLength(SignatureKeyIdMaxLength);

        _ = builder.Property(record => record.SignatureKeyVersion)
            .HasMaxLength(SignatureKeyVersionMaxLength);

        _ = builder.Property(record => record.SignatureAlgorithm)
            .HasMaxLength(SignatureAlgorithmMaxLength);

        _ = builder.Property(record => record.SignatureValue)
            .HasMaxLength(SignatureValueMaxLength);

        _ = builder.Property(record => record.SignatureProvider)
            .HasMaxLength(SignatureProviderMaxLength);

        _ = builder.Property(record => record.SignedUtc);

        _ = builder.Property(record => record.MetadataJson)
            .IsRequired()
            .HasMaxLength(SerializedJsonMaxLength);

        _ = builder.HasIndex(record => record.RecordId)
            .IsUnique();

        _ = builder.HasIndex(record => new
        {
            record.RecordedUtc,
            record.RecordId
        });

        _ = builder.HasIndex(record => new
        {
            record.ActorId,
            record.RecordedUtc,
            record.RecordId
        });

        _ = builder.HasIndex(record => new
        {
            record.CorrelationId,
            record.RecordedUtc,
            record.RecordId
        });

        _ = builder.HasIndex(record => new
        {
            record.TraceId,
            record.RecordedUtc,
            record.RecordId
        });

        _ = builder.HasIndex(record => record.PreviousRecordHash);
    }
}
