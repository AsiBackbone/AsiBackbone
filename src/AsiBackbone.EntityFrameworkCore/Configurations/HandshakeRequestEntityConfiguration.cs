using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsiBackbone.EntityFrameworkCore.Configurations;

/// <summary>
/// Configures the Entity Framework Core persistence mapping for <see cref="HandshakeRequestEntity" />.
/// </summary>
public sealed class HandshakeRequestEntityConfiguration
    : IEntityTypeConfiguration<HandshakeRequestEntity>
{
    private const int IdentifierMaxLength = 128;
    private const int SchemaVersionMaxLength = 64;
    private const int DisplayNameMaxLength = 256;
    private const int OperationNameMaxLength = 256;
    private const int ActorTypeMaxLength = 64;
    private const int ReasonCodeMaxLength = 256;
    private const int AcknowledgmentCodeMaxLength = 128;
    private const int RiskLevelMaxLength = 64;
    private const int RiskCategoryMaxLength = 128;
    private const int CorrelationMaxLength = 128;
    private const int PolicyVersionMaxLength = 128;
    private const int HashMaxLength = 512;
    private const int ConcurrencyStampMaxLength = 64;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HandshakeRequestEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable("AsiBackboneHandshakeRequests");

        _ = builder.HasKey(request => request.Id);

        _ = builder.Property(request => request.Id)
            .ValueGeneratedNever();

        _ = builder.Property(request => request.ConcurrencyStamp)
            .IsRequired()
            .HasMaxLength(ConcurrencyStampMaxLength)
            .IsConcurrencyToken();

        _ = builder.Property(request => request.HandshakeId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(request => request.SchemaVersion)
            .IsRequired()
            .HasMaxLength(SchemaVersionMaxLength);

        _ = builder.Property(request => request.ActorId)
            .IsRequired()
            .HasMaxLength(IdentifierMaxLength);

        _ = builder.Property(request => request.ActorType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(ActorTypeMaxLength);

        _ = builder.Property(request => request.ActorDisplayName)
            .HasMaxLength(DisplayNameMaxLength);

        _ = builder.Property(request => request.OperationName)
            .IsRequired()
            .HasMaxLength(OperationNameMaxLength);

        _ = builder.Property(request => request.ReasonCode)
            .IsRequired()
            .HasMaxLength(ReasonCodeMaxLength);

        _ = builder.Property(request => request.Message)
            .IsRequired();

        _ = builder.Property(request => request.RequiredAcknowledgmentCode)
            .IsRequired()
            .HasMaxLength(AcknowledgmentCodeMaxLength);

        _ = builder.Property(request => request.RequiredAcknowledgmentText)
            .IsRequired();

        _ = builder.Property(request => request.RiskLevel)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(RiskLevelMaxLength);

        _ = builder.Property(request => request.RiskCategory)
            .HasMaxLength(RiskCategoryMaxLength);

        _ = builder.Property(request => request.CorrelationId)
            .HasMaxLength(CorrelationMaxLength);

        _ = builder.Property(request => request.TraceId)
            .HasMaxLength(CorrelationMaxLength);

        _ = builder.Property(request => request.PolicyVersion)
            .HasMaxLength(PolicyVersionMaxLength);

        _ = builder.Property(request => request.PolicyHash)
            .HasMaxLength(HashMaxLength);

        _ = builder.HasIndex(request => request.HandshakeId)
            .IsUnique();

        _ = builder.HasIndex(request => request.SchemaVersion);

        _ = builder.HasIndex(request => request.ActorId);

        _ = builder.HasIndex(request => request.ActorType);

        _ = builder.HasIndex(request => request.OperationName);

        _ = builder.HasIndex(request => request.ReasonCode);

        _ = builder.HasIndex(request => request.RequiredAcknowledgmentCode);

        _ = builder.HasIndex(request => request.RiskLevel);

        _ = builder.HasIndex(request => request.RiskCategory);

        _ = builder.HasIndex(request => request.CorrelationId);

        _ = builder.HasIndex(request => request.TraceId);

        _ = builder.HasIndex(request => request.PolicyVersion);

        _ = builder.HasIndex(request => request.PolicyHash);

        _ = builder.HasIndex(request => new
        {
            request.ActorId,
            request.OperationName
        });

        _ = builder.HasIndex(request => new
        {
            request.CorrelationId,
            request.OperationName
        });

        _ = builder.HasIndex(request => new
        {
            request.PolicyVersion,
            request.PolicyHash
        });
    }
}
