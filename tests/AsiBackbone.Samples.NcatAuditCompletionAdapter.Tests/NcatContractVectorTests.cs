using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.HostIntegration;
using AsiBackbone.Storage.InMemory.Audit;
using Xunit;

namespace AsiBackbone.Samples.NcatAuditCompletionAdapter.Tests;

/// <summary>
/// Replays NCAT's pinned audit-completion contract vectors against the independently modeled adapter contract.
/// </summary>
/// <remarks>
/// The vectors are vendored from a pinned NCAT commit under <c>ContractVectors/ncat</c>. See
/// <c>docs/articles/ncat-audit-completion-adapter.md</c> for the update procedure.
/// </remarks>
public sealed class NcatContractVectorTests
{
    private const string ReviewGuidance =
        "NCAT published a contract vector this adapter does not recognize. Review the NCAT change, update the " +
        "adapter contract if needed, and extend these tests before re-pinning the vectors.";

    private static readonly Lazy<ContractFixture> Fixture = new(ContractFixture.Load);

    private static readonly JsonSerializerOptions MessageJsonOptions = new(JsonSerializerDefaults.Web);

    // Explicit allowlist: a vector NCAT adds must be reviewed here before it becomes a test case.
    private static readonly HashSet<string> SupportedVectorNames =
        new(StringComparer.Ordinal)
        {
            "committed-single-record-all-identifiers",
            "committed-multi-record-canonical-ordering",
            "committed-null-identifiers-custom-destination",
            "committed-padded-mutation-batch-id"
        };

    private static readonly Dictionary<string, string> ExpectedInvalidReasons =
        new(StringComparer.Ordinal)
        {
            ["manifest-digest-mismatch"] = "manifest-digest-mismatch",
            ["manifest-digest-not-hex"] = "invalid-manifest-hash",
            ["manifest-algorithm-unsupported"] = "unsupported-manifest-algorithm",
            ["audit-record-count-mismatch"] = "manifest-record-count-mismatch",
            ["mutation-batch-id-missing"] = "mutation-batch-id-required",
            ["destination-too-long"] = "invalid-destination",
            ["idempotency-key-mismatch"] = "idempotency-key-mismatch",
            ["persistence-outcome-unsupported"] = "unsupported-persistence-outcome",
            ["message-schema-version-unsupported"] = "unsupported-message-schema-version",
            ["manifest-schema-version-unsupported"] = "unsupported-manifest-schema-version",
            ["optional-identifier-whitespace"] = "malformed-optional-identifier"
        };

    private static readonly Dictionary<string, GovernedOperationPersistenceOutcome> WithoutReceiptOutcomes =
        new(StringComparer.Ordinal)
        {
            ["no-mutation"] = GovernedOperationPersistenceOutcome.CompletedWithoutMutation,
            ["failed"] = GovernedOperationPersistenceOutcome.Failed,
            ["rolled-back"] = GovernedOperationPersistenceOutcome.RolledBack
        };

    /// <summary>Gets the names of the valid NCAT vectors this adapter has reviewed.</summary>
    /// <remarks>
    /// Drawn from the supported-vector allowlist rather than the fixture, so a newly published vector fails
    /// <see cref="FixtureVectorNamesMatchSupportedAllowlist" /> instead of silently becoming a test case.
    /// </remarks>
    public static TheoryData<string> VectorNames => [.. SupportedVectorNames.Order(StringComparer.Ordinal)];

    /// <summary>Gets the names of the invalid NCAT messages.</summary>
    public static TheoryData<string> InvalidMessageNames => [.. Fixture.Value.InvalidMessages.Keys];

    /// <summary>Gets the NCAT scenarios that produce no completion message.</summary>
    public static TheoryData<string> WithoutReceiptScenarios => [.. Fixture.Value.WithoutReceiptScenarios];

    /// <summary>
    /// Verifies the vendored vectors are the exact bytes recorded in the pin file.
    /// </summary>
    [Fact]
    public void VendoredVectorsMatchPinnedDigest()
    {
        ContractFixture fixture = Fixture.Value;
        string actual = Convert.ToHexStringLower(SHA256.HashData(fixture.Bytes));

        Assert.True(
            string.Equals(fixture.Pin.Sha256, actual, StringComparison.Ordinal),
            $"The vendored NCAT vectors changed without re-pinning. Expected SHA-256 {fixture.Pin.Sha256} for " +
            $"{fixture.Pin.Repository}@{fixture.Pin.Revision}, found {actual}.");
        Assert.Matches("^[0-9a-f]{40}$", fixture.Pin.Revision);
    }

    /// <summary>
    /// Verifies the pinned contract version, schema versions, algorithm, and idempotency rules match the adapter contract.
    /// </summary>
    [Fact]
    public void ContractDescriptorMatchesAdapterContract()
    {
        JsonElement root = Fixture.Value.Root;

        Assert.Equal("ncat.audit-completion", root.GetProperty("contract").GetString());
        string contractVersion = root.GetProperty("contractVersion").GetString()!;
        Assert.True(
            contractVersion.StartsWith($"{NcatAuditCompletionContract.SupportedContractMajorVersion}.", StringComparison.Ordinal),
            $"NCAT contract version {contractVersion} is not supported by this adapter. {ReviewGuidance}");

        JsonElement schemaVersions = root.GetProperty("schemaVersions");
        Assert.Equal(NcatAuditCompletionContract.MessageSchemaVersion, schemaVersions.GetProperty("completionMessage").GetString());
        Assert.Equal(NcatAuditCompletionContract.ManifestSchemaVersion, schemaVersions.GetProperty("mutationManifest").GetString());
        Assert.Equal(NcatAuditCompletionContract.ManifestAlgorithm, root.GetProperty("manifest").GetProperty("algorithm").GetString());
        Assert.Equal("uppercase-hex", root.GetProperty("manifest").GetProperty("digestEncoding").GetString());
        Assert.Equal(NcatAuditCompletionContract.IdempotencyKeyPrefix, root.GetProperty("idempotency").GetProperty("keyPrefix").GetString());

        string[] supportedOutcomes = [.. root.GetProperty("persistenceOutcomes").GetProperty("supported")
            .EnumerateArray()
            .Select(outcome => outcome.GetString()!)];
        Assert.True(
            supportedOutcomes.SequenceEqual([NcatAuditCompletionContract.CommittedOutcome], StringComparer.Ordinal),
            $"NCAT now publishes outcomes [{string.Join(", ", supportedOutcomes)}]. {ReviewGuidance}");
    }

    /// <summary>
    /// Verifies the pinned fixture publishes exactly the valid vectors this adapter has reviewed.
    /// </summary>
    [Fact]
    public void FixtureVectorNamesMatchSupportedAllowlist()
    {
        IReadOnlyDictionary<string, ContractVector> vectors = Fixture.Value.Vectors;
        string[] unrecognized = [.. vectors.Keys
            .Where(name => !SupportedVectorNames.Contains(name))
            .Order(StringComparer.Ordinal)];
        string[] missing = [.. SupportedVectorNames
            .Where(name => !vectors.ContainsKey(name))
            .Order(StringComparer.Ordinal)];

        Assert.True(
            unrecognized.Length == 0,
            $"NCAT publishes unrecognized vectors [{string.Join(", ", unrecognized)}]. {ReviewGuidance}");
        Assert.True(
            missing.Length == 0,
            $"NCAT no longer publishes vectors [{string.Join(", ", missing)}]. {ReviewGuidance}");
    }

    /// <summary>
    /// Verifies each valid vector's canonical manifest bytes, digest, and idempotency key.
    /// </summary>
    [Theory]
    [MemberData(nameof(VectorNames))]
    public void ValidVectorEvidenceIsReproducible(string vectorName)
    {
        ContractVector vector = GetSupportedVector(vectorName);
        byte[] manifestBytes = new UTF8Encoding(false).GetBytes(vector.CanonicalManifestJson);

        Assert.Equal(vector.CanonicalManifestByteLength, manifestBytes.Length);
        Assert.Equal(vector.ExpectedDigest, Convert.ToHexString(SHA256.HashData(manifestBytes)));
        Assert.Equal(vector.ExpectedDigest, vector.Message.MutationManifestHash);
        Assert.Equal(
            vector.Message.IdempotencyKey,
            NcatAuditCompletionContract.ComputeIdempotencyKey(vector.Message.Destination, vector.Message.MutationBatchId));
        Assert.True(
            NcatAuditCompletionContract.TryVerifyCanonicalManifest(vector.Message, manifestBytes, out string? reason),
            reason);
    }

    /// <summary>
    /// Verifies each valid vector's receipt and dispatched message agree field by field.
    /// </summary>
    [Theory]
    [MemberData(nameof(VectorNames))]
    public void ValidVectorReceiptAndMessageAgree(string vectorName)
    {
        ContractVector vector = GetSupportedVector(vectorName);
        JsonElement receipt = vector.Receipt;
        NcatAuditCompletionMessage message = vector.Message;

        Assert.Equal(receipt.GetProperty("mutationBatchId").GetString(), message.MutationBatchId);
        Assert.Equal(receipt.GetProperty("auditRecordCount").GetInt32(), message.AuditRecordCount);
        Assert.Equal(receipt.GetProperty("persistenceOutcome").GetString(), message.PersistenceOutcome);
        Assert.Equal(receipt.GetProperty("completedUtc").GetDateTimeOffset(), message.ReceiptCompletedUtc);
        Assert.Equal(receipt.GetProperty("mutationManifestHash").GetString(), message.MutationManifestHash);
        Assert.Equal(receipt.GetProperty("mutationManifestAlgorithm").GetString(), message.MutationManifestAlgorithm);
        Assert.Equal(receipt.GetProperty("mutationManifestSchemaVersion").GetString(), message.MutationManifestSchemaVersion);
        Assert.Equal(GetOptional(receipt, "operationExecutionId"), message.OperationExecutionId);
        Assert.Equal(GetOptional(receipt, "executionAttemptId"), message.ExecutionAttemptId);
        Assert.Equal(GetOptional(receipt, "decisionAuditRecordId"), message.DecisionAuditRecordId);
        Assert.Equal(GetOptional(receipt, "correlationId"), message.CorrelationId);
        Assert.Equal(GetOptional(receipt, "traceId"), message.TraceId);
    }

    /// <summary>
    /// Verifies every valid vector passes the contract and is delivered, or rejected only for the adapter's
    /// documented governed-operation requirements.
    /// </summary>
    [Theory]
    [MemberData(nameof(VectorNames))]
    public async Task ValidVectorIsAcceptedAndDeliveredWithBoundEvidence(string vectorName)
    {
        NcatAuditCompletionMessage message = GetSupportedVector(vectorName).Message;

        Assert.True(
            NcatAuditCompletionContract.TryCreateHandoff(message, deliveryAttempt: 1, out NcatAuditCompletionHandoff? handoff, out string? reason),
            reason);
        Assert.Equal(message.IdempotencyKey, handoff.CompletionEntryId);

        InMemoryDecisionReceiptLifecycleStore store = new();
        NcatAuditCompletionAdapter adapter = new(store, new StubResolver(new TestDecisionReceipt(message)));
        NcatAuditCompletionDeliveryResult result = await adapter.DeliverAsync(handoff, TestContext.Current.CancellationToken);

        // The adapter joins NCAT evidence to an AsiBackbone decision, so it requires operation and decision identifiers.
        if (string.IsNullOrWhiteSpace(message.OperationExecutionId))
        {
            AssertTerminal(result, "operation-execution-id-required");
            return;
        }

        if (string.IsNullOrWhiteSpace(message.DecisionAuditRecordId))
        {
            AssertTerminal(result, "decision-audit-record-id-required");
            return;
        }

        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Delivered, result.Disposition);
        Assert.True(result.ShouldAcknowledgeSource);
        GovernedOperationExecutionReceipt receipt = Assert.IsType<GovernedOperationExecutionReceipt>(result.Receipt);
        Assert.Equal(GovernedOperationPersistenceOutcome.Committed, receipt.PersistenceOutcome);
        Assert.True(receipt.HasCommittedMutation);
        Assert.Equal(message.OperationExecutionId.Trim(), receipt.OperationExecutionId);
        Assert.Equal(message.ExecutionAttemptId?.Trim(), receipt.ExecutionAttemptId);
        Assert.Equal(message.DecisionAuditRecordId.Trim(), receipt.DecisionAuditRecordId);
        Assert.Equal(message.MutationBatchId.Trim(), receipt.MutationBatchId);
        Assert.Equal(message.AuditRecordCount, receipt.MutationRecordCount);
        Assert.Equal(message.MutationManifestHash, receipt.MutationManifestHash, ignoreCase: true);
        Assert.Equal(message.MutationManifestAlgorithm, receipt.MutationManifestAlgorithm);
        Assert.Equal(message.ReceiptCompletedUtc, receipt.CompletedUtc);

        DecisionReceiptLifecycleEvent lifecycleEvent = Assert.IsType<DecisionReceiptLifecycleEvent>(result.LifecycleEvent);
        Assert.Equal(message.CorrelationId, lifecycleEvent.CorrelationId);
        Assert.Equal(message.TraceId, lifecycleEvent.TraceId);
        Assert.Equal(message.IdempotencyKey, lifecycleEvent.Metadata[NcatAuditCompletionAdapterMetadataKeys.CompletionEntryId]);

        NcatAuditCompletionDeliveryResult duplicate = await adapter.DeliverAsync(handoff, TestContext.Current.CancellationToken);
        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Duplicate, duplicate.Disposition);
        Assert.Equal(result.LifecycleEventId, duplicate.LifecycleEventId);
    }

    /// <summary>
    /// Verifies a lowercase manifest digest is rejected, because the contract encodes digests as uppercase hex.
    /// </summary>
    [Theory]
    [MemberData(nameof(VectorNames))]
    public void LowercaseManifestDigestIsRejected(string vectorName)
    {
        ContractVector vector = GetSupportedVector(vectorName);
        byte[] manifestBytes = new UTF8Encoding(false).GetBytes(vector.CanonicalManifestJson);
        NcatAuditCompletionMessage message = vector.Message with
        {
            MutationManifestHash = vector.ExpectedDigest.ToLowerInvariant()
        };

        // Guards the negative case: the digest must contain a letter for lowercasing to change it.
        Assert.NotEqual(vector.ExpectedDigest, message.MutationManifestHash);

        Assert.False(NcatAuditCompletionContract.TryCreateHandoff(message, deliveryAttempt: 1, out _, out string? handoffReason));
        Assert.Equal("invalid-manifest-hash", handoffReason);
        Assert.False(NcatAuditCompletionContract.TryVerifyCanonicalManifest(message, manifestBytes, out string? verifyReason));
        Assert.Equal("invalid-manifest-hash", verifyReason);
    }

    /// <summary>
    /// Verifies a retained manifest followed by another JSON value is rejected even when the digest covers the
    /// combined bytes.
    /// </summary>
    [Theory]
    [InlineData("{}")]
    [InlineData(" 1")]
    [InlineData("\n\"trailing\"")]
    [InlineData(/*lang=json,strict*/ "\n{\"schemaVersion\":\"1.0\"}\n")]
    public void ManifestWithTrailingJsonValueIsRejected(string trailingContent)
    {
        ContractVector vector = GetSupportedVector("committed-single-record-all-identifiers");
        byte[] manifestBytes = new UTF8Encoding(false).GetBytes(vector.CanonicalManifestJson + trailingContent);
        NcatAuditCompletionMessage message = vector.Message with
        {
            MutationManifestHash = Convert.ToHexString(SHA256.HashData(manifestBytes))
        };

        Assert.False(NcatAuditCompletionContract.TryVerifyCanonicalManifest(message, manifestBytes, out string? reason));
        Assert.Equal("manifest-malformed", reason);
    }

    /// <summary>
    /// Verifies every published invalid message is rejected with the expected reason.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidMessageNames))]
    public void InvalidMessageIsRejected(string invalidName)
    {
        Assert.True(ExpectedInvalidReasons.TryGetValue(invalidName, out string? expectedReason), $"'{invalidName}': {ReviewGuidance}");
        NcatAuditCompletionMessage message = Fixture.Value.InvalidMessages[invalidName];

        if (NcatAuditCompletionContract.TryCreateHandoff(message, deliveryAttempt: 1, out _, out string? actualReason))
        {
            // Structurally valid messages must still fail against the retained canonical manifest they claim.
            ContractVector evidence = Fixture.Value.Vectors.Values.Single(
                vector => string.Equals(vector.Message.MutationBatchId, message.MutationBatchId, StringComparison.Ordinal));
            byte[] manifestBytes = new UTF8Encoding(false).GetBytes(evidence.CanonicalManifestJson);

            Assert.False(
                NcatAuditCompletionContract.TryVerifyCanonicalManifest(message, manifestBytes, out actualReason),
                $"Invalid NCAT message '{invalidName}' was accepted.");
        }

        Assert.Equal(expectedReason, actualReason);
    }

    /// <summary>
    /// Verifies outcomes NCAT reports without a completion message cannot claim committed evidence.
    /// </summary>
    [Theory]
    [MemberData(nameof(WithoutReceiptScenarios))]
    public async Task OutcomesWithoutReceiptCannotClaimCommittedEvidence(string scenario)
    {
        Assert.True(WithoutReceiptOutcomes.TryGetValue(scenario, out GovernedOperationPersistenceOutcome expectedOutcome), $"'{scenario}': {ReviewGuidance}");
        NcatAuditCompletionMessage committed = Fixture.Value.Vectors.Values
            .Select(vector => vector.Message)
            .First(message => message.OperationExecutionId is not null && message.DecisionAuditRecordId is not null);

        // An NCAT completion message never carries a non-committed outcome.
        Assert.False(NcatAuditCompletionContract.TryCreateHandoff(
            committed with { PersistenceOutcome = scenario }, 1, out _, out string? messageReason));
        Assert.Equal("unsupported-persistence-outcome", messageReason);

        Assert.True(NcatAuditCompletionContract.TryCreateHandoff(committed, 1, out NcatAuditCompletionHandoff? handoff, out _));
        NcatAuditCompletionAdapter adapter = new(
            new InMemoryDecisionReceiptLifecycleStore(),
            new StubResolver(new TestDecisionReceipt(committed)));

        // A host-reported non-committed outcome that still carries committed batch evidence is rejected.
        NcatAuditCompletionDeliveryResult claimingEvidence = await adapter.DeliverAsync(
            handoff with { PersistenceOutcome = scenario, CompletionEntryId = $"{scenario}-claiming" },
            TestContext.Current.CancellationToken);
        AssertTerminal(claimingEvidence, "invalid-execution-receipt");

        // Without batch evidence, the host-reported outcome is recorded as non-mutating.
        NcatAuditCompletionDeliveryResult withoutEvidence = await adapter.DeliverAsync(
            handoff with
            {
                PersistenceOutcome = scenario,
                CompletionEntryId = $"{scenario}-clean",
                MutationBatchId = null,
                AuditRecordCount = 0,
                MutationManifestHash = null,
                MutationManifestAlgorithm = null
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Delivered, withoutEvidence.Disposition);
        Assert.Equal(expectedOutcome, withoutEvidence.Receipt!.PersistenceOutcome);
        Assert.False(withoutEvidence.Receipt.HasCommittedMutation);
    }

    private static ContractVector GetSupportedVector(string vectorName)
    {
        if (!Fixture.Value.Vectors.TryGetValue(vectorName, out ContractVector? vector))
        {
            Assert.Fail($"NCAT no longer publishes supported vector '{vectorName}'. {ReviewGuidance}");
        }

        return vector;
    }

    private static void AssertTerminal(NcatAuditCompletionDeliveryResult result, string reasonCode)
    {
        Assert.Equal(NcatAuditCompletionDeliveryDisposition.Terminal, result.Disposition);
        Assert.Equal(reasonCode, result.ReasonCode);
        Assert.False(result.ShouldAcknowledgeSource);
    }

    private static string? GetOptional(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).ValueKind == JsonValueKind.Null
            ? null
            : element.GetProperty(propertyName).GetString();
    }

    private sealed record ContractPin(string Repository, string Revision, string SourcePath, string VendoredPath, string Sha256);

    private sealed record ContractVector(
        string CanonicalManifestJson,
        int CanonicalManifestByteLength,
        string ExpectedDigest,
        JsonElement Receipt,
        NcatAuditCompletionMessage Message);

    private sealed class ContractFixture
    {
        private ContractFixture(
            ContractPin pin,
            byte[] bytes,
            JsonElement root,
            IReadOnlyDictionary<string, ContractVector> vectors,
            IReadOnlyDictionary<string, NcatAuditCompletionMessage> invalidMessages,
            IReadOnlyList<string> withoutReceiptScenarios)
        {
            Pin = pin;
            Bytes = bytes;
            Root = root;
            Vectors = vectors;
            InvalidMessages = invalidMessages;
            WithoutReceiptScenarios = withoutReceiptScenarios;
        }

        public ContractPin Pin { get; }

        public byte[] Bytes { get; }

        public JsonElement Root { get; }

        public IReadOnlyDictionary<string, ContractVector> Vectors { get; }

        public IReadOnlyDictionary<string, NcatAuditCompletionMessage> InvalidMessages { get; }

        public IReadOnlyList<string> WithoutReceiptScenarios { get; }

        public static ContractFixture Load()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "ContractVectors", "ncat");
            ContractPin pin = JsonSerializer.Deserialize<ContractPin>(
                File.ReadAllBytes(Path.Combine(directory, "ncat-contract-pin.json")),
                MessageJsonOptions)!;
            byte[] bytes = File.ReadAllBytes(Path.Combine(directory, pin.VendoredPath));
            JsonElement root = JsonDocument.Parse(bytes).RootElement.Clone();

            Dictionary<string, ContractVector> vectors = new(StringComparer.Ordinal);
            foreach (JsonElement vector in root.GetProperty("vectors").EnumerateArray())
            {
                JsonElement manifest = vector.GetProperty("canonicalManifest");
                vectors.Add(
                    vector.GetProperty("name").GetString()!,
                    new ContractVector(
                        manifest.GetProperty("json").GetString()!,
                        manifest.GetProperty("utf8ByteLength").GetInt32(),
                        manifest.GetProperty("expectedDigest").GetString()!,
                        vector.GetProperty("receipt"),
                        ReadMessage(vector.GetProperty("message"))));
            }

            Dictionary<string, NcatAuditCompletionMessage> invalidMessages = new(StringComparer.Ordinal);
            foreach (JsonElement invalid in root.GetProperty("invalidMessages").EnumerateArray())
            {
                invalidMessages.Add(invalid.GetProperty("name").GetString()!, ReadMessage(invalid.GetProperty("message")));
            }

            string[] withoutReceipt = [.. root.GetProperty("persistenceOutcomes").GetProperty("withoutReceipt")
                .EnumerateArray()
                .Select(scenario => scenario.GetProperty("scenario").GetString()!)];

            return new ContractFixture(pin, bytes, root, vectors, invalidMessages, withoutReceipt);
        }

        private static NcatAuditCompletionMessage ReadMessage(JsonElement element)
        {
            return element.Deserialize<NcatAuditCompletionMessage>(MessageJsonOptions)
                ?? throw new InvalidOperationException("A contract vector message was null.");
        }
    }

    private sealed class StubResolver(IDecisionReceipt receipt) : INcatDecisionReceiptResolver
    {
        public ValueTask<IDecisionReceipt?> ResolveAsync(
            string decisionAuditRecordId,
            string? correlationId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IDecisionReceipt?>(receipt);
        }
    }

    private sealed class TestDecisionReceipt(NcatAuditCompletionMessage message) : IDecisionReceipt
    {
        public string EventId => message.DecisionAuditRecordId ?? "decision-unset";
        public string? DecisionReceiptId => message.DecisionAuditRecordId;
        public DateTimeOffset OccurredUtc => message.ReceiptCompletedUtc;
        public string ActorId => "synthetic-actor";
        public GovernanceActorType ActorType => GovernanceActorType.Human;
        public string? ActorDisplayName => "Synthetic Actor";
        public string OperationName => "synthetic.operation";
        public string Outcome => "Allowed";
        public IReadOnlyList<string> ReasonCodes => [];
        public string? CorrelationId => message.CorrelationId;
        public string? TraceId => message.TraceId;
        public string? PolicyVersion => "policy-v1";
        public string? PolicyHash => "policy-hash-1";
        public IReadOnlyDictionary<string, string> Metadata => new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
