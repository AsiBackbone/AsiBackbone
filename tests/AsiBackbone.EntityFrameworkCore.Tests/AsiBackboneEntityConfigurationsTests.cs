using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Handshakes;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests;

/// <summary>
/// Unit tests for verifying that the entity configurations defined in the AsiBackbone.EntityFrameworkCore assembly
/// </summary>
public sealed class AsiBackboneEntityConfigurationsTests
{
    /// <summary>
    /// Verifies that the entity types for the audit ledger records, reason codes, and metadata are included in the model when applying the configurations.
    /// </summary>
    [Fact]
    public void ApplyAsiBackboneConfigurationsAddsAuditLedgerEntityTypes()
    {
        using HostOwnedDbContext context = CreateContext();

        Assert.NotNull(context.Model.FindEntityType(typeof(AuditLedgerRecordEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AuditLedgerReasonCodeEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AuditLedgerMetadataEntity)));
    }

    /// <summary>
    /// Verifies that the entity types for the handshake requests, request metadata, acknowledgments, and acknowledgment metadata are included in the model when applying the configurations.
    /// </summary>
    [Fact]
    public void ApplyAsiBackboneConfigurationsAddsHandshakeEntityTypes()
    {
        using HostOwnedDbContext context = CreateContext();

        Assert.NotNull(context.Model.FindEntityType(typeof(HandshakeRequestEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(HandshakeRequestMetadataEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(HandshakeAcknowledgmentEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(HandshakeAcknowledgmentMetadataEntity)));
    }

    /// <summary>
    /// Verifies that the entity configuration for the audit ledger record entity defines the expected keys, properties, and indexes according to the design of the audit ledger record. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, enum properties are stored as strings, and that indexes are defined on commonly queried properties to optimize performance.
    /// </summary>
    [Fact]
    public void AuditLedgerRecordConfigurationDefinesKeysPropertiesAndIndexes()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AuditLedgerRecordEntity>(context);

        Assert.Equal("AsiBackboneAuditLedgerRecords", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AuditLedgerRecordEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AuditLedgerRecordEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequiredMaxLength(entityType, nameof(AuditLedgerRecordEntity.RecordId), 128);
        AssertRequiredMaxLength(entityType, nameof(AuditLedgerRecordEntity.EventId), 128);
        AssertRequired(entityType, nameof(AuditLedgerRecordEntity.OccurredUtc));
        AssertRequired(entityType, nameof(AuditLedgerRecordEntity.RecordedUtc));
        AssertRequiredMaxLength(entityType, nameof(AuditLedgerRecordEntity.ActorId), 128);
        AssertRequiredMaxLength(entityType, nameof(AuditLedgerRecordEntity.ActorType), 64);
        AssertStoresEnumAsString(entityType, nameof(AuditLedgerRecordEntity.ActorType));
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.ActorDisplayName), 256);
        AssertRequiredMaxLength(entityType, nameof(AuditLedgerRecordEntity.OperationName), 256);
        AssertRequiredMaxLength(entityType, nameof(AuditLedgerRecordEntity.Outcome), 128);
        AssertRequiredMaxLength(entityType, nameof(AuditLedgerRecordEntity.ReasonCodesJson), 65536);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.CorrelationId), 128);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.TraceId), 128);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.PolicyVersion), 128);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.PolicyHash), 512);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.HandshakeId), 128);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.AcknowledgmentId), 128);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.CapabilityTokenId), 128);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.PreviousRecordHash), 512);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.RecordHash), 512);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.SignatureKeyId), 128);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.SignatureAlgorithm), 128);
        AssertOptionalMaxLength(entityType, nameof(AuditLedgerRecordEntity.SignatureValue), 16384);
        AssertRequiredMaxLength(entityType, nameof(AuditLedgerRecordEntity.MetadataJson), 65536);

        Assert.Equal(6, entityType.GetIndexes().Count());
        AssertHasUniqueIndex(entityType, nameof(AuditLedgerRecordEntity.RecordId));
        AssertHasIndex(
            entityType,
            nameof(AuditLedgerRecordEntity.RecordedUtc),
            nameof(AuditLedgerRecordEntity.RecordId));

        AssertHasIndex(
            entityType,
            nameof(AuditLedgerRecordEntity.ActorId),
            nameof(AuditLedgerRecordEntity.RecordedUtc),
            nameof(AuditLedgerRecordEntity.RecordId));

        AssertHasIndex(
            entityType,
            nameof(AuditLedgerRecordEntity.CorrelationId),
            nameof(AuditLedgerRecordEntity.RecordedUtc),
            nameof(AuditLedgerRecordEntity.RecordId));

        AssertHasIndex(
            entityType,
            nameof(AuditLedgerRecordEntity.TraceId),
            nameof(AuditLedgerRecordEntity.RecordedUtc),
            nameof(AuditLedgerRecordEntity.RecordId));

        AssertHasIndex(entityType, nameof(AuditLedgerRecordEntity.PreviousRecordHash));
    }

    /// <summary>
    /// Verifies that the entity configuration for the audit ledger reason code entity defines the expected keys, properties, relationships, and indexes according to the design of the audit ledger reason code. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, that a required relationship is defined to the audit ledger record with cascade delete behavior, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that a unique index is defined on the combination of the foreign key to the audit ledger record and the sequence number to enforce uniqueness of reason codes within each audit ledger record.
    /// </summary>
    [Fact]
    public void AuditLedgerReasonCodeConfigurationDefinesRelationshipIndexesAndCascadeDelete()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AuditLedgerReasonCodeEntity>(context);

        Assert.Equal("AsiBackboneAuditLedgerReasonCodes", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AuditLedgerReasonCodeEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AuditLedgerReasonCodeEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequired(entityType, nameof(AuditLedgerReasonCodeEntity.AuditLedgerRecordId));
        AssertRequired(entityType, nameof(AuditLedgerReasonCodeEntity.Sequence));
        AssertRequiredMaxLength(entityType, nameof(AuditLedgerReasonCodeEntity.ReasonCode), 256);

        AssertHasCascadeForeignKey(
            entityType,
            typeof(AuditLedgerRecordEntity),
            nameof(AuditLedgerReasonCodeEntity.AuditLedgerRecordId));

        AssertHasIndex(entityType, nameof(AuditLedgerReasonCodeEntity.AuditLedgerRecordId));
        AssertHasIndex(entityType, nameof(AuditLedgerReasonCodeEntity.ReasonCode));

        AssertHasUniqueIndex(
            entityType,
            nameof(AuditLedgerReasonCodeEntity.AuditLedgerRecordId),
            nameof(AuditLedgerReasonCodeEntity.Sequence));

        AssertHasIndex(
            entityType,
            nameof(AuditLedgerReasonCodeEntity.AuditLedgerRecordId),
            nameof(AuditLedgerReasonCodeEntity.ReasonCode));
    }

    /// <summary>
    /// Verifies that the entity configuration for the audit ledger metadata entity defines the expected keys, properties, relationships, and indexes according to the design of the audit ledger metadata. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, that a required relationship is defined to the audit ledger record with cascade delete behavior, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that a unique index is defined on the combination of the foreign key to the audit ledger record and the metadata key to enforce uniqueness of metadata keys within each audit ledger record.
    /// </summary>
    [Fact]
    public void AuditLedgerMetadataConfigurationDefinesRelationshipIndexesAndCascadeDelete()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AuditLedgerMetadataEntity>(context);

        Assert.Equal("AsiBackboneAuditLedgerMetadata", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AuditLedgerMetadataEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AuditLedgerMetadataEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequired(entityType, nameof(AuditLedgerMetadataEntity.AuditLedgerRecordId));
        AssertRequiredMaxLength(entityType, nameof(AuditLedgerMetadataEntity.MetadataKey), 256);
        AssertRequiredMaxLength(entityType, nameof(AuditLedgerMetadataEntity.MetadataValue), 4096);

        AssertHasCascadeForeignKey(
            entityType,
            typeof(AuditLedgerRecordEntity),
            nameof(AuditLedgerMetadataEntity.AuditLedgerRecordId));

        AssertHasIndex(entityType, nameof(AuditLedgerMetadataEntity.AuditLedgerRecordId));
        AssertHasIndex(entityType, nameof(AuditLedgerMetadataEntity.MetadataKey));

        AssertHasUniqueIndex(
            entityType,
            nameof(AuditLedgerMetadataEntity.AuditLedgerRecordId),
            nameof(AuditLedgerMetadataEntity.MetadataKey));
    }

    /// <summary>
    /// Verifies that the entity configuration for the handshake request entity defines the expected keys, properties, and indexes according to the design of the handshake request. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, enum properties are stored as strings, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that composite indexes are defined on combinations of properties that are commonly queried together to further optimize query performance.
    /// </summary>
    [Fact]
    public void HandshakeRequestConfigurationDefinesKeysPropertiesAndIndexes()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<HandshakeRequestEntity>(context);

        Assert.Equal("AsiBackboneHandshakeRequests", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(HandshakeRequestEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(HandshakeRequestEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequiredMaxLength(entityType, nameof(HandshakeRequestEntity.HandshakeId), 128);
        AssertRequiredMaxLength(entityType, nameof(HandshakeRequestEntity.ActorId), 128);
        AssertRequiredMaxLength(entityType, nameof(HandshakeRequestEntity.ActorType), 64);
        AssertStoresEnumAsString(entityType, nameof(HandshakeRequestEntity.ActorType));
        AssertOptionalMaxLength(entityType, nameof(HandshakeRequestEntity.ActorDisplayName), 256);
        AssertRequiredMaxLength(entityType, nameof(HandshakeRequestEntity.OperationName), 256);
        AssertRequiredMaxLength(entityType, nameof(HandshakeRequestEntity.ReasonCode), 256);
        AssertRequired(entityType, nameof(HandshakeRequestEntity.Message));
        AssertRequiredMaxLength(entityType, nameof(HandshakeRequestEntity.RequiredAcknowledgmentCode), 128);
        AssertRequired(entityType, nameof(HandshakeRequestEntity.RequiredAcknowledgmentText));
        AssertRequiredMaxLength(entityType, nameof(HandshakeRequestEntity.RiskLevel), 64);
        AssertStoresEnumAsString(entityType, nameof(HandshakeRequestEntity.RiskLevel));
        AssertOptionalMaxLength(entityType, nameof(HandshakeRequestEntity.RiskCategory), 128);
        AssertOptionalMaxLength(entityType, nameof(HandshakeRequestEntity.CorrelationId), 128);
        AssertOptionalMaxLength(entityType, nameof(HandshakeRequestEntity.TraceId), 128);
        AssertOptionalMaxLength(entityType, nameof(HandshakeRequestEntity.PolicyVersion), 128);
        AssertOptionalMaxLength(entityType, nameof(HandshakeRequestEntity.PolicyHash), 512);

        AssertHasUniqueIndex(entityType, nameof(HandshakeRequestEntity.HandshakeId));
        AssertHasIndex(entityType, nameof(HandshakeRequestEntity.ActorId));
        AssertHasIndex(entityType, nameof(HandshakeRequestEntity.ActorType));
        AssertHasIndex(entityType, nameof(HandshakeRequestEntity.OperationName));
        AssertHasIndex(entityType, nameof(HandshakeRequestEntity.ReasonCode));
        AssertHasIndex(entityType, nameof(HandshakeRequestEntity.RequiredAcknowledgmentCode));
        AssertHasIndex(entityType, nameof(HandshakeRequestEntity.RiskLevel));
        AssertHasIndex(entityType, nameof(HandshakeRequestEntity.RiskCategory));
        AssertHasIndex(entityType, nameof(HandshakeRequestEntity.CorrelationId));
        AssertHasIndex(entityType, nameof(HandshakeRequestEntity.TraceId));
        AssertHasIndex(entityType, nameof(HandshakeRequestEntity.PolicyVersion));
        AssertHasIndex(entityType, nameof(HandshakeRequestEntity.PolicyHash));

        AssertHasIndex(
            entityType,
            nameof(HandshakeRequestEntity.ActorId),
            nameof(HandshakeRequestEntity.OperationName));

        AssertHasIndex(
            entityType,
            nameof(HandshakeRequestEntity.CorrelationId),
            nameof(HandshakeRequestEntity.OperationName));

        AssertHasIndex(
            entityType,
            nameof(HandshakeRequestEntity.PolicyVersion),
            nameof(HandshakeRequestEntity.PolicyHash));
    }

    /// <summary>
    /// Verifies that the entity configuration for the handshake request metadata entity defines the expected keys, properties, relationships, and indexes according to the design of the handshake request metadata. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, that a required relationship is defined to the handshake request with cascade delete behavior, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that a unique index is defined on the combination of the foreign key to the handshake request and the metadata key to enforce uniqueness of metadata keys within each handshake request.
    /// </summary>
    [Fact]
    public void HandshakeRequestMetadataConfigurationDefinesRelationshipIndexesAndCascadeDelete()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<HandshakeRequestMetadataEntity>(context);

        Assert.Equal("AsiBackboneHandshakeRequestMetadata", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(HandshakeRequestMetadataEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(HandshakeRequestMetadataEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequired(entityType, nameof(HandshakeRequestMetadataEntity.HandshakeRequestId));
        AssertRequiredMaxLength(entityType, nameof(HandshakeRequestMetadataEntity.MetadataKey), 256);
        AssertRequiredMaxLength(entityType, nameof(HandshakeRequestMetadataEntity.MetadataValue), 4096);

        AssertHasCascadeForeignKey(
            entityType,
            typeof(HandshakeRequestEntity),
            nameof(HandshakeRequestMetadataEntity.HandshakeRequestId));

        AssertHasIndex(entityType, nameof(HandshakeRequestMetadataEntity.HandshakeRequestId));
        AssertHasIndex(entityType, nameof(HandshakeRequestMetadataEntity.MetadataKey));

        AssertHasUniqueIndex(
            entityType,
            nameof(HandshakeRequestMetadataEntity.HandshakeRequestId),
            nameof(HandshakeRequestMetadataEntity.MetadataKey));
    }

    /// <summary>
    /// Verifies that the entity configuration for the handshake acknowledgment entity defines the expected keys, properties, and indexes according to the design of the handshake acknowledgment. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, enum properties are stored as strings, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that composite indexes are defined on combinations of properties that are commonly queried together to further optimize query performance.
    /// </summary>
    [Fact]
    public void HandshakeAcknowledgmentConfigurationDefinesKeysPropertiesAndIndexes()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<HandshakeAcknowledgmentEntity>(context);

        Assert.Equal("AsiBackboneHandshakeAcknowledgments", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(HandshakeAcknowledgmentEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(HandshakeAcknowledgmentEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequiredMaxLength(entityType, nameof(HandshakeAcknowledgmentEntity.AcknowledgmentId), 128);
        AssertRequiredMaxLength(entityType, nameof(HandshakeAcknowledgmentEntity.HandshakeId), 128);
        AssertRequiredMaxLength(entityType, nameof(HandshakeAcknowledgmentEntity.ActorId), 128);
        AssertRequiredMaxLength(entityType, nameof(HandshakeAcknowledgmentEntity.ActorType), 64);
        AssertStoresEnumAsString(entityType, nameof(HandshakeAcknowledgmentEntity.ActorType));
        AssertOptionalMaxLength(entityType, nameof(HandshakeAcknowledgmentEntity.ActorDisplayName), 256);
        AssertRequiredMaxLength(entityType, nameof(HandshakeAcknowledgmentEntity.AcknowledgmentCode), 128);
        AssertRequired(entityType, nameof(HandshakeAcknowledgmentEntity.Acknowledged));
        AssertRequired(entityType, nameof(HandshakeAcknowledgmentEntity.OccurredUtc));
        AssertOptionalMaxLength(entityType, nameof(HandshakeAcknowledgmentEntity.CorrelationId), 128);
        AssertOptionalMaxLength(entityType, nameof(HandshakeAcknowledgmentEntity.TraceId), 128);

        AssertHasUniqueIndex(entityType, nameof(HandshakeAcknowledgmentEntity.AcknowledgmentId));
        AssertHasIndex(entityType, nameof(HandshakeAcknowledgmentEntity.HandshakeId));
        AssertHasIndex(entityType, nameof(HandshakeAcknowledgmentEntity.ActorId));
        AssertHasIndex(entityType, nameof(HandshakeAcknowledgmentEntity.ActorType));
        AssertHasIndex(entityType, nameof(HandshakeAcknowledgmentEntity.AcknowledgmentCode));
        AssertHasIndex(entityType, nameof(HandshakeAcknowledgmentEntity.Acknowledged));
        AssertHasIndex(entityType, nameof(HandshakeAcknowledgmentEntity.OccurredUtc));
        AssertHasIndex(entityType, nameof(HandshakeAcknowledgmentEntity.CorrelationId));
        AssertHasIndex(entityType, nameof(HandshakeAcknowledgmentEntity.TraceId));

        AssertHasIndex(
            entityType,
            nameof(HandshakeAcknowledgmentEntity.HandshakeId),
            nameof(HandshakeAcknowledgmentEntity.OccurredUtc));

        AssertHasIndex(
            entityType,
            nameof(HandshakeAcknowledgmentEntity.ActorId),
            nameof(HandshakeAcknowledgmentEntity.OccurredUtc));

        AssertHasIndex(
            entityType,
            nameof(HandshakeAcknowledgmentEntity.CorrelationId),
            nameof(HandshakeAcknowledgmentEntity.OccurredUtc));
    }

    /// <summary>
    /// Verifies that the entity configuration for the handshake acknowledgment metadata entity defines the expected keys, properties, relationships, and indexes according to the design of the handshake acknowledgment metadata. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, that a required relationship is defined to the handshake acknowledgment with cascade delete behavior, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that a unique index is defined on the combination of the foreign key to the handshake acknowledgment and the metadata key to enforce uniqueness of metadata keys within each handshake acknowledgment.
    /// </summary>
    [Fact]
    public void HandshakeAcknowledgmentMetadataConfigurationDefinesRelationshipIndexesAndCascadeDelete()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<HandshakeAcknowledgmentMetadataEntity>(context);

        Assert.Equal("AsiBackboneHandshakeAcknowledgmentMetadata", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(HandshakeAcknowledgmentMetadataEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(HandshakeAcknowledgmentMetadataEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequired(entityType, nameof(HandshakeAcknowledgmentMetadataEntity.HandshakeAcknowledgmentId));
        AssertRequiredMaxLength(entityType, nameof(HandshakeAcknowledgmentMetadataEntity.MetadataKey), 256);
        AssertRequiredMaxLength(entityType, nameof(HandshakeAcknowledgmentMetadataEntity.MetadataValue), 4096);

        AssertHasCascadeForeignKey(
            entityType,
            typeof(HandshakeAcknowledgmentEntity),
            nameof(HandshakeAcknowledgmentMetadataEntity.HandshakeAcknowledgmentId));

        AssertHasIndex(entityType, nameof(HandshakeAcknowledgmentMetadataEntity.HandshakeAcknowledgmentId));
        AssertHasIndex(entityType, nameof(HandshakeAcknowledgmentMetadataEntity.MetadataKey));

        AssertHasUniqueIndex(
            entityType,
            nameof(HandshakeAcknowledgmentMetadataEntity.HandshakeAcknowledgmentId),
            nameof(HandshakeAcknowledgmentMetadataEntity.MetadataKey));
    }

    /// <summary>
    /// Verifies that entities configured with the configurations defined in the AsiBackbone.EntityFrameworkCore assembly can be successfully persisted to the database using a host-owned DbContext. This includes verifying that entities can be added and saved, that relationships are properly established, and that properties are correctly mapped to the database schema as defined by the entity configurations. Additionally, verifies that enum properties are stored as strings in the database and that indexes defined in the configurations are effective for query performance. This test serves as an end-to-end verification of the entity configurations in a real database context to ensure they function as intended when used in an application.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous test operation. The test will complete successfully if entities can be persisted and retrieved with the expected values, and will fail if any exceptions are thrown during the process or if the retrieved entities do not match the expected values based on the configurations.
    /// </returns>
    [Fact]
    public async Task ConfiguredEntitiesCanBePersistedWithHostOwnedDbContext()
    {
        using HostOwnedDbContext context = CreateContext();

        var auditLedgerRecordId = Guid.NewGuid();
        var handshakeRequestId = Guid.NewGuid();
        var handshakeAcknowledgmentId = Guid.NewGuid();

        _ = context.AuditLedgerRecords.Add(new AuditLedgerRecordEntity
        {
            Id = auditLedgerRecordId,
            RecordId = "record-123",
            EventId = "event-123",
            OccurredUtc = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero),
            RecordedUtc = new DateTimeOffset(2026, 6, 5, 12, 1, 0, TimeSpan.Zero),
            ActorId = "service-123",
            ActorType = GovernanceActorType.Service,
            ActorDisplayName = "Service",
            OperationName = "document.approve",
            Outcome = "Allowed",
            ReasonCodesJson = "[\"policy.allowed\"]",
            CorrelationId = "correlation-123",
            TraceId = "trace-123",
            PolicyVersion = "v1",
            PolicyHash = "hash-123",
            HandshakeId = "handshake-123",
            AcknowledgmentId = "acknowledgment-123",
            CapabilityTokenId = "capability-token-123",
            PreviousRecordHash = "previous-record-hash",
            RecordHash = "record-hash",
            SignatureKeyId = "key-123",
            SignatureAlgorithm = "HMACSHA256",
            SignatureValue = "signature-value",
            MetadataJson = /*lang=json,strict*/ "{\"source\":\"unit-test\"}"
        });

        _ = context.AuditLedgerReasonCodes.Add(new AuditLedgerReasonCodeEntity
        {
            Id = Guid.NewGuid(),
            AuditLedgerRecordId = auditLedgerRecordId,
            Sequence = 0,
            ReasonCode = "policy.allowed"
        });

        _ = context.AuditLedgerMetadata.Add(new AuditLedgerMetadataEntity
        {
            Id = Guid.NewGuid(),
            AuditLedgerRecordId = auditLedgerRecordId,
            MetadataKey = "source",
            MetadataValue = "unit-test"
        });

        _ = context.HandshakeRequests.Add(new HandshakeRequestEntity
        {
            Id = handshakeRequestId,
            HandshakeId = "handshake-123",
            ActorId = "service-123",
            ActorType = GovernanceActorType.Service,
            ActorDisplayName = "Service",
            OperationName = "document.approve",
            ReasonCode = "ack.required",
            Message = "Acknowledgment is required.",
            RequiredAcknowledgmentCode = "ACK-001",
            RequiredAcknowledgmentText = "I understand this action is consequential.",
            RiskLevel = LiabilityHandshakeRiskLevel.High,
            RiskCategory = "administrative",
            CorrelationId = "correlation-123",
            TraceId = "trace-123",
            PolicyVersion = "v1",
            PolicyHash = "hash-123"
        });

        _ = context.HandshakeRequestMetadata.Add(new HandshakeRequestMetadataEntity
        {
            Id = Guid.NewGuid(),
            HandshakeRequestId = handshakeRequestId,
            MetadataKey = "source",
            MetadataValue = "unit-test"
        });

        _ = context.HandshakeAcknowledgments.Add(new HandshakeAcknowledgmentEntity
        {
            Id = handshakeAcknowledgmentId,
            AcknowledgmentId = "acknowledgment-123",
            HandshakeId = "handshake-123",
            ActorId = "service-123",
            ActorType = GovernanceActorType.Service,
            ActorDisplayName = "Service",
            AcknowledgmentCode = "ACK-001",
            Acknowledged = true,
            OccurredUtc = new DateTimeOffset(2026, 6, 5, 12, 2, 0, TimeSpan.Zero),
            CorrelationId = "correlation-123",
            TraceId = "trace-123"
        });

        _ = context.HandshakeAcknowledgmentMetadata.Add(new HandshakeAcknowledgmentMetadataEntity
        {
            Id = Guid.NewGuid(),
            HandshakeAcknowledgmentId = handshakeAcknowledgmentId,
            MetadataKey = "source",
            MetadataValue = "unit-test"
        });

        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        Assert.Equal(1, await context.AuditLedgerRecords.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.AuditLedgerReasonCodes.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.AuditLedgerMetadata.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.HandshakeRequests.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.HandshakeRequestMetadata.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.HandshakeAcknowledgments.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.HandshakeAcknowledgmentMetadata.CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        AuditLedgerRecordEntity auditRecord = await context.AuditLedgerRecords.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(GovernanceActorType.Service, auditRecord.ActorType);

        HandshakeRequestEntity handshakeRequest = await context.HandshakeRequests.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(LiabilityHandshakeRiskLevel.High, handshakeRequest.RiskLevel);

        HandshakeAcknowledgmentEntity acknowledgment = await context.HandshakeAcknowledgments.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(acknowledgment.Acknowledged);
    }

    private static HostOwnedDbContext CreateContext()
    {
        DbContextOptions<HostOwnedDbContext> options =
            new DbContextOptionsBuilder<HostOwnedDbContext>()
                .UseInMemoryDatabase($"asi-backbone-ef-core-{Guid.NewGuid():N}")
                .Options;

        return new HostOwnedDbContext(options);
    }

    private static IEntityType GetEntityType<TEntity>(DbContext context)
    {
        return context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type '{typeof(TEntity).Name}' was not found.");
    }

    private static IProperty GetProperty(IEntityType entityType, string propertyName)
    {
        return entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Property '{propertyName}' was not found on entity type '{entityType.ClrType.Name}'.");
    }

    private static void AssertPrimaryKey(IEntityType entityType, string propertyName)
    {
        IKey primaryKey = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException($"Primary key was not found on entity type '{entityType.ClrType.Name}'.");

        IProperty property = Assert.Single(primaryKey.Properties);

        Assert.Equal(propertyName, property.Name);
    }

    private static void AssertValueGeneratedNever(IEntityType entityType, string propertyName)
    {
        IProperty property = GetProperty(entityType, propertyName);

        Assert.Equal(ValueGenerated.Never, property.ValueGenerated);
    }

    private static void AssertConcurrencyStamp(IEntityType entityType)
    {
        IProperty property = GetProperty(entityType, "ConcurrencyStamp");

        Assert.False(property.IsNullable);
        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(64, property.GetMaxLength());
    }

    private static void AssertRequired(IEntityType entityType, string propertyName)
    {
        IProperty property = GetProperty(entityType, propertyName);

        Assert.False(property.IsNullable);
    }

    private static void AssertRequiredMaxLength(IEntityType entityType, string propertyName, int maxLength)
    {
        IProperty property = GetProperty(entityType, propertyName);

        Assert.False(property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    private static void AssertOptionalMaxLength(IEntityType entityType, string propertyName, int maxLength)
    {
        IProperty property = GetProperty(entityType, propertyName);

        Assert.True(property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    private static void AssertStoresEnumAsString(IEntityType entityType, string propertyName)
    {
        IProperty property = GetProperty(entityType, propertyName);

        Assert.Equal(typeof(string), property.GetProviderClrType());
    }

    private static void AssertHasIndex(IEntityType entityType, params string[] propertyNames)
    {
        bool hasIndex = entityType.GetIndexes()
            .Any(index => index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

        Assert.True(
            hasIndex,
            $"Expected index on {entityType.ClrType.Name}({string.Join(", ", propertyNames)}).");
    }

    private static void AssertHasUniqueIndex(IEntityType entityType, params string[] propertyNames)
    {
        bool hasIndex = entityType.GetIndexes()
            .Any(index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

        Assert.True(
            hasIndex,
            $"Expected unique index on {entityType.ClrType.Name}({string.Join(", ", propertyNames)}).");
    }

    private static void AssertHasCascadeForeignKey(
        IEntityType dependentEntityType,
        Type principalClrType,
        string foreignKeyPropertyName)
    {
        IForeignKey? foreignKey = dependentEntityType.GetForeignKeys()
            .SingleOrDefault(candidate =>
                candidate.PrincipalEntityType.ClrType == principalClrType &&
                candidate.Properties.Any(property => property.Name == foreignKeyPropertyName));

        Assert.NotNull(foreignKey);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    private sealed class HostOwnedDbContext(DbContextOptions<HostOwnedDbContext> options)
        : DbContext(options)
    {
        public DbSet<AuditLedgerRecordEntity> AuditLedgerRecords =>
            Set<AuditLedgerRecordEntity>();

        public DbSet<AuditLedgerReasonCodeEntity> AuditLedgerReasonCodes =>
            Set<AuditLedgerReasonCodeEntity>();

        public DbSet<AuditLedgerMetadataEntity> AuditLedgerMetadata =>
            Set<AuditLedgerMetadataEntity>();

        public DbSet<HandshakeRequestEntity> HandshakeRequests =>
            Set<HandshakeRequestEntity>();

        public DbSet<HandshakeRequestMetadataEntity> HandshakeRequestMetadata =>
            Set<HandshakeRequestMetadataEntity>();

        public DbSet<HandshakeAcknowledgmentEntity> HandshakeAcknowledgments =>
            Set<HandshakeAcknowledgmentEntity>();

        public DbSet<HandshakeAcknowledgmentMetadataEntity> HandshakeAcknowledgmentMetadata =>
            Set<HandshakeAcknowledgmentMetadataEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
