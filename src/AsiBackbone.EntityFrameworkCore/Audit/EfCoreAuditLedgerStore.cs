using System.Collections.ObjectModel;
using System.Text.Json;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Results;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AsiBackbone.EntityFrameworkCore.Audit;

/// <summary>
/// Entity Framework Core-backed audit ledger store that persists records through a host-owned <see cref="DbContext" />.
/// </summary>
/// <remarks>
/// This store is append-oriented and intentionally relies on the host application to expose the ASI Backbone entities from
/// its own <see cref="DbContext" /> and migrations. It does not create a package-owned context or select a database provider.
/// </remarks>
public sealed class EfCoreAuditLedgerStore : IGovernanceAuditLedgerStore
{
    private const string AppendFailedReasonCode = "asi_backbone.audit_ledger.append_failed";
    private const string AppendFailedReasonMessage =
        "The audit ledger record could not be persisted by the configured EF Core store.";

    private static readonly Action<ILogger, string, Exception?> LogAuditLedgerAppendFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1001, nameof(LogAuditLedgerAppendFailed)),
            "EF Core audit ledger append failed for record {AuditLedgerRecordId}.");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DbContext dbContext;
    private readonly ILogger<EfCoreAuditLedgerStore>? logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreAuditLedgerStore" /> class.
    /// </summary>
    /// <param name="dbContext">The host-owned database context.</param>
    /// <param name="logger">The optional host-owned logger used for internal persistence diagnostics.</param>
    public EfCoreAuditLedgerStore(
        DbContext dbContext,
        ILogger<EfCoreAuditLedgerStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        this.dbContext = dbContext;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<OperationResult<AuditLedgerRecord>> AppendAsync(
        AuditLedgerRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        AuditLedgerRecordEntity entity = ToEntity(record);

        _ = await dbContext
            .Set<AuditLedgerRecordEntity>()
            .AddAsync(entity, cancellationToken)
            .ConfigureAwait(false);

        foreach (AuditLedgerReasonCodeEntity reasonCode in ToReasonCodeEntities(entity.Id, record.ReasonCodes))
        {
            _ = await dbContext
                .Set<AuditLedgerReasonCodeEntity>()
                .AddAsync(reasonCode, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (AuditLedgerMetadataEntity metadata in ToMetadataEntities(entity.Id, record.Metadata))
        {
            _ = await dbContext
                .Set<AuditLedgerMetadataEntity>()
                .AddAsync(metadata, cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            dbContext.ChangeTracker.Clear();

            if (logger is not null)
            {
                LogAuditLedgerAppendFailed(logger, record.RecordId, ex);
            }

            return OperationResult.Failure<AuditLedgerRecord>(
                AppendFailedReasonCode,
                AppendFailedReasonMessage);
        }

        return OperationResult.Success(record);
    }

    /// <inheritdoc />
    public async ValueTask<AuditLedgerRecord?> FindByRecordIdAsync(
        string recordId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        string normalizedRecordId = recordId.Trim();

        AuditLedgerRecordEntity? entity = await LedgerRecords()
            .Where(record => record.RecordId == normalizedRecordId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToRecord(entity);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string normalizedCorrelationId = correlationId.Trim();

        List<AuditLedgerRecordEntity> entities = await LedgerRecords()
            .Where(record => record.CorrelationId == normalizedCorrelationId)
            .OrderBy(record => record.RecordedUtc)
            .ThenBy(record => record.RecordId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToRecords(entities);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByTraceIdAsync(
        string traceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);

        string normalizedTraceId = traceId.Trim();

        List<AuditLedgerRecordEntity> entities = await LedgerRecords()
            .Where(record => record.TraceId == normalizedTraceId)
            .OrderBy(record => record.RecordedUtc)
            .ThenBy(record => record.RecordId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToRecords(entities);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByActorIdAsync(
        string actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        string normalizedActorId = actorId.Trim();

        List<AuditLedgerRecordEntity> entities = await LedgerRecords()
            .Where(record => record.ActorId == normalizedActorId)
            .OrderBy(record => record.RecordedUtc)
            .ThenBy(record => record.RecordId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToRecords(entities);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AuditLedgerRecord>> FindByRecordedUtcRangeAsync(
        DateTimeOffset recordedFromUtc,
        DateTimeOffset recordedToUtc,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset normalizedFromUtc = recordedFromUtc.ToUniversalTime();
        DateTimeOffset normalizedToUtc = recordedToUtc.ToUniversalTime();

        if (normalizedFromUtc > normalizedToUtc)
        {
            throw new ArgumentException(
                "The recorded UTC range start must be less than or equal to the range end.",
                nameof(recordedFromUtc));
        }

        List<AuditLedgerRecordEntity> entities = await LedgerRecords()
            .Where(record => record.RecordedUtc >= normalizedFromUtc && record.RecordedUtc <= normalizedToUtc)
            .OrderBy(record => record.RecordedUtc)
            .ThenBy(record => record.RecordId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToRecords(entities);
    }

    private IQueryable<AuditLedgerRecordEntity> LedgerRecords()
    {
        return dbContext.Set<AuditLedgerRecordEntity>().AsNoTracking();
    }

    private static AuditLedgerRecordEntity ToEntity(AuditLedgerRecord record)
    {
        return new AuditLedgerRecordEntity
        {
            RecordId = record.RecordId,
            SchemaVersion = record.SchemaVersion,
            EventId = record.EventId,
            DecisionReceiptId = record.DecisionReceiptId,
            OccurredUtc = record.OccurredUtc,
            RecordedUtc = record.RecordedUtc,
            ActorId = record.ActorId,
            ActorType = record.ActorType,
            ActorDisplayName = record.ActorDisplayName,
            OperationName = record.OperationName,
            Outcome = record.Outcome,
            ReasonCodesJson = JsonSerializer.Serialize(record.ReasonCodes, JsonOptions),
            CorrelationId = record.CorrelationId,
            TraceId = record.TraceId,
            SpanId = record.SpanId,
            ParentSpanId = record.ParentSpanId,
            DecisionLatencyMs = record.DecisionLatencyMs,
            ConstraintSetHash = record.ConstraintSetHash,
            ConstraintCount = record.ConstraintCount,
            RiskScore = record.RiskScore,
            PolicyScope = record.PolicyScope,
            TenantHash = record.TenantHash,
            OrganizationHash = record.OrganizationHash,
            EmitterStatus = record.EmitterStatus,
            EmitterProvider = record.EmitterProvider,
            OutboxSequence = record.OutboxSequence,
            GatewayExecutionId = record.GatewayExecutionId,
            DecisionStage = record.DecisionStage,
            PolicyVersion = record.PolicyVersion,
            PolicyHash = record.PolicyHash,
            HandshakeId = record.HandshakeId,
            AcknowledgmentId = record.AcknowledgmentId,
            CapabilityTokenId = record.CapabilityTokenId,
            PreviousRecordHash = record.PreviousRecordHash,
            RecordHash = record.RecordHash,
            SigningHash = record.SigningHash,
            SignatureKeyId = record.SignatureKeyId,
            SignatureKeyVersion = record.SignatureKeyVersion,
            SignatureAlgorithm = record.SignatureAlgorithm,
            SignatureValue = record.SignatureValue,
            SignatureProvider = record.SignatureProvider,
            SignedUtc = record.SignedUtc,
            MetadataJson = JsonSerializer.Serialize(record.Metadata, JsonOptions)
        };
    }

    private static AuditLedgerReasonCodeEntity[] ToReasonCodeEntities(
        Guid auditLedgerRecordId,
        IReadOnlyList<string> reasonCodes)
    {
        return [.. reasonCodes
            .Select((reasonCode, index) => new AuditLedgerReasonCodeEntity
            {
                AuditLedgerRecordId = auditLedgerRecordId,
                Sequence = index,
                ReasonCode = reasonCode
            })];
    }

    private static AuditLedgerMetadataEntity[] ToMetadataEntities(
        Guid auditLedgerRecordId,
        IReadOnlyDictionary<string, string> metadata)
    {
        return [.. metadata
            .Select(item => new AuditLedgerMetadataEntity
            {
                AuditLedgerRecordId = auditLedgerRecordId,
                MetadataKey = item.Key,
                MetadataValue = item.Value
            })];
    }

    private static AuditLedgerRecord[] ToRecords(IEnumerable<AuditLedgerRecordEntity> entities)
    {
        return [.. entities.Select(ToRecord)];
    }

    private static AuditLedgerRecord ToRecord(AuditLedgerRecordEntity entity)
    {
        string[] reasonCodes = DeserializeReasonCodes(entity.ReasonCodesJson);
        ReadOnlyDictionary<string, string> metadata = DeserializeMetadata(entity.MetadataJson);

        var residue = new EntityDecisionReceipt(
            entity.EventId,
            entity.DecisionReceiptId,
            entity.SchemaVersion,
            entity.OccurredUtc,
            entity.ActorId,
            entity.ActorType,
            entity.ActorDisplayName,
            entity.OperationName,
            entity.Outcome,
            Array.AsReadOnly(reasonCodes),
            entity.CorrelationId,
            entity.TraceId,
            entity.SpanId,
            entity.ParentSpanId,
            entity.DecisionLatencyMs,
            entity.ConstraintSetHash,
            entity.ConstraintCount,
            entity.RiskScore,
            entity.PolicyScope,
            entity.TenantHash,
            entity.OrganizationHash,
            entity.EmitterStatus,
            entity.EmitterProvider,
            entity.OutboxSequence,
            entity.GatewayExecutionId,
            entity.DecisionStage,
            entity.PolicyVersion,
            entity.PolicyHash,
            metadata);

        return AuditLedgerRecord.FromDecisionReceipt(
            residue,
            entity.RecordId,
            entity.RecordedUtc,
            entity.HandshakeId,
            entity.AcknowledgmentId,
            entity.CapabilityTokenId,
            entity.PreviousRecordHash,
            entity.RecordHash,
            entity.SignatureKeyId,
            entity.SignatureAlgorithm,
            entity.SignatureValue,
            signingHash: entity.SigningHash,
            signatureKeyVersion: entity.SignatureKeyVersion,
            signatureProvider: entity.SignatureProvider,
            signedUtc: entity.SignedUtc,
            schemaVersion: entity.SchemaVersion);
    }

    private static string[] DeserializeReasonCodes(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
    }

    private static ReadOnlyDictionary<string, string> DeserializeMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));
        }

        Dictionary<string, string>? metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);

        return metadata is null || metadata.Count == 0
            ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal))
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata, StringComparer.Ordinal));
    }

    private sealed class EntityDecisionReceipt(
        string eventId,
        string? decisionReceiptId,
        string schemaVersion,
        DateTimeOffset occurredUtc,
        string actorId,
        GovernanceActorType actorType,
        string? actorDisplayName,
        string operationName,
        string outcome,
        IReadOnlyList<string> reasonCodes,
        string? correlationId,
        string? traceId,
        string? spanId,
        string? parentSpanId,
        long? decisionLatencyMs,
        string? constraintSetHash,
        int? constraintCount,
        double? riskScore,
        string? policyScope,
        string? tenantHash,
        string? organizationHash,
        string? emitterStatus,
        string? emitterProvider,
        long? outboxSequence,
        string? gatewayExecutionId,
        string? decisionStage,
        string? policyVersion,
        string? policyHash,
        IReadOnlyDictionary<string, string> metadata) : IDecisionReceipt
    {
        public string EventId { get; } = eventId;

        public string? DecisionReceiptId { get; } = decisionReceiptId;

        public string SchemaVersion { get; } = schemaVersion;

        public DateTimeOffset OccurredUtc { get; } = occurredUtc;

        public string ActorId { get; } = actorId;

        public GovernanceActorType ActorType { get; } = actorType;

        public string? ActorDisplayName { get; } = actorDisplayName;

        public string OperationName { get; } = operationName;

        public string Outcome { get; } = outcome;

        public IReadOnlyList<string> ReasonCodes { get; } = reasonCodes;

        public string? CorrelationId { get; } = correlationId;

        public string? TraceId { get; } = traceId;

        public string? SpanId { get; } = spanId;

        public string? ParentSpanId { get; } = parentSpanId;

        public long? DecisionLatencyMs { get; } = decisionLatencyMs;

        public string? ConstraintSetHash { get; } = constraintSetHash;

        public int? ConstraintCount { get; } = constraintCount;

        public double? RiskScore { get; } = riskScore;

        public string? PolicyScope { get; } = policyScope;

        public string? TenantHash { get; } = tenantHash;

        public string? OrganizationHash { get; } = organizationHash;

        public string? EmitterStatus { get; } = emitterStatus;

        public string? EmitterProvider { get; } = emitterProvider;

        public long? OutboxSequence { get; } = outboxSequence;

        public string? GatewayExecutionId { get; } = gatewayExecutionId;

        public string? DecisionStage { get; } = decisionStage;

        public string? PolicyVersion { get; } = policyVersion;

        public string? PolicyHash { get; } = policyHash;

        public IReadOnlyDictionary<string, string> Metadata { get; } = metadata;
    }
}
