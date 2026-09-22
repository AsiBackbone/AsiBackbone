using System.Collections.ObjectModel;
using System.Text.Json;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Entities;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsiBackbone.EntityFrameworkCore.Outbox;

/// <summary>
/// Entity Framework Core-backed outbox store that persists provider-neutral emission envelopes through a host-owned <see cref="DbContext" />.
/// </summary>
/// <remarks>
/// This store provides durable local storage only. Provider delivery, telemetry export, SIEM routing, and cloud emission remain downstream and optional.
/// </remarks>
public sealed class EfCoreGovernanceOutboxStore : IGovernanceOutboxClaimStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreGovernanceOutboxStore" /> class.
    /// </summary>
    /// <param name="dbContext">The host-owned database context.</param>
    public EfCoreGovernanceOutboxStore(DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> EnqueueAsync(
        GovernanceEmissionEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        var entry = GovernanceOutboxEntry.Create(envelope);

        _ = dbContext
            .Set<GovernanceOutboxEntryEntity>()
            .Add(ToEntity(entry));

        _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return entry;
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> SaveAsync(
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        GovernanceOutboxEntryEntity persistedEntity = ToEntity(entry);
        GovernanceOutboxEntryEntity? existingEntity = await dbContext
            .Set<GovernanceOutboxEntryEntity>()
            .SingleOrDefaultAsync(entity => entity.OutboxEntryId == entry.OutboxEntryId, cancellationToken)
            .ConfigureAwait(false);

        if (existingEntity is null)
        {
            _ = dbContext
                .Set<GovernanceOutboxEntryEntity>()
                .Add(persistedEntity);
        }
        else
        {
            GovernanceOutboxEntry currentEntry = ToEntry(existingEntity);
            if (IsTerminal(currentEntry))
            {
                return currentEntry;
            }

            persistedEntity.Id = existingEntity.Id;
            persistedEntity.ConcurrencyStamp = GovernanceEntity.NewConcurrencyStamp();
            dbContext.Entry(existingEntity).CurrentValues.SetValues(persistedEntity);
        }

        _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return entry;
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry?> FindByOutboxEntryIdAsync(
        string outboxEntryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);

        string normalizedOutboxEntryId = outboxEntryId.Trim();

        GovernanceOutboxEntryEntity? entity = await OutboxEntries()
            .Where(outboxEntry => outboxEntry.OutboxEntryId == normalizedOutboxEntryId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToEntry(entity);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindPendingAsync(
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        int normalizedMaxCount = NormalizeMaxCount(maxCount);

        List<GovernanceOutboxEntryEntity> entities = await OutboxEntries()
            .Where(outboxEntry => outboxEntry.Status == GovernanceEmissionStatus.Pending)
            .OrderBy(outboxEntry => outboxEntry.CreatedUtc)
            .ThenBy(outboxEntry => outboxEntry.OutboxEntryId)
            .Take(normalizedMaxCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToEntries(entities);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindRetryReadyAsync(
        DateTimeOffset utcNow,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        int normalizedMaxCount = NormalizeMaxCount(maxCount);
        DateTimeOffset normalizedUtcNow = utcNow.ToUniversalTime();

        List<GovernanceOutboxEntryEntity> entities = await OutboxEntries()
            .Where(outboxEntry =>
                outboxEntry.Status == GovernanceEmissionStatus.Deferred ||
                outboxEntry.Status == GovernanceEmissionStatus.Failed ||
                outboxEntry.Status == GovernanceEmissionStatus.RetryableFailure)
            .Where(outboxEntry => outboxEntry.NextRetryUtc == null || outboxEntry.NextRetryUtc <= normalizedUtcNow)
            .OrderBy(outboxEntry => outboxEntry.NextRetryUtc ?? outboxEntry.UpdatedUtc)
            .ThenBy(outboxEntry => outboxEntry.OutboxEntryId)
            .Take(normalizedMaxCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToEntries(entities);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimPendingAsync(
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IQueryable<GovernanceOutboxEntryEntity> candidates = OutboxEntries()
            .Where(outboxEntry => outboxEntry.Status == GovernanceEmissionStatus.Pending)
            .Where(outboxEntry => outboxEntry.ClaimToken == null || outboxEntry.ClaimExpiresUtc == null || outboxEntry.ClaimExpiresUtc <= request.UtcNow)
            .OrderBy(outboxEntry => outboxEntry.CreatedUtc)
            .ThenBy(outboxEntry => outboxEntry.OutboxEntryId)
            .Take(request.MaxCount);

        return await ClaimEntriesAsync(
            candidates,
            query => query
                .OrderBy(outboxEntry => outboxEntry.CreatedUtc)
                .ThenBy(outboxEntry => outboxEntry.OutboxEntryId),
            request,
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimRetryReadyAsync(
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IQueryable<GovernanceOutboxEntryEntity> candidates = OutboxEntries()
            .Where(outboxEntry =>
                outboxEntry.Status == GovernanceEmissionStatus.Deferred ||
                outboxEntry.Status == GovernanceEmissionStatus.Failed ||
                outboxEntry.Status == GovernanceEmissionStatus.RetryableFailure)
            .Where(outboxEntry => outboxEntry.NextRetryUtc == null || outboxEntry.NextRetryUtc <= request.UtcNow)
            .Where(outboxEntry => outboxEntry.ClaimToken == null || outboxEntry.ClaimExpiresUtc == null || outboxEntry.ClaimExpiresUtc <= request.UtcNow)
            .OrderBy(outboxEntry => outboxEntry.NextRetryUtc ?? outboxEntry.UpdatedUtc)
            .ThenBy(outboxEntry => outboxEntry.OutboxEntryId)
            .Take(request.MaxCount);

        return await ClaimEntriesAsync(
            candidates,
            query => query
                .OrderBy(outboxEntry => outboxEntry.NextRetryUtc ?? outboxEntry.UpdatedUtc)
                .ThenBy(outboxEntry => outboxEntry.OutboxEntryId),
            request,
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> MarkDeliveredAsync(
        string outboxEntryId,
        GovernanceEmissionResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
        ArgumentNullException.ThrowIfNull(result);

        GovernanceOutboxEntry entry = await RequireEntryAsync(outboxEntryId, cancellationToken).ConfigureAwait(false);
        if (IsTerminal(entry))
        {
            return entry;
        }

        GovernanceOutboxEntry updatedEntry = entry.MarkDelivered(result);

        return await SaveAsync(updatedEntry, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> MarkClaimDeliveredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(result);

        return await UpdateClaimedEntryAsync(claim, entry => entry.MarkDelivered(result), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> MarkFailedAsync(
        string outboxEntryId,
        GovernanceEmissionError governanceEmissionError,
        DateTimeOffset? nextRetryUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
        ArgumentNullException.ThrowIfNull(governanceEmissionError);

        GovernanceOutboxEntry entry = await RequireEntryAsync(outboxEntryId, cancellationToken).ConfigureAwait(false);
        if (IsTerminal(entry))
        {
            return entry;
        }

        GovernanceOutboxEntry updatedEntry = entry.MarkFailed(governanceEmissionError, nextRetryUtc);

        return await SaveAsync(updatedEntry, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> MarkClaimFailedAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        DateTimeOffset? nextRetryUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(governanceEmissionError);

        return await UpdateClaimedEntryAsync(
            claim,
            entry => entry.MarkFailed(governanceEmissionError, nextRetryUtc),
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> MarkDeadLetteredAsync(
        string outboxEntryId,
        GovernanceEmissionError governanceEmissionError,
        string? deadLetterReason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
        ArgumentNullException.ThrowIfNull(governanceEmissionError);

        GovernanceOutboxEntry entry = await RequireEntryAsync(outboxEntryId, cancellationToken).ConfigureAwait(false);
        if (IsTerminal(entry))
        {
            return entry;
        }

        GovernanceOutboxEntry updatedEntry = entry.MarkDeadLettered(governanceEmissionError, deadLetterReason);

        return await SaveAsync(updatedEntry, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> MarkClaimDeadLetteredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        string? deadLetterReason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(governanceEmissionError);

        return await UpdateClaimedEntryAsync(
            claim,
            entry => entry.MarkDeadLettered(governanceEmissionError, deadLetterReason),
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> SaveClaimAsync(
        GovernanceOutboxClaim claim,
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(entry);

        return !string.Equals(claim.OutboxEntryId, entry.OutboxEntryId, StringComparison.Ordinal)
            ? throw new ArgumentException("Claim and entry must reference the same outbox entry ID.", nameof(entry))
            : await UpdateClaimedEntryAsync(claim, _ => entry, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry?> ReleaseClaimAsync(
        GovernanceOutboxClaim claim,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        cancellationToken.ThrowIfCancellationRequested();

        GovernanceOutboxEntryEntity? entity = await dbContext
            .Set<GovernanceOutboxEntryEntity>()
            .SingleOrDefaultAsync(outboxEntry => outboxEntry.OutboxEntryId == claim.OutboxEntryId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        GovernanceOutboxEntry currentEntry = ToEntry(entity);
        if (!currentEntry.IsClaimedBy(claim) || IsTerminal(currentEntry))
        {
            return currentEntry;
        }

        GovernanceOutboxEntry releasedEntry = currentEntry.ReleaseClaim();
        await ApplyEntryUpdateAsync(entity, releasedEntry, cancellationToken).ConfigureAwait(false);

        return releasedEntry;
    }

    private async ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimEntriesAsync(
        IQueryable<GovernanceOutboxEntryEntity> candidates,
        Func<IQueryable<GovernanceOutboxEntryEntity>, IOrderedQueryable<GovernanceOutboxEntryEntity>> orderClaimedEntries,
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken)
    {
        string claimToken = Guid.NewGuid().ToString("N");
        string concurrencyStamp = GovernanceEntity.NewConcurrencyStamp();

        int claimedCount = await candidates
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(outboxEntry => outboxEntry.ConcurrencyStamp, concurrencyStamp)
                    .SetProperty(outboxEntry => outboxEntry.UpdatedUtc, request.UtcNow)
                    .SetProperty(outboxEntry => outboxEntry.ClaimOwner, request.WorkerId)
                    .SetProperty(outboxEntry => outboxEntry.ClaimToken, claimToken)
                    .SetProperty(outboxEntry => outboxEntry.ClaimedUtc, request.UtcNow)
                    .SetProperty(outboxEntry => outboxEntry.ClaimExpiresUtc, request.ClaimExpiresUtc)
                    .SetProperty(outboxEntry => outboxEntry.ClaimAttemptCount, outboxEntry => outboxEntry.ClaimAttemptCount + 1),
                cancellationToken)
            .ConfigureAwait(false);

        if (claimedCount == 0)
        {
            return [];
        }

        List<GovernanceOutboxEntryEntity> claimedEntities = await orderClaimedEntries(
            OutboxEntries().Where(outboxEntry => outboxEntry.ClaimToken == claimToken))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        ReconcileTrackedEntries(claimedEntities);

        return [.. claimedEntities.Select(ToEntry).Select(CreateClaim)];
    }

    /// <summary>
    /// Reconciles tracked instances of rows that a set-based update has just rewritten.
    /// </summary>
    /// <remarks>
    /// <c>ExecuteUpdateAsync</c> writes to the database without passing through the change tracker. An instance the
    /// host-owned context already tracks, such as one added by <see cref="EnqueueAsync" />, keeps its pre-claim values,
    /// and identity resolution returns that stale instance from later tracking queries. The claim then appears not to
    /// be held, so claim-scoped transitions return without applying.
    /// <para>
    /// An unchanged instance is detached, so the next query loads the claimed row. A modified or deleted instance holds
    /// unsaved host work that detaching would discard, so the claim-governed columns are resolved from the persisted
    /// claimed row instead: the columns the claim wrote, plus <see cref="GovernanceOutboxEntryEntity.Status" />, which
    /// the claim was taken against. Each is set as both the original and the current value, overriding any pending host
    /// change to that column. A pending host status or claim-field change would otherwise let the tracked instance look
    /// terminal or unclaimed, so the claim-scoped transition would return without recording delivery or clearing the
    /// lease. The host's other pending changes, and a pending deletion, are kept and save against the claimed row.
    /// </para>
    /// </remarks>
    /// <param name="claimedEntities">The claimed rows, read without tracking after the update.</param>
    private void ReconcileTrackedEntries(List<GovernanceOutboxEntryEntity> claimedEntities)
    {
        if (claimedEntities.Count == 0)
        {
            return;
        }

        var claimedById = claimedEntities.ToDictionary(entity => entity.OutboxEntryId, StringComparer.Ordinal);

        List<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GovernanceOutboxEntryEntity>> staleEntries = [.. dbContext
            .ChangeTracker
            .Entries<GovernanceOutboxEntryEntity>()
            .Where(entry => claimedById.ContainsKey(entry.Entity.OutboxEntryId))];

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GovernanceOutboxEntryEntity> entry in staleEntries)
        {
            if (entry.State == EntityState.Unchanged)
            {
                entry.State = EntityState.Detached;
                continue;
            }

            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                MergeClaimColumns(entry, claimedById[entry.Entity.OutboxEntryId]);
            }
        }
    }

    private static void MergeClaimColumns(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GovernanceOutboxEntryEntity> entry,
        GovernanceOutboxEntryEntity claimed)
    {
        foreach ((string propertyName, object? claimedValue) in ClaimColumnValues(claimed))
        {
            Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry property = entry.Property(propertyName);
            property.OriginalValue = claimedValue;
            property.CurrentValue = claimedValue;
        }
    }

    // The claim-governed columns: those written by the claim update in ClaimEntriesAsync, which must stay aligned with
    // that update, plus Status, which the claim eligibility query was evaluated against.
    private static (string PropertyName, object? Value)[] ClaimColumnValues(GovernanceOutboxEntryEntity claimed)
    {
        return
        [
            (nameof(GovernanceOutboxEntryEntity.ConcurrencyStamp), claimed.ConcurrencyStamp),
            (nameof(GovernanceOutboxEntryEntity.Status), claimed.Status),
            (nameof(GovernanceOutboxEntryEntity.UpdatedUtc), claimed.UpdatedUtc),
            (nameof(GovernanceOutboxEntryEntity.ClaimOwner), claimed.ClaimOwner),
            (nameof(GovernanceOutboxEntryEntity.ClaimToken), claimed.ClaimToken),
            (nameof(GovernanceOutboxEntryEntity.ClaimedUtc), claimed.ClaimedUtc),
            (nameof(GovernanceOutboxEntryEntity.ClaimExpiresUtc), claimed.ClaimExpiresUtc),
            (nameof(GovernanceOutboxEntryEntity.ClaimAttemptCount), claimed.ClaimAttemptCount),
        ];
    }

    private async ValueTask<GovernanceOutboxEntry> UpdateClaimedEntryAsync(
        GovernanceOutboxClaim claim,
        Func<GovernanceOutboxEntry, GovernanceOutboxEntry> updateEntry,
        CancellationToken cancellationToken)
    {
        GovernanceOutboxEntryEntity entity = await RequireEntityAsync(claim.OutboxEntryId, cancellationToken).ConfigureAwait(false);
        GovernanceOutboxEntry currentEntry = ToEntry(entity);

        if (!currentEntry.IsClaimedBy(claim) || IsTerminal(currentEntry))
        {
            return currentEntry;
        }

        GovernanceOutboxEntry updatedEntry = updateEntry(currentEntry);

        try
        {
            await ApplyEntryUpdateAsync(entity, updatedEntry, cancellationToken).ConfigureAwait(false);
            return updatedEntry;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            DetachEntries(exception);
            GovernanceOutboxEntry? refreshedEntry = await FindByOutboxEntryIdAsync(claim.OutboxEntryId, cancellationToken).ConfigureAwait(false);
            return refreshedEntry ?? currentEntry;
        }
    }

    private async ValueTask ApplyEntryUpdateAsync(
        GovernanceOutboxEntryEntity entity,
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken)
    {
        GovernanceOutboxEntryEntity persistedEntity = ToEntity(entry);
        persistedEntity.Id = entity.Id;
        persistedEntity.ConcurrencyStamp = GovernanceEntity.NewConcurrencyStamp();
        dbContext.Entry(entity).CurrentValues.SetValues(persistedEntity);

        _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<GovernanceOutboxEntry> RequireEntryAsync(
        string outboxEntryId,
        CancellationToken cancellationToken)
    {
        GovernanceOutboxEntry? entry = await FindByOutboxEntryIdAsync(outboxEntryId, cancellationToken).ConfigureAwait(false);

        return entry ?? throw new InvalidOperationException($"Outbox entry '{outboxEntryId.Trim()}' was not found.");
    }

    private async ValueTask<GovernanceOutboxEntryEntity> RequireEntityAsync(
        string outboxEntryId,
        CancellationToken cancellationToken)
    {
        GovernanceOutboxEntryEntity? entity = await dbContext
            .Set<GovernanceOutboxEntryEntity>()
            .SingleOrDefaultAsync(outboxEntry => outboxEntry.OutboxEntryId == outboxEntryId.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return entity ?? throw new InvalidOperationException($"Outbox entry '{outboxEntryId.Trim()}' was not found.");
    }

    private IQueryable<GovernanceOutboxEntryEntity> OutboxEntries()
    {
        return dbContext.Set<GovernanceOutboxEntryEntity>().AsNoTracking();
    }

    private static GovernanceOutboxEntryEntity ToEntity(GovernanceOutboxEntry entry)
    {
        GovernanceEmissionEnvelope envelope = entry.Envelope;
        GovernanceEmissionPayload? payload = envelope.Payload;
        GovernanceEmissionError? lastError = entry.LastError;

        return new GovernanceOutboxEntryEntity
        {
            OutboxEntryId = entry.OutboxEntryId,
            Status = entry.Status,
            CreatedUtc = entry.CreatedUtc,
            UpdatedUtc = entry.UpdatedUtc,
            DeliveredUtc = entry.Status is GovernanceEmissionStatus.Delivered ? entry.UpdatedUtc : null,
            RetryCount = entry.RetryCount,
            MaxRetryCount = entry.MaxRetryCount,
            NextRetryUtc = entry.NextRetryUtc,
            ProviderName = entry.ProviderName,
            ProviderRecordId = entry.ProviderRecordId,
            DeadLetterReason = entry.DeadLetterReason,
            LastErrorCode = lastError?.Code,
            LastErrorMessage = lastError?.Message,
            LastErrorIsRetryable = lastError?.IsRetryable,
            LastErrorProviderName = lastError?.ProviderName,
            LastErrorProviderErrorCode = lastError?.ProviderErrorCode,
            MetadataJson = JsonSerializer.Serialize(entry.Metadata, JsonOptions),
            ClaimOwner = entry.ClaimOwner,
            ClaimToken = entry.ClaimToken,
            ClaimedUtc = entry.ClaimedUtc,
            ClaimExpiresUtc = entry.ClaimExpiresUtc,
            ClaimAttemptCount = entry.ClaimAttemptCount,
            EnvelopeId = envelope.EnvelopeId,
            EnvelopeSchemaVersion = envelope.SchemaVersion,
            EnvelopeEventType = envelope.EventType,
            EnvelopeEventId = envelope.EventId,
            EnvelopeOccurredUtc = envelope.OccurredUtc,
            EnvelopeCreatedUtc = envelope.CreatedUtc,
            EnvelopeCorrelationId = envelope.CorrelationId,
            EnvelopeDecisionReceiptId = envelope.DecisionReceiptId,
            EnvelopeLifecycleStage = envelope.LifecycleStage,
            EnvelopeLifecycleStageSequence = envelope.LifecycleStageSequence,
            EnvelopePolicyVersion = envelope.PolicyVersion,
            EnvelopePolicyHash = envelope.PolicyHash,
            EnvelopeTraceId = envelope.TraceId,
            EnvelopeSpanId = envelope.SpanId,
            EnvelopeParentSpanId = envelope.ParentSpanId,
            EnvelopeOperationName = envelope.OperationName,
            EnvelopeOutcome = envelope.Outcome,
            EnvelopeActorId = envelope.ActorId,
            EnvelopeEmitterStatus = envelope.EmitterStatus,
            EnvelopeEmitterProvider = envelope.EmitterProvider,
            EnvelopeOutboxSequence = envelope.OutboxSequence,
            EnvelopeGatewayExecutionId = envelope.GatewayExecutionId,
            EnvelopeDecisionStage = envelope.DecisionStage,
            EnvelopeMetadataJson = JsonSerializer.Serialize(envelope.Metadata, JsonOptions),
            EnvelopePayloadType = payload?.PayloadType,
            EnvelopePayloadSchemaVersion = payload?.SchemaVersion,
            EnvelopePayloadContentType = payload?.ContentType,
            EnvelopePayloadContentHash = payload?.ContentHash,
            EnvelopePayloadSizeBytes = payload?.SizeBytes,
            EnvelopePayloadMetadataJson = JsonSerializer.Serialize(payload?.Metadata ?? EmptyMetadata(), JsonOptions)
        };
    }

    private static GovernanceOutboxEntry[] ToEntries(IEnumerable<GovernanceOutboxEntryEntity> entities)
    {
        return [.. entities.Select(ToEntry)];
    }

    private static GovernanceOutboxEntry ToEntry(GovernanceOutboxEntryEntity entity)
    {
        GovernanceEmissionPayload? payload = string.IsNullOrWhiteSpace(entity.EnvelopePayloadType)
            ? null
            : GovernanceEmissionPayload.Create(
                entity.EnvelopePayloadType,
                entity.EnvelopePayloadSchemaVersion,
                entity.EnvelopePayloadContentType,
                entity.EnvelopePayloadContentHash,
                entity.EnvelopePayloadSizeBytes,
                DeserializeMetadata(entity.EnvelopePayloadMetadataJson));

        var envelope = GovernanceEmissionEnvelope.Create(
            entity.EnvelopeEventType,
            entity.EnvelopeEventId,
            entity.EnvelopeOccurredUtc,
            entity.EnvelopeId,
            entity.EnvelopeCreatedUtc,
            entity.EnvelopeSchemaVersion,
            entity.EnvelopeCorrelationId,
            entity.EnvelopeDecisionReceiptId,
            entity.EnvelopeLifecycleStage,
            entity.EnvelopePolicyVersion,
            entity.EnvelopePolicyHash,
            entity.EnvelopeTraceId,
            entity.EnvelopeSpanId,
            entity.EnvelopeParentSpanId,
            entity.EnvelopeOperationName,
            entity.EnvelopeOutcome,
            entity.EnvelopeActorId,
            entity.EnvelopeEmitterStatus,
            entity.EnvelopeEmitterProvider,
            entity.EnvelopeOutboxSequence,
            entity.EnvelopeGatewayExecutionId,
            entity.EnvelopeDecisionStage,
            payload,
            DeserializeMetadata(entity.EnvelopeMetadataJson));

        GovernanceEmissionError? lastError = string.IsNullOrWhiteSpace(entity.LastErrorCode) || string.IsNullOrWhiteSpace(entity.LastErrorMessage)
            ? null
            : GovernanceEmissionError.Create(
                entity.LastErrorCode,
                entity.LastErrorMessage,
                entity.LastErrorIsRetryable ?? false,
                entity.LastErrorProviderName,
                entity.LastErrorProviderErrorCode);

        return GovernanceOutboxEntry.Restore(
            envelope,
            entity.Status,
            entity.OutboxEntryId,
            entity.CreatedUtc,
            entity.UpdatedUtc,
            entity.RetryCount,
            entity.MaxRetryCount,
            entity.NextRetryUtc,
            lastError,
            entity.ProviderName,
            entity.ProviderRecordId,
            entity.DeadLetterReason,
            DeserializeMetadata(entity.MetadataJson),
            entity.ClaimOwner,
            entity.ClaimToken,
            entity.ClaimedUtc,
            entity.ClaimExpiresUtc,
            entity.ClaimAttemptCount);
    }

    private static GovernanceOutboxClaim CreateClaim(GovernanceOutboxEntry entry)
    {
        return GovernanceOutboxClaim.Create(
            entry,
            entry.ClaimOwner ?? throw new InvalidOperationException("Claimed entry is missing claim owner."),
            entry.ClaimToken ?? throw new InvalidOperationException("Claimed entry is missing claim token."),
            entry.ClaimedUtc ?? throw new InvalidOperationException("Claimed entry is missing claimed timestamp."),
            entry.ClaimExpiresUtc ?? throw new InvalidOperationException("Claimed entry is missing claim expiration timestamp."));
    }

    internal static bool IsPendingClaimEligible(GovernanceOutboxEntryEntity entity, DateTimeOffset utcNow)
    {
        return entity.Status is GovernanceEmissionStatus.Pending && IsClaimAvailable(entity, utcNow);
    }

    internal static bool IsRetryReadyClaimEligible(GovernanceOutboxEntryEntity entity, DateTimeOffset utcNow)
    {
        return (entity.Status is GovernanceEmissionStatus.Deferred or GovernanceEmissionStatus.Failed or GovernanceEmissionStatus.RetryableFailure)
            && (entity.NextRetryUtc is null || entity.NextRetryUtc <= utcNow.ToUniversalTime())
            && IsClaimAvailable(entity, utcNow);
    }

    private static bool IsClaimAvailable(GovernanceOutboxEntryEntity entity, DateTimeOffset utcNow)
    {
        return entity.ClaimToken is null || entity.ClaimExpiresUtc is null || entity.ClaimExpiresUtc <= utcNow.ToUniversalTime();
    }

    private static bool IsTerminal(GovernanceOutboxEntry entry)
    {
        return entry.IsDelivered || entry.IsDeadLettered;
    }

    private static void DetachEntries(DbUpdateConcurrencyException exception)
    {
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry in exception.Entries)
        {
            entry.State = EntityState.Detached;
        }
    }

    private static ReadOnlyDictionary<string, string> DeserializeMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EmptyMetadata();
        }

        Dictionary<string, string>? metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);

        return metadata is null || metadata.Count == 0
            ? EmptyMetadata()
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata, StringComparer.Ordinal));
    }

    private static ReadOnlyDictionary<string, string> EmptyMetadata()
    {
        return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private static int NormalizeMaxCount(int maxCount)
    {
        return maxCount <= 0
            ? throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "Maximum count must be greater than zero.")
            : maxCount;
    }
}
