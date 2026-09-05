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

        Assert.NotNull(context.Model.FindEntityType(typeof(AsiBackboneAuditLedgerRecordEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AsiBackboneAuditLedgerReasonCodeEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AsiBackboneAuditLedgerMetadataEntity)));
    }

    /// <summary>
    /// Verifies that the entity types for the handshake requests, request metadata, acknowledgments, and acknowledgment metadata are included in the model when applying the configurations.
    /// </summary>
    [Fact]
    public void ApplyAsiBackboneConfigurationsAddsHandshakeEntityTypes()
    {
        using HostOwnedDbContext context = CreateContext();

        Assert.NotNull(context.Model.FindEntityType(typeof(AsiBackboneHandshakeRequestEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AsiBackboneHandshakeRequestMetadataEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AsiBackboneHandshakeAcknowledgmentEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AsiBackboneHandshakeAcknowledgmentMetadataEntity)));
    }

    /// <summary>
    /// Verifies that the entity configuration for the audit ledger record entity defines the expected keys, properties, and indexes according to the design of the audit ledger record. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, enum properties are stored as strings, and that indexes are defined on commonly queried properties to optimize performance.
    /// </summary>
    [Fact]
    public void AuditLedgerRecordConfigurationDefinesKeysPropertiesAndIndexes()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AsiBackboneAuditLedgerRecordEntity>(context);

        Assert.Equal("AsiBackboneAuditLedgerRecords", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequiredMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.RecordId), 128);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.EventId), 128);
        AssertRequired(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.OccurredUtc));
        AssertRequired(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.RecordedUtc));
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.ActorId), 128);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.ActorType), 64);
        AssertStoresEnumAsString(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.ActorType));
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.ActorDisplayName), 256);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.OperationName), 256);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.Outcome), 128);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.ReasonCodesJson), 65536);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.CorrelationId), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.TraceId), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.PolicyVersion), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.PolicyHash), 512);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.HandshakeId), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.AcknowledgmentId), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.CapabilityTokenId), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.PreviousRecordHash), 512);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.RecordHash), 512);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.SignatureKeyId), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.SignatureAlgorithm), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.SignatureValue), 16384);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.MetadataJson), 65536);

        Assert.Equal(6, entityType.GetIndexes().Count());
        AssertHasUniqueIndex(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.RecordId));
        AssertHasIndex(
            entityType,
            nameof(AsiBackboneAuditLedgerRecordEntity.RecordedUtc),
            nameof(AsiBackboneAuditLedgerRecordEntity.RecordId));

        AssertHasIndex(
            entityType,
            nameof(AsiBackboneAuditLedgerRecordEntity.ActorId),
            nameof(AsiBackboneAuditLedgerRecordEntity.RecordedUtc),
            nameof(AsiBackboneAuditLedgerRecordEntity.RecordId));

        AssertHasIndex(
            entityType,
            nameof(AsiBackboneAuditLedgerRecordEntity.CorrelationId),
            nameof(AsiBackboneAuditLedgerRecordEntity.RecordedUtc),
            nameof(AsiBackboneAuditLedgerRecordEntity.RecordId));

        AssertHasIndex(
            entityType,
            nameof(AsiBackboneAuditLedgerRecordEntity.TraceId),
            nameof(AsiBackboneAuditLedgerRecordEntity.RecordedUtc),
            nameof(AsiBackboneAuditLedgerRecordEntity.RecordId));

        AssertHasIndex(entityType, nameof(AsiBackboneAuditLedgerRecordEntity.PreviousRecordHash));
    }

    /// <summary>
    /// Verifies that the entity configuration for the audit ledger reason code entity defines the expected keys, properties, relationships, and indexes according to the design of the audit ledger reason code. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, that a required relationship is defined to the audit ledger record with cascade delete behavior, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that a unique index is defined on the combination of the foreign key to the audit ledger record and the sequence number to enforce uniqueness of reason codes within each audit ledger record.
    /// </summary>
    [Fact]
    public void AuditLedgerReasonCodeConfigurationDefinesRelationshipIndexesAndCascadeDelete()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AsiBackboneAuditLedgerReasonCodeEntity>(context);

        Assert.Equal("AsiBackboneAuditLedgerReasonCodes", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AsiBackboneAuditLedgerReasonCodeEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AsiBackboneAuditLedgerReasonCodeEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequired(entityType, nameof(AsiBackboneAuditLedgerReasonCodeEntity.AuditLedgerRecordId));
        AssertRequired(entityType, nameof(AsiBackboneAuditLedgerReasonCodeEntity.Sequence));
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneAuditLedgerReasonCodeEntity.ReasonCode), 256);

        AssertHasCascadeForeignKey(
            entityType,
            typeof(AsiBackboneAuditLedgerRecordEntity),
            nameof(AsiBackboneAuditLedgerReasonCodeEntity.AuditLedgerRecordId));

        AssertHasIndex(entityType, nameof(AsiBackboneAuditLedgerReasonCodeEntity.AuditLedgerRecordId));
        AssertHasIndex(entityType, nameof(AsiBackboneAuditLedgerReasonCodeEntity.ReasonCode));

        AssertHasUniqueIndex(
            entityType,
            nameof(AsiBackboneAuditLedgerReasonCodeEntity.AuditLedgerRecordId),
            nameof(AsiBackboneAuditLedgerReasonCodeEntity.Sequence));

        AssertHasIndex(
            entityType,
            nameof(AsiBackboneAuditLedgerReasonCodeEntity.AuditLedgerRecordId),
            nameof(AsiBackboneAuditLedgerReasonCodeEntity.ReasonCode));
    }

    /// <summary>
    /// Verifies that the entity configuration for the audit ledger metadata entity defines the expected keys, properties, relationships, and indexes according to the design of the audit ledger metadata. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, that a required relationship is defined to the audit ledger record with cascade delete behavior, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that a unique index is defined on the combination of the foreign key to the audit ledger record and the metadata key to enforce uniqueness of metadata keys within each audit ledger record.
    /// </summary>
    [Fact]
    public void AuditLedgerMetadataConfigurationDefinesRelationshipIndexesAndCascadeDelete()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AsiBackboneAuditLedgerMetadataEntity>(context);

        Assert.Equal("AsiBackboneAuditLedgerMetadata", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AsiBackboneAuditLedgerMetadataEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AsiBackboneAuditLedgerMetadataEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequired(entityType, nameof(AsiBackboneAuditLedgerMetadataEntity.AuditLedgerRecordId));
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneAuditLedgerMetadataEntity.MetadataKey), 256);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneAuditLedgerMetadataEntity.MetadataValue), 4096);

        AssertHasCascadeForeignKey(
            entityType,
            typeof(AsiBackboneAuditLedgerRecordEntity),
            nameof(AsiBackboneAuditLedgerMetadataEntity.AuditLedgerRecordId));

        AssertHasIndex(entityType, nameof(AsiBackboneAuditLedgerMetadataEntity.AuditLedgerRecordId));
        AssertHasIndex(entityType, nameof(AsiBackboneAuditLedgerMetadataEntity.MetadataKey));

        AssertHasUniqueIndex(
            entityType,
            nameof(AsiBackboneAuditLedgerMetadataEntity.AuditLedgerRecordId),
            nameof(AsiBackboneAuditLedgerMetadataEntity.MetadataKey));
    }

    /// <summary>
    /// Verifies that the entity configuration for the handshake request entity defines the expected keys, properties, and indexes according to the design of the handshake request. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, enum properties are stored as strings, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that composite indexes are defined on combinations of properties that are commonly queried together to further optimize query performance.
    /// </summary>
    [Fact]
    public void HandshakeRequestConfigurationDefinesKeysPropertiesAndIndexes()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AsiBackboneHandshakeRequestEntity>(context);

        Assert.Equal("AsiBackboneHandshakeRequests", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AsiBackboneHandshakeRequestEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AsiBackboneHandshakeRequestEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.HandshakeId), 128);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.ActorId), 128);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.ActorType), 64);
        AssertStoresEnumAsString(entityType, nameof(AsiBackboneHandshakeRequestEntity.ActorType));
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.ActorDisplayName), 256);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.OperationName), 256);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.ReasonCode), 256);
        AssertRequired(entityType, nameof(AsiBackboneHandshakeRequestEntity.Message));
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.RequiredAcknowledgmentCode), 128);
        AssertRequired(entityType, nameof(AsiBackboneHandshakeRequestEntity.RequiredAcknowledgmentText));
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.RiskLevel), 64);
        AssertStoresEnumAsString(entityType, nameof(AsiBackboneHandshakeRequestEntity.RiskLevel));
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.RiskCategory), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.CorrelationId), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.TraceId), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.PolicyVersion), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneHandshakeRequestEntity.PolicyHash), 512);

        AssertHasUniqueIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.HandshakeId));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.ActorId));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.ActorType));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.OperationName));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.ReasonCode));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.RequiredAcknowledgmentCode));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.RiskLevel));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.RiskCategory));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.CorrelationId));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.TraceId));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.PolicyVersion));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestEntity.PolicyHash));

        AssertHasIndex(
            entityType,
            nameof(AsiBackboneHandshakeRequestEntity.ActorId),
            nameof(AsiBackboneHandshakeRequestEntity.OperationName));

        AssertHasIndex(
            entityType,
            nameof(AsiBackboneHandshakeRequestEntity.CorrelationId),
            nameof(AsiBackboneHandshakeRequestEntity.OperationName));

        AssertHasIndex(
            entityType,
            nameof(AsiBackboneHandshakeRequestEntity.PolicyVersion),
            nameof(AsiBackboneHandshakeRequestEntity.PolicyHash));
    }

    /// <summary>
    /// Verifies that the entity configuration for the handshake request metadata entity defines the expected keys, properties, relationships, and indexes according to the design of the handshake request metadata. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, that a required relationship is defined to the handshake request with cascade delete behavior, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that a unique index is defined on the combination of the foreign key to the handshake request and the metadata key to enforce uniqueness of metadata keys within each handshake request.
    /// </summary>
    [Fact]
    public void HandshakeRequestMetadataConfigurationDefinesRelationshipIndexesAndCascadeDelete()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AsiBackboneHandshakeRequestMetadataEntity>(context);

        Assert.Equal("AsiBackboneHandshakeRequestMetadata", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AsiBackboneHandshakeRequestMetadataEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AsiBackboneHandshakeRequestMetadataEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequired(entityType, nameof(AsiBackboneHandshakeRequestMetadataEntity.HandshakeRequestId));
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeRequestMetadataEntity.MetadataKey), 256);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeRequestMetadataEntity.MetadataValue), 4096);

        AssertHasCascadeForeignKey(
            entityType,
            typeof(AsiBackboneHandshakeRequestEntity),
            nameof(AsiBackboneHandshakeRequestMetadataEntity.HandshakeRequestId));

        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestMetadataEntity.HandshakeRequestId));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeRequestMetadataEntity.MetadataKey));

        AssertHasUniqueIndex(
            entityType,
            nameof(AsiBackboneHandshakeRequestMetadataEntity.HandshakeRequestId),
            nameof(AsiBackboneHandshakeRequestMetadataEntity.MetadataKey));
    }

    /// <summary>
    /// Verifies that the entity configuration for the handshake acknowledgment entity defines the expected keys, properties, and indexes according to the design of the handshake acknowledgment. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, enum properties are stored as strings, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that composite indexes are defined on combinations of properties that are commonly queried together to further optimize query performance.
    /// </summary>
    [Fact]
    public void HandshakeAcknowledgmentConfigurationDefinesKeysPropertiesAndIndexes()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AsiBackboneHandshakeAcknowledgmentEntity>(context);

        Assert.Equal("AsiBackboneHandshakeAcknowledgments", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.AcknowledgmentId), 128);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.HandshakeId), 128);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.ActorId), 128);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.ActorType), 64);
        AssertStoresEnumAsString(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.ActorType));
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.ActorDisplayName), 256);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.AcknowledgmentCode), 128);
        AssertRequired(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.Acknowledged));
        AssertRequired(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.OccurredUtc));
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.CorrelationId), 128);
        AssertOptionalMaxLength(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.TraceId), 128);

        AssertHasUniqueIndex(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.AcknowledgmentId));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.HandshakeId));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.ActorId));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.ActorType));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.AcknowledgmentCode));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.Acknowledged));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.OccurredUtc));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.CorrelationId));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeAcknowledgmentEntity.TraceId));

        AssertHasIndex(
            entityType,
            nameof(AsiBackboneHandshakeAcknowledgmentEntity.HandshakeId),
            nameof(AsiBackboneHandshakeAcknowledgmentEntity.OccurredUtc));

        AssertHasIndex(
            entityType,
            nameof(AsiBackboneHandshakeAcknowledgmentEntity.ActorId),
            nameof(AsiBackboneHandshakeAcknowledgmentEntity.OccurredUtc));

        AssertHasIndex(
            entityType,
            nameof(AsiBackboneHandshakeAcknowledgmentEntity.CorrelationId),
            nameof(AsiBackboneHandshakeAcknowledgmentEntity.OccurredUtc));
    }

    /// <summary>
    /// Verifies that the entity configuration for the handshake acknowledgment metadata entity defines the expected keys, properties, relationships, and indexes according to the design of the handshake acknowledgment metadata. This includes verifying that required properties are configured as required, string properties have the expected maximum lengths, that a required relationship is defined to the handshake acknowledgment with cascade delete behavior, and that indexes are defined on commonly queried properties to optimize performance. Additionally, verifies that a unique index is defined on the combination of the foreign key to the handshake acknowledgment and the metadata key to enforce uniqueness of metadata keys within each handshake acknowledgment.
    /// </summary>
    [Fact]
    public void HandshakeAcknowledgmentMetadataConfigurationDefinesRelationshipIndexesAndCascadeDelete()
    {
        using HostOwnedDbContext context = CreateContext();

        IEntityType entityType = GetEntityType<AsiBackboneHandshakeAcknowledgmentMetadataEntity>(context);

        Assert.Equal("AsiBackboneHandshakeAcknowledgmentMetadata", entityType.GetTableName());
        AssertPrimaryKey(entityType, nameof(AsiBackboneHandshakeAcknowledgmentMetadataEntity.Id));
        AssertValueGeneratedNever(entityType, nameof(AsiBackboneHandshakeAcknowledgmentMetadataEntity.Id));
        AssertConcurrencyStamp(entityType);

        AssertRequired(entityType, nameof(AsiBackboneHandshakeAcknowledgmentMetadataEntity.HandshakeAcknowledgmentId));
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeAcknowledgmentMetadataEntity.MetadataKey), 256);
        AssertRequiredMaxLength(entityType, nameof(AsiBackboneHandshakeAcknowledgmentMetadataEntity.MetadataValue), 4096);

        AssertHasCascadeForeignKey(
            entityType,
            typeof(AsiBackboneHandshakeAcknowledgmentEntity),
            nameof(AsiBackboneHandshakeAcknowledgmentMetadataEntity.HandshakeAcknowledgmentId));

        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeAcknowledgmentMetadataEntity.HandshakeAcknowledgmentId));
        AssertHasIndex(entityType, nameof(AsiBackboneHandshakeAcknowledgmentMetadataEntity.MetadataKey));

        AssertHasUniqueIndex(
            entityType,
            nameof(AsiBackboneHandshakeAcknowledgmentMetadataEntity.HandshakeAcknowledgmentId),
            nameof(AsiBackboneHandshakeAcknowledgmentMetadataEntity.MetadataKey));
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

        _ = context.AuditLedgerRecords.Add(new AsiBackboneAuditLedgerRecordEntity
        {
            Id = auditLedgerRecordId,
            RecordId = "record-123",
            EventId = "event-123",
            OccurredUtc = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero),
            RecordedUtc = new DateTimeOffset(2026, 6, 5, 12, 1, 0, TimeSpan.Zero),
            ActorId = "service-123",
            ActorType = AsiBackboneActorType.Service,
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

        _ = context.AuditLedgerReasonCodes.Add(new AsiBackboneAuditLedgerReasonCodeEntity
        {
            Id = Guid.NewGuid(),
            AuditLedgerRecordId = auditLedgerRecordId,
            Sequence = 0,
            ReasonCode = "policy.allowed"
        });

        _ = context.AuditLedgerMetadata.Add(new AsiBackboneAuditLedgerMetadataEntity
        {
            Id = Guid.NewGuid(),
            AuditLedgerRecordId = auditLedgerRecordId,
            MetadataKey = "source",
            MetadataValue = "unit-test"
        });

        _ = context.HandshakeRequests.Add(new AsiBackboneHandshakeRequestEntity
        {
            Id = handshakeRequestId,
            HandshakeId = "handshake-123",
            ActorId = "service-123",
            ActorType = AsiBackboneActorType.Service,
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

        _ = context.HandshakeRequestMetadata.Add(new AsiBackboneHandshakeRequestMetadataEntity
        {
            Id = Guid.NewGuid(),
            HandshakeRequestId = handshakeRequestId,
            MetadataKey = "source",
            MetadataValue = "unit-test"
        });

        _ = context.HandshakeAcknowledgments.Add(new AsiBackboneHandshakeAcknowledgmentEntity
        {
            Id = handshakeAcknowledgmentId,
            AcknowledgmentId = "acknowledgment-123",
            HandshakeId = "handshake-123",
            ActorId = "service-123",
            ActorType = AsiBackboneActorType.Service,
            ActorDisplayName = "Service",
            AcknowledgmentCode = "ACK-001",
            Acknowledged = true,
            OccurredUtc = new DateTimeOffset(2026, 6, 5, 12, 2, 0, TimeSpan.Zero),
            CorrelationId = "correlation-123",
            TraceId = "trace-123"
        });

        _ = context.HandshakeAcknowledgmentMetadata.Add(new AsiBackboneHandshakeAcknowledgmentMetadataEntity
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

        AsiBackboneAuditLedgerRecordEntity auditRecord = await context.AuditLedgerRecords.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(AsiBackboneActorType.Service, auditRecord.ActorType);

        AsiBackboneHandshakeRequestEntity handshakeRequest = await context.HandshakeRequests.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(LiabilityHandshakeRiskLevel.High, handshakeRequest.RiskLevel);

        AsiBackboneHandshakeAcknowledgmentEntity acknowledgment = await context.HandshakeAcknowledgments.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
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
        public DbSet<AsiBackboneAuditLedgerRecordEntity> AuditLedgerRecords =>
            Set<AsiBackboneAuditLedgerRecordEntity>();

        public DbSet<AsiBackboneAuditLedgerReasonCodeEntity> AuditLedgerReasonCodes =>
            Set<AsiBackboneAuditLedgerReasonCodeEntity>();

        public DbSet<AsiBackboneAuditLedgerMetadataEntity> AuditLedgerMetadata =>
            Set<AsiBackboneAuditLedgerMetadataEntity>();

        public DbSet<AsiBackboneHandshakeRequestEntity> HandshakeRequests =>
            Set<AsiBackboneHandshakeRequestEntity>();

        public DbSet<AsiBackboneHandshakeRequestMetadataEntity> HandshakeRequestMetadata =>
            Set<AsiBackboneHandshakeRequestMetadataEntity>();

        public DbSet<AsiBackboneHandshakeAcknowledgmentEntity> HandshakeAcknowledgments =>
            Set<AsiBackboneHandshakeAcknowledgmentEntity>();

        public DbSet<AsiBackboneHandshakeAcknowledgmentMetadataEntity> HandshakeAcknowledgmentMetadata =>
            Set<AsiBackboneHandshakeAcknowledgmentMetadataEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
