using System.Text.Json;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Serialization;
using Xunit;

namespace AsiBackbone.Core.Tests.Audit;

/// <summary>
/// Unit tests for stable audit ledger record schema version serialization.
/// </summary>
public sealed class AuditLedgerRecordSchemaVersionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Verifies that audit ledger records default to the initial stable schema version and serialize it explicitly.
    /// </summary>
    [Fact]
    public void FromResidueDefaultsAndSerializesStableSchemaVersion()
    {
        var record = AuditLedgerRecord.FromResidue(CreateValidResidue());

        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV1, record.SchemaVersion);
        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV1, ReadSerializedSchemaVersion(record));
    }

    /// <summary>
    /// Verifies that audit ledger records preserve an explicit schema version and serialize it explicitly.
    /// </summary>
    [Fact]
    public void FromResiduePreservesAndSerializesExplicitSchemaVersion()
    {
        var record = AuditLedgerRecord.FromResidue(
            CreateValidResidue(),
            schemaVersion: " 1.1-test ");

        Assert.Equal("1.1-test", record.SchemaVersion);
        Assert.Equal("1.1-test", ReadSerializedSchemaVersion(record));
    }

    private static string ReadSerializedSchemaVersion(AuditLedgerRecord record)
    {
        string json = JsonSerializer.Serialize(record, JsonOptions);

        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty("schemaVersion").GetString()!;
    }

    private static TestAuditResidue CreateValidResidue()
    {
        return new TestAuditResidue
        {
            EventId = "event-123",
            OccurredUtc = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
            ActorId = "actor-123",
            ActorType = GovernanceActorType.System,
            ActorDisplayName = "System",
            OperationName = "system.sync",
            Outcome = "Allowed",
            ReasonCodes = ["policy.allowed"],
            CorrelationId = "correlation-123",
            TraceId = "trace-456",
            PolicyVersion = "v1",
            PolicyHash = "hash-abc",
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "test"
            }
        };
    }

    private sealed class TestAuditResidue : IDecisionReceipt
    {
        public string EventId { get; set; } = "event-123";

        public DateTimeOffset OccurredUtc { get; set; } =
            new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

        public string ActorId { get; set; } = "actor-123";

        public GovernanceActorType ActorType { get; set; } = GovernanceActorType.System;

        public string? ActorDisplayName { get; set; } = "System";

        public string OperationName { get; set; } = "system.sync";

        public string Outcome { get; set; } = "Allowed";

        public IReadOnlyList<string> ReasonCodes { get; set; } = ["policy.allowed"];

        public string? CorrelationId { get; set; } = "correlation-123";

        public string? TraceId { get; set; } = "trace-456";

        public string? PolicyVersion { get; set; } = "v1";

        public string? PolicyHash { get; set; } = "hash-abc";

        public IReadOnlyDictionary<string, string> Metadata { get; set; } =
            new Dictionary<string, string>
            {
                ["source"] = "test"
            };
    }
}
