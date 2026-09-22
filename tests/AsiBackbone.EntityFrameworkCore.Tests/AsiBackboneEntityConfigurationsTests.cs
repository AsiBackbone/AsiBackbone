using AsiBackbone.Core.Acknowledgments;
using AsiBackbone.Core.Actors;
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

        Assert.NotNull(context.Model.FindEntityType(typeof(AcknowledgmentRequestEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AcknowledgmentRequestMetadataEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AcknowledgmentResponseEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AcknowledgmentResponseMetadataEntity)));
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
    public void AcknowledgmentRequestConfigurationDefinesKeysPropertiesAndIndexes()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AcknowledgmentRequestEntity>(context);

        Assert.Equal("AsiBackboneHandshakeRequests", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AcknowledgmentRequestEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AcknowledgmentRequestEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentRequestEntity.HandshakeId), 128);
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentRequestEntity.ActorId), 128);
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentRequestEntity.ActorType), 64);
        AssertStoresEnumAsString(entityType, nameof(AcknowledgmentRequestEntity.ActorType));
        AssertOptionalMaxLength(entityType, nameof(AcknowledgmentRequestEntity.ActorDisplayName), 256);
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentRequestEntity.OperationName), 256);
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentRequestEntity.ReasonCode), 256);
        AssertRequired(entityType, nameof(AcknowledgmentRequestEntity.Message));
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentRequestEntity.RequiredAcknowledgmentCode), 128);
        AssertRequired(entityType, nameof(AcknowledgmentRequestEntity.RequiredAcknowledgmentText));
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentRequestEntity.RiskLevel), 64);
        AssertStoresEnumAsString(entityType, nameof(AcknowledgmentRequestEntity.RiskLevel));
        AssertOptionalMaxLength(entityType, nameof(AcknowledgmentRequestEntity.RiskCategory), 128);
        AssertOptionalMaxLength(entityType, nameof(AcknowledgmentRequestEntity.CorrelationId), 128);
        AssertOptionalMaxLength(entityType, nameof(AcknowledgmentRequestEntity.TraceId), 128);
        AssertOptionalMaxLength(entityType, nameof(AcknowledgmentRequestEntity.PolicyVersion), 128);
        AssertOptionalMaxLength(entityType, nameof(AcknowledgmentRequestEntity.PolicyHash), 512);

        AssertHasUniqueIndex(entityType, nameof(AcknowledgmentRequestEntity.HandshakeId));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestEntity.ActorId));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestEntity.ActorType));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestEntity.OperationName));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestEntity.ReasonCode));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestEntity.RequiredAcknowledgmentCode));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestEntity.RiskLevel));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestEntity.RiskCategory));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestEntity.CorrelationId));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestEntity.TraceId));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestEntity.PolicyVersion));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestEntity.PolicyHash));

        AssertHasIndex(
            entityType,
            nameof(AcknowledgmentRequestEntity.ActorId),
            nameof(AcknowledgmentRequestEntity.OperationName));

        AssertHasIndex(
            entityType,
            nameof(AcknowledgmentRequestEntity.CorrelationId),
            nameof(AcknowledgmentRequestEntity.OperationName));

        AssertHasIndex(
            entityType,
            nameof(AcknowledgmentRequestEntity.PolicyVersion),
            nameof(AcknowledgmentRequestEntity.PolicyHash));
    }

    /// <summary>
    /// Verifies that the entity configuration for the handshake request metadata entity defines the expected keys, properties, relationships, and indexes according to the design of the handshake request metadata. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, that a required relationship is defined to the handshake request with cascade delete behavior, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that a unique index is defined on the combination of the foreign key to the handshake request and the metadata key to enforce uniqueness of metadata keys within each handshake request.
    /// </summary>
    [Fact]
    public void AcknowledgmentRequestMetadataConfigurationDefinesRelationshipIndexesAndCascadeDelete()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AcknowledgmentRequestMetadataEntity>(context);

        Assert.Equal("AsiBackboneHandshakeRequestMetadata", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AcknowledgmentRequestMetadataEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AcknowledgmentRequestMetadataEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequired(entityType, nameof(AcknowledgmentRequestMetadataEntity.AcknowledgmentRequestId));
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentRequestMetadataEntity.MetadataKey), 256);
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentRequestMetadataEntity.MetadataValue), 4096);

        AssertHasCascadeForeignKey(
            entityType,
            typeof(AcknowledgmentRequestEntity),
            nameof(AcknowledgmentRequestMetadataEntity.AcknowledgmentRequestId));

        AssertHasIndex(entityType, nameof(AcknowledgmentRequestMetadataEntity.AcknowledgmentRequestId));
        AssertHasIndex(entityType, nameof(AcknowledgmentRequestMetadataEntity.MetadataKey));

        AssertHasUniqueIndex(
            entityType,
            nameof(AcknowledgmentRequestMetadataEntity.AcknowledgmentRequestId),
            nameof(AcknowledgmentRequestMetadataEntity.MetadataKey));
    }

    /// <summary>
    /// Verifies that the entity configuration for the handshake acknowledgment entity defines the expected keys, properties, and indexes according to the design of the handshake acknowledgment. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, enum properties are stored as strings, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that composite indexes are defined on combinations of properties that are commonly queried together to further optimize query performance.
    /// </summary>
    [Fact]
    public void AcknowledgmentResponseConfigurationDefinesKeysPropertiesAndIndexes()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AcknowledgmentResponseEntity>(context);

        Assert.Equal("AsiBackboneHandshakeAcknowledgments", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AcknowledgmentResponseEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AcknowledgmentResponseEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentResponseEntity.AcknowledgmentId), 128);
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentResponseEntity.HandshakeId), 128);
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentResponseEntity.ActorId), 128);
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentResponseEntity.ActorType), 64);
        AssertStoresEnumAsString(entityType, nameof(AcknowledgmentResponseEntity.ActorType));
        AssertOptionalMaxLength(entityType, nameof(AcknowledgmentResponseEntity.ActorDisplayName), 256);
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentResponseEntity.AcknowledgmentCode), 128);
        AssertRequired(entityType, nameof(AcknowledgmentResponseEntity.Acknowledged));
        AssertRequired(entityType, nameof(AcknowledgmentResponseEntity.OccurredUtc));
        AssertOptionalMaxLength(entityType, nameof(AcknowledgmentResponseEntity.CorrelationId), 128);
        AssertOptionalMaxLength(entityType, nameof(AcknowledgmentResponseEntity.TraceId), 128);

        AssertHasUniqueIndex(entityType, nameof(AcknowledgmentResponseEntity.AcknowledgmentId));
        AssertHasIndex(entityType, nameof(AcknowledgmentResponseEntity.HandshakeId));
        AssertHasIndex(entityType, nameof(AcknowledgmentResponseEntity.ActorId));
        AssertHasIndex(entityType, nameof(AcknowledgmentResponseEntity.ActorType));
        AssertHasIndex(entityType, nameof(AcknowledgmentResponseEntity.AcknowledgmentCode));
        AssertHasIndex(entityType, nameof(AcknowledgmentResponseEntity.Acknowledged));
        AssertHasIndex(entityType, nameof(AcknowledgmentResponseEntity.OccurredUtc));
        AssertHasIndex(entityType, nameof(AcknowledgmentResponseEntity.CorrelationId));
        AssertHasIndex(entityType, nameof(AcknowledgmentResponseEntity.TraceId));

        AssertHasIndex(
            entityType,
            nameof(AcknowledgmentResponseEntity.HandshakeId),
            nameof(AcknowledgmentResponseEntity.OccurredUtc));

        AssertHasIndex(
            entityType,
            nameof(AcknowledgmentResponseEntity.ActorId),
            nameof(AcknowledgmentResponseEntity.OccurredUtc));

        AssertHasIndex(
            entityType,
            nameof(AcknowledgmentResponseEntity.CorrelationId),
            nameof(AcknowledgmentResponseEntity.OccurredUtc));
    }

    /// <summary>
    /// Verifies that the entity configuration for the handshake acknowledgment metadata entity defines the expected keys, properties, relationships, and indexes according to the design of the handshake acknowledgment metadata. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, that a required relationship is defined to the handshake acknowledgment with cascade delete behavior, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that a unique index is defined on the combination of the foreign key to the handshake acknowledgment and the metadata key to enforce uniqueness of metadata keys within each handshake acknowledgment.
    /// </summary>
    [Fact]
    public void AcknowledgmentResponseMetadataConfigurationDefinesRelationshipIndexesAndCascadeDelete()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AcknowledgmentResponseMetadataEntity>(context);

        Assert.Equal("AsiBackboneHandshakeAcknowledgmentMetadata", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AcknowledgmentResponseMetadataEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AcknowledgmentResponseMetadataEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequired(entityType, nameof(AcknowledgmentResponseMetadataEntity.AcknowledgmentResponseId));
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentResponseMetadataEntity.MetadataKey), 256);
        AssertRequiredMaxLength(entityType, nameof(AcknowledgmentResponseMetadataEntity.MetadataValue), 4096);

        AssertHasCascadeForeignKey(
            entityType,
            typeof(AcknowledgmentResponseEntity),
            nameof(AcknowledgmentResponseMetadataEntity.AcknowledgmentResponseId));

        AssertHasIndex(entityType, nameof(AcknowledgmentResponseMetadataEntity.AcknowledgmentResponseId));
        AssertHasIndex(entityType, nameof(AcknowledgmentResponseMetadataEntity.MetadataKey));

        AssertHasUniqueIndex(
            entityType,
            nameof(AcknowledgmentResponseMetadataEntity.AcknowledgmentResponseId),
            nameof(AcknowledgmentResponseMetadataEntity.MetadataKey));
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

        _ = context.AcknowledgmentRequests.Add(new AcknowledgmentRequestEntity
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
            RiskLevel = AcknowledgmentRiskLevel.High,
            RiskCategory = "administrative",
            CorrelationId = "correlation-123",
            TraceId = "trace-123",
            PolicyVersion = "v1",
            PolicyHash = "hash-123"
        });

        _ = context.AcknowledgmentRequestMetadata.Add(new AcknowledgmentRequestMetadataEntity
        {
            Id = Guid.NewGuid(),
            AcknowledgmentRequestId = handshakeRequestId,
            MetadataKey = "source",
            MetadataValue = "unit-test"
        });

        _ = context.AcknowledgmentResponses.Add(new AcknowledgmentResponseEntity
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

        _ = context.AcknowledgmentResponseMetadata.Add(new AcknowledgmentResponseMetadataEntity
        {
            Id = Guid.NewGuid(),
            AcknowledgmentResponseId = handshakeAcknowledgmentId,
            MetadataKey = "source",
            MetadataValue = "unit-test"
        });

        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        Assert.Equal(1, await context.AuditLedgerRecords.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.AuditLedgerReasonCodes.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.AuditLedgerMetadata.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.AcknowledgmentRequests.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.AcknowledgmentRequestMetadata.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.AcknowledgmentResponses.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.AcknowledgmentResponseMetadata.CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        AuditLedgerRecordEntity auditRecord = await context.AuditLedgerRecords.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(GovernanceActorType.Service, auditRecord.ActorType);

        AcknowledgmentRequestEntity handshakeRequest = await context.AcknowledgmentRequests.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(AcknowledgmentRiskLevel.High, handshakeRequest.RiskLevel);

        AcknowledgmentResponseEntity acknowledgment = await context.AcknowledgmentResponses.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
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

        public DbSet<AcknowledgmentRequestEntity> AcknowledgmentRequests =>
            Set<AcknowledgmentRequestEntity>();

        public DbSet<AcknowledgmentRequestMetadataEntity> AcknowledgmentRequestMetadata =>
            Set<AcknowledgmentRequestMetadataEntity>();

        public DbSet<AcknowledgmentResponseEntity> AcknowledgmentResponses =>
            Set<AcknowledgmentResponseEntity>();

        public DbSet<AcknowledgmentResponseMetadataEntity> AcknowledgmentResponseMetadata =>
            Set<AcknowledgmentResponseMetadataEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
