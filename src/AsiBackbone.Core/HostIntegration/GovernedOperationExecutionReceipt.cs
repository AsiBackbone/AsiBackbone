using System.Collections.ObjectModel;
using System.Globalization;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.HostIntegration;

/// <summary>
/// Represents a framework-neutral completion receipt for one governed host execution attempt.
/// </summary>
/// <remarks>
/// The receipt binds governance lifecycle evidence to host-owned persistence without copying raw
/// original or current application values into AsiBackbone. Mutation details remain authoritative
/// in the host audit store; this receipt carries only opaque identifiers, counts, hashes, and
/// minimized metadata.
/// </remarks>
public sealed class GovernedOperationExecutionReceipt
{
    private const int MaximumIdentifierLength = 256;
    private const int MaximumProviderLength = 256;
    private const int MaximumHashLength = 1024;
    private const int MaximumAlgorithmLength = 128;
    private const int MaximumMetadataKeyLength = 128;
    private const int MaximumMetadataValueLength = 2048;

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    private GovernedOperationExecutionReceipt(
        string operationExecutionId,
        string? executionAttemptId,
        GovernedOperationPersistenceOutcome persistenceOutcome,
        string? mutationBatchId,
        int mutationRecordCount,
        string? mutationManifestHash,
        string? mutationManifestAlgorithm,
        DateTimeOffset completedUtc,
        string? persistenceProvider,
        string? decisionAuditRecordId,
        IReadOnlyDictionary<string, string> metadata)
    {
        OperationExecutionId = operationExecutionId;
        ExecutionAttemptId = executionAttemptId;
        PersistenceOutcome = persistenceOutcome;
        MutationBatchId = mutationBatchId;
        MutationRecordCount = mutationRecordCount;
        MutationManifestHash = mutationManifestHash;
        MutationManifestAlgorithm = mutationManifestAlgorithm;
        CompletedUtc = completedUtc;
        PersistenceProvider = persistenceProvider;
        DecisionAuditRecordId = decisionAuditRecordId;
        Metadata = metadata;
    }

    /// <summary>Gets the logical execution identifier shared across retries.</summary>
    public string OperationExecutionId { get; }

    /// <summary>Gets the identifier for this execution attempt, when supplied.</summary>
    public string? ExecutionAttemptId { get; }

    /// <summary>Gets the host persistence outcome.</summary>
    public GovernedOperationPersistenceOutcome PersistenceOutcome { get; }

    /// <summary>Gets the opaque host mutation batch identifier, when persistence committed.</summary>
    public string? MutationBatchId { get; }

    /// <summary>Gets the number of host-owned mutation records represented by the batch.</summary>
    public int MutationRecordCount { get; }

    /// <summary>Gets the canonical privacy-safe mutation manifest hash, when persistence committed.</summary>
    public string? MutationManifestHash { get; }

    /// <summary>Gets the mutation manifest hash algorithm, when persistence committed.</summary>
    public string? MutationManifestAlgorithm { get; }

    /// <summary>Gets the UTC timestamp when execution completed.</summary>
    public DateTimeOffset CompletedUtc { get; }

    /// <summary>Gets the host persistence provider or provider family, when supplied.</summary>
    public string? PersistenceProvider { get; }

    /// <summary>Gets the persisted AsiBackbone decision audit record identifier, when supplied.</summary>
    public string? DecisionAuditRecordId { get; }

    /// <summary>Gets minimized host metadata associated with the receipt.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>Gets a value indicating whether execution completed without persisted mutation.</summary>
    public bool CompletedWithoutMutation => PersistenceOutcome == GovernedOperationPersistenceOutcome.CompletedWithoutMutation;

    /// <summary>Gets a value indicating whether the receipt contains a committed mutation binding.</summary>
    public bool HasCommittedMutation => PersistenceOutcome == GovernedOperationPersistenceOutcome.Committed;

    /// <summary>
    /// Creates a validated execution completion receipt.
    /// </summary>
    public static GovernedOperationExecutionReceipt Create(
        string operationExecutionId,
        GovernedOperationPersistenceOutcome persistenceOutcome,
        string? executionAttemptId = null,
        string? mutationBatchId = null,
        int mutationRecordCount = 0,
        string? mutationManifestHash = null,
        string? mutationManifestAlgorithm = null,
        DateTimeOffset? completedUtc = null,
        string? persistenceProvider = null,
        string? decisionAuditRecordId = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (!Enum.IsDefined(persistenceOutcome))
        {
            throw new ArgumentOutOfRangeException(nameof(persistenceOutcome), persistenceOutcome, "Persistence outcome must be defined.");
        }

        if (mutationRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mutationRecordCount), mutationRecordCount, "Mutation record count cannot be negative.");
        }

        string normalizedOperationExecutionId = NormalizeRequired(operationExecutionId, nameof(operationExecutionId), MaximumIdentifierLength);
        string? normalizedExecutionAttemptId = NormalizeOptional(executionAttemptId, nameof(executionAttemptId), MaximumIdentifierLength);
        string? normalizedMutationBatchId = NormalizeOptional(mutationBatchId, nameof(mutationBatchId), MaximumIdentifierLength);
        string? normalizedManifestHash = NormalizeOptional(mutationManifestHash, nameof(mutationManifestHash), MaximumHashLength)?.ToLowerInvariant();
        string? normalizedManifestAlgorithm = NormalizeOptional(mutationManifestAlgorithm, nameof(mutationManifestAlgorithm), MaximumAlgorithmLength);
        string? normalizedPersistenceProvider = NormalizeOptional(persistenceProvider, nameof(persistenceProvider), MaximumProviderLength);
        string? normalizedDecisionAuditRecordId = NormalizeOptional(decisionAuditRecordId, nameof(decisionAuditRecordId), MaximumIdentifierLength);

        ValidateMutationBinding(
            persistenceOutcome,
            mutationRecordCount,
            normalizedMutationBatchId,
            normalizedManifestHash,
            normalizedManifestAlgorithm);

        return new GovernedOperationExecutionReceipt(
            normalizedOperationExecutionId,
            normalizedExecutionAttemptId,
            persistenceOutcome,
            normalizedMutationBatchId,
            mutationRecordCount,
            normalizedManifestHash,
            normalizedManifestAlgorithm,
            (completedUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            normalizedPersistenceProvider,
            normalizedDecisionAuditRecordId,
            NormalizeMetadata(metadata));
    }

    /// <summary>
    /// Builds lifecycle metadata from the typed receipt and optional minimized host metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> ToLifecycleMetadata(IReadOnlyDictionary<string, string>? metadata = null)
    {
        SortedDictionary<string, string> values = new(StringComparer.Ordinal);

        AddMetadata(values, Metadata);
        AddMetadata(values, metadata);

        values[HostAccountabilityMetadataKeys.OperationExecutionId] = OperationExecutionId;
        values[HostAccountabilityMetadataKeys.MutationRecordCount] = MutationRecordCount.ToString(CultureInfo.InvariantCulture);
        values[HostAccountabilityMetadataKeys.PersistenceOutcome] =
            CanonicalEnumWireNames.ForPersistenceOutcome(PersistenceOutcome);
        values[HostAccountabilityMetadataKeys.CompletedWithoutMutation] = CompletedWithoutMutation ? "true" : "false";

        AddOptional(values, HostAccountabilityMetadataKeys.ExecutionAttemptId, ExecutionAttemptId);
        AddOptional(values, HostAccountabilityMetadataKeys.MutationBatchId, MutationBatchId);
        AddOptional(values, HostAccountabilityMetadataKeys.MutationManifestHash, MutationManifestHash);
        AddOptional(values, HostAccountabilityMetadataKeys.MutationManifestAlgorithm, MutationManifestAlgorithm);
        AddOptional(values, HostAccountabilityMetadataKeys.PersistenceProvider, PersistenceProvider);
        AddOptional(values, HostAccountabilityMetadataKeys.DecisionAuditRecordId, DecisionAuditRecordId);

        return new ReadOnlyDictionary<string, string>(values);
    }

    private static void ValidateMutationBinding(
        GovernedOperationPersistenceOutcome outcome,
        int mutationRecordCount,
        string? mutationBatchId,
        string? mutationManifestHash,
        string? mutationManifestAlgorithm)
    {
        bool hasAnyBinding = mutationBatchId is not null || mutationManifestHash is not null || mutationManifestAlgorithm is not null;

        if (outcome == GovernedOperationPersistenceOutcome.Committed)
        {
            if (mutationRecordCount < 1)
            {
                throw new ArgumentException("A committed persistence outcome requires at least one mutation record.", nameof(mutationRecordCount));
            }

            if (mutationBatchId is null || mutationManifestHash is null || mutationManifestAlgorithm is null)
            {
                throw new ArgumentException("A committed persistence outcome requires a mutation batch identifier, manifest hash, and manifest algorithm.");
            }

            return;
        }

        if (mutationRecordCount != 0 || hasAnyBinding)
        {
            throw new ArgumentException("Failed, rolled-back, and no-mutation outcomes cannot claim a committed mutation batch.");
        }
    }

    private static string NormalizeRequired(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return Normalize(value, parameterName, maximumLength);
    }

    private static string? NormalizeOptional(string? value, string parameterName, int maximumLength)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Normalize(value, parameterName, maximumLength);
    }

    private static string Normalize(string value, string parameterName, int maximumLength)
    {
        string normalized = value.Trim();
        return normalized.Length > maximumLength
            ? throw new ArgumentOutOfRangeException(parameterName, value, $"Value cannot exceed {maximumLength} characters.")
            : normalized.Any(char.IsControl)
            ? throw new ArgumentException("Value cannot contain control characters.", parameterName)
            : normalized;
    }

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return EmptyMetadata;
        }

        SortedDictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> item in metadata)
        {
            string key = NormalizeRequired(item.Key, nameof(metadata), MaximumMetadataKeyLength);
            string value = Normalize(item.Value ?? string.Empty, nameof(metadata), MaximumMetadataValueLength);
            normalized[key] = value;
        }

        return new ReadOnlyDictionary<string, string>(normalized);
    }

    private static void AddMetadata(SortedDictionary<string, string> destination, IReadOnlyDictionary<string, string>? metadata)
    {
        IReadOnlyDictionary<string, string> normalized = NormalizeMetadata(metadata);
        foreach (KeyValuePair<string, string> item in normalized)
        {
            destination[item.Key] = item.Value;
        }
    }

    private static void AddOptional(SortedDictionary<string, string> destination, string key, string? value)
    {
        if (value is not null)
        {
            destination[key] = value;
        }
    }
}
