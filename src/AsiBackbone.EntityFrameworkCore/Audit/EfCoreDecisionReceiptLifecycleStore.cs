using System.Collections.ObjectModel;
using System.Text.Json;
using AsiBackbone.Core.Audit;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsiBackbone.EntityFrameworkCore.Audit;

/// <summary>
/// Entity Framework Core-backed decision receipt lifecycle store that persists records through a host-owned <see cref="DbContext" />.
/// </summary>
/// <remarks>
/// This store appends provider-neutral lifecycle events and intentionally relies on the host application to expose the ASI Backbone entities from its own <see cref="DbContext" /> and migrations.
/// </remarks>
public sealed class EfCoreDecisionReceiptLifecycleStore : IDecisionReceiptLifecycleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreDecisionReceiptLifecycleStore" /> class.
    /// </summary>
    /// <param name="dbContext">The host-owned database context.</param>
    public EfCoreDecisionReceiptLifecycleStore(DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async ValueTask<DecisionReceiptLifecycleEvent> AppendAsync(
        DecisionReceiptLifecycleEvent lifecycleEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);
        cancellationToken.ThrowIfCancellationRequested();

        _ = await dbContext
            .Set<DecisionReceiptLifecycleEventEntity>()
            .AddAsync(ToEntity(lifecycleEvent), cancellationToken)
            .ConfigureAwait(false);

        _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return lifecycleEvent;
    }

    /// <inheritdoc />
    public async ValueTask<DecisionReceiptLifecycleEvent?> FindByEventIdAsync(
        string eventId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        string normalizedEventId = eventId.Trim();

        DecisionReceiptLifecycleEventEntity? entity = await LifecycleEvents()
            .Where(lifecycleEvent => lifecycleEvent.EventId == normalizedEventId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToLifecycleEvent(entity);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DecisionReceiptLifecycleEvent>> FindByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string normalizedCorrelationId = correlationId.Trim();

        List<DecisionReceiptLifecycleEventEntity> entities = await LifecycleEvents()
            .Where(lifecycleEvent => lifecycleEvent.CorrelationId == normalizedCorrelationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. ToLifecycleEvents(entities)
            .OrderBy(lifecycleEvent => lifecycleEvent.OccurredUtc)
            .ThenBy(lifecycleEvent => lifecycleEvent.EventId, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DecisionReceiptLifecycleEvent>> FindByDecisionReceiptIdAsync(
        string decisionReceiptId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionReceiptId);

        string normalizedDecisionReceiptId = decisionReceiptId.Trim();

        List<DecisionReceiptLifecycleEventEntity> entities = await LifecycleEvents()
            .Where(lifecycleEvent => lifecycleEvent.DecisionReceiptId == normalizedDecisionReceiptId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. ToLifecycleEvents(entities)
            .OrderBy(lifecycleEvent => lifecycleEvent.OccurredUtc)
            .ThenBy(lifecycleEvent => lifecycleEvent.EventId, StringComparer.Ordinal)];
    }

    private IQueryable<DecisionReceiptLifecycleEventEntity> LifecycleEvents()
    {
        return dbContext.Set<DecisionReceiptLifecycleEventEntity>().AsNoTracking();
    }

    private static DecisionReceiptLifecycleEventEntity ToEntity(DecisionReceiptLifecycleEvent lifecycleEvent)
    {
        return new DecisionReceiptLifecycleEventEntity
        {
            EventId = lifecycleEvent.EventId,
            Stage = lifecycleEvent.Stage,
            StageSequence = lifecycleEvent.StageSequence,
            OccurredUtc = lifecycleEvent.OccurredUtc,
            CorrelationId = lifecycleEvent.CorrelationId,
            DecisionReceiptId = lifecycleEvent.DecisionReceiptId,
            TraceId = lifecycleEvent.TraceId,
            OperationName = lifecycleEvent.OperationName,
            Outcome = lifecycleEvent.Outcome,
            MetadataJson = JsonSerializer.Serialize(lifecycleEvent.Metadata, JsonOptions)
        };
    }

    private static DecisionReceiptLifecycleEvent[] ToLifecycleEvents(IEnumerable<DecisionReceiptLifecycleEventEntity> entities)
    {
        return [.. entities.Select(ToLifecycleEvent)];
    }

    private static DecisionReceiptLifecycleEvent ToLifecycleEvent(DecisionReceiptLifecycleEventEntity entity)
    {
        return DecisionReceiptLifecycleEvent.Create(
            entity.Stage,
            entity.CorrelationId,
            entity.DecisionReceiptId,
            entity.EventId,
            entity.OccurredUtc,
            entity.TraceId,
            entity.OperationName,
            entity.Outcome,
            DeserializeMetadata(entity.MetadataJson));
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
}
