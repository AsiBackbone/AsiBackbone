using System.Security.Cryptography;
using System.Text;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.HostIntegration;

namespace AsiBackbone.Samples.NcatAuditCompletionAdapter;

/// <summary>
/// Maps a minimized NCAT audit-completion handoff into AsiBackbone governed execution evidence.
/// </summary>
/// <remarks>
/// This reference adapter intentionally depends only on AsiBackbone Core and the source-neutral
/// <see cref="NcatAuditCompletionHandoff" /> contract. A consuming host may translate its NCAT
/// completion receipt or completion-outbox entry into that contract without coupling either core product.
/// </remarks>
public sealed class NcatAuditCompletionAdapter
{
    private const string SourceCompletionEntryIdMetadataKey = "ncatCompletionEntryId";
    private const string SourceAdapterMetadataKey = "completionAdapter";
    private const string SourceAdapterMetadataValue = "NCAT";
    private const string LifecycleEventPrefix = "ncat-completion-";

    private readonly IDecisionReceiptLifecycleStore lifecycleStore;
    private readonly INcatDecisionReceiptResolver decisionReceiptResolver;
    private readonly NcatAuditCompletionAdapterOptions options;

    /// <summary>
    /// Initializes a new optional NCAT audit-completion adapter.
    /// </summary>
    public NcatAuditCompletionAdapter(
        IDecisionReceiptLifecycleStore lifecycleStore,
        INcatDecisionReceiptResolver decisionReceiptResolver,
        NcatAuditCompletionAdapterOptions? options = null)
    {
        this.lifecycleStore = lifecycleStore ?? throw new ArgumentNullException(nameof(lifecycleStore));
        this.decisionReceiptResolver = decisionReceiptResolver ?? throw new ArgumentNullException(nameof(decisionReceiptResolver));
        this.options = options ?? new NcatAuditCompletionAdapterOptions();
        this.options.Validate();
    }

    /// <summary>
    /// Attempts to append the governed completion lifecycle event before the source entry is acknowledged.
    /// </summary>
    public async ValueTask<NcatAuditCompletionDeliveryResult> DeliverAsync(
        NcatAuditCompletionHandoff handoff,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        cancellationToken.ThrowIfCancellationRequested();

        NcatAuditCompletionDeliveryResult? validationFailure = ValidateHandoff(handoff);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        if (!TryMapOutcome(handoff.PersistenceOutcome, out GovernedOperationPersistenceOutcome persistenceOutcome))
        {
            return Terminal("unsupported-persistence-outcome");
        }

        IDecisionReceipt? decisionReceipt = await decisionReceiptResolver.ResolveAsync(
            handoff.DecisionAuditRecordId!.Trim(),
            NormalizeOptional(handoff.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        if (decisionReceipt is null)
        {
            return new NcatAuditCompletionDeliveryResult(
                NcatAuditCompletionDeliveryDisposition.Deferred,
                "decision-residue-not-available");
        }

        NcatAuditCompletionDeliveryResult? correlationFailure = ValidateDecisionCorrelation(handoff, decisionReceipt);
        if (correlationFailure is not null)
        {
            return correlationFailure;
        }

        GovernedOperationExecutionReceipt executionReceipt;
        try
        {
            executionReceipt = GovernedOperationExecutionReceipt.Create(
                operationExecutionId: handoff.OperationExecutionId!,
                persistenceOutcome: persistenceOutcome,
                executionAttemptId: handoff.ExecutionAttemptId,
                mutationBatchId: handoff.MutationBatchId,
                mutationRecordCount: handoff.AuditRecordCount,
                mutationManifestHash: handoff.MutationManifestHash,
                mutationManifestAlgorithm: handoff.MutationManifestAlgorithm,
                completedUtc: handoff.CompletedUtc,
                persistenceProvider: options.PersistenceProvider,
                decisionAuditRecordId: handoff.DecisionAuditRecordId);
        }
        catch (ArgumentException exception)
        {
            return Terminal("invalid-execution-receipt", exception.GetType().Name);
        }

        string lifecycleEventId = CreateLifecycleEventId(handoff.CompletionEntryId);
        IReadOnlyDictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SourceCompletionEntryIdMetadataKey] = handoff.CompletionEntryId.Trim(),
            [SourceAdapterMetadataKey] = SourceAdapterMetadataValue
        };

        DecisionReceiptLifecycleEvent lifecycleEvent = HostAccountabilityLifecycleEvent.FromExecutionReceipt(
            decisionReceipt,
            executionReceipt,
            eventId: lifecycleEventId,
            metadata: metadata);

        DecisionReceiptLifecycleEvent? existing = await lifecycleStore.FindByEventIdAsync(
            lifecycleEventId,
            cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            return Equivalent(existing, lifecycleEvent)
                ? new NcatAuditCompletionDeliveryResult(
                    NcatAuditCompletionDeliveryDisposition.Duplicate,
                    "completion-already-delivered",
                    lifecycleEventId,
                    executionReceipt,
                    existing)
                : Terminal("idempotency-conflict", lifecycleEventId: lifecycleEventId);
        }

        try
        {
            DecisionReceiptLifecycleEvent appended = await lifecycleStore.AppendAsync(
                lifecycleEvent,
                cancellationToken).ConfigureAwait(false);

            return new NcatAuditCompletionDeliveryResult(
                NcatAuditCompletionDeliveryDisposition.Delivered,
                "lifecycle-event-appended",
                appended.EventId,
                executionReceipt,
                appended);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return PersistenceFailure(handoff, lifecycleEventId, executionReceipt, lifecycleEvent, exception);
        }
    }

    private static NcatAuditCompletionDeliveryResult? ValidateHandoff(NcatAuditCompletionHandoff handoff)
    {
        return string.IsNullOrWhiteSpace(handoff.CompletionEntryId)
            ? Terminal("completion-entry-id-required")
            : string.IsNullOrWhiteSpace(handoff.OperationExecutionId)
            ? Terminal("operation-execution-id-required")
            : string.IsNullOrWhiteSpace(handoff.DecisionAuditRecordId)
            ? Terminal("decision-audit-record-id-required")
            : string.IsNullOrWhiteSpace(handoff.PersistenceOutcome)
            ? Terminal("persistence-outcome-required")
            : handoff.AuditRecordCount < 0
            ? Terminal("audit-record-count-invalid")
            : handoff.DeliveryAttempt < 0
            ? Terminal("delivery-attempt-invalid")
            : null;
    }

    private static NcatAuditCompletionDeliveryResult? ValidateDecisionCorrelation(
        NcatAuditCompletionHandoff handoff,
        IDecisionReceipt decisionReceipt)
    {
        string? correlationId = NormalizeOptional(handoff.CorrelationId);
        if (correlationId is not null &&
            !string.Equals(correlationId, decisionReceipt.CorrelationId, StringComparison.Ordinal))
        {
            return Terminal("correlation-id-mismatch");
        }

        string? traceId = NormalizeOptional(handoff.TraceId);
        return traceId is not null &&
            !string.Equals(traceId, decisionReceipt.TraceId, StringComparison.Ordinal)
            ? Terminal("trace-id-mismatch")
            : null;
    }

    private NcatAuditCompletionDeliveryResult PersistenceFailure(
        NcatAuditCompletionHandoff handoff,
        string lifecycleEventId,
        GovernedOperationExecutionReceipt executionReceipt,
        DecisionReceiptLifecycleEvent lifecycleEvent,
        Exception exception)
    {
        bool deadLettered = options.DeadLetterAfterAttempts is int threshold &&
            handoff.DeliveryAttempt >= threshold;

        return new NcatAuditCompletionDeliveryResult(
            deadLettered
                ? NcatAuditCompletionDeliveryDisposition.DeadLetter
                : NcatAuditCompletionDeliveryDisposition.Retryable,
            deadLettered
                ? "lifecycle-persistence-dead-lettered"
                : "lifecycle-persistence-failed",
            lifecycleEventId,
            executionReceipt,
            lifecycleEvent,
            exception.GetType().Name);
    }

    private static bool TryMapOutcome(
        string sourceOutcome,
        out GovernedOperationPersistenceOutcome persistenceOutcome)
    {
        string normalized = string.Concat(sourceOutcome
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant));

        persistenceOutcome = normalized switch
        {
            "committed" => GovernedOperationPersistenceOutcome.Committed,
            "failed" => GovernedOperationPersistenceOutcome.Failed,
            "rolledback" => GovernedOperationPersistenceOutcome.RolledBack,
            "completedwithoutmutation" or "nomutation" => GovernedOperationPersistenceOutcome.CompletedWithoutMutation,
            _ => default
        };

        return normalized is "committed" or "failed" or "rolledback" or "completedwithoutmutation" or "nomutation";
    }

    private static string CreateLifecycleEventId(string completionEntryId)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(completionEntryId.Trim()));
        return string.Concat(LifecycleEventPrefix, Convert.ToHexString(digest).ToLowerInvariant());
    }

    private static bool Equivalent(
        DecisionReceiptLifecycleEvent existing,
        DecisionReceiptLifecycleEvent candidate)
    {
        if (existing.Stage != candidate.Stage ||
            !string.Equals(existing.CorrelationId, candidate.CorrelationId, StringComparison.Ordinal) ||
            !string.Equals(existing.AuditResidueId, candidate.AuditResidueId, StringComparison.Ordinal) ||
            !string.Equals(existing.TraceId, candidate.TraceId, StringComparison.Ordinal) ||
            !string.Equals(existing.OperationName, candidate.OperationName, StringComparison.Ordinal) ||
            !string.Equals(existing.Outcome, candidate.Outcome, StringComparison.Ordinal))
        {
            return false;
        }

        string[] keys =
        [
            SourceCompletionEntryIdMetadataKey,
            HostAccountabilityMetadataKeys.OperationExecutionId,
            HostAccountabilityMetadataKeys.ExecutionAttemptId,
            HostAccountabilityMetadataKeys.DecisionAuditRecordId,
            HostAccountabilityMetadataKeys.PersistenceOutcome,
            HostAccountabilityMetadataKeys.MutationBatchId,
            HostAccountabilityMetadataKeys.MutationRecordCount,
            HostAccountabilityMetadataKeys.MutationManifestHash,
            HostAccountabilityMetadataKeys.MutationManifestAlgorithm
        ];

        return keys.All(key => string.Equals(
            GetMetadata(existing.Metadata, key),
            GetMetadata(candidate.Metadata, key),
            StringComparison.Ordinal));
    }

    private static string? GetMetadata(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out string? value) ? value : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static NcatAuditCompletionDeliveryResult Terminal(
        string reasonCode,
        string? failureType = null,
        string? lifecycleEventId = null)
    {
        return new NcatAuditCompletionDeliveryResult(
            NcatAuditCompletionDeliveryDisposition.Terminal,
            reasonCode,
            lifecycleEventId,
            FailureType: failureType);
    }
}
