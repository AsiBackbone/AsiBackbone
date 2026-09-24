namespace AsiBackbone.Samples.NcatAuditCompletionAdapter;

/// <summary>
/// Models the minimized audit-completion message an NCAT completion-outbox publisher receives.
/// </summary>
/// <remarks>
/// This record is modeled independently from NCAT so neither product takes a compile-time dependency on
/// the other. Its field names and meanings follow NCAT's published audit-completion contract vectors, which
/// the sample tests replay to detect drift. Property names serialize to the camelCase shape used by those vectors.
/// </remarks>
public sealed record NcatAuditCompletionMessage(
    string SchemaVersion,
    string Destination,
    string IdempotencyKey,
    string MutationBatchId,
    int AuditRecordCount,
    string PersistenceOutcome,
    DateTimeOffset ReceiptCompletedUtc,
    string MutationManifestHash,
    string MutationManifestAlgorithm,
    string MutationManifestSchemaVersion,
    string? OperationExecutionId = null,
    string? ExecutionAttemptId = null,
    string? DecisionAuditRecordId = null,
    string? CorrelationId = null,
    string? TraceId = null);
