using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Results;
using AsiBackbone.Testing.Contracts;
using Xunit;

namespace AsiBackbone.Testing.Tests.Contracts;

/// <summary>
/// Covers defensive decision and audit-residue contract branches.
/// </summary>
public sealed class AsiBackboneDecisionContractDefensiveTests
{
    /// <summary>
    /// Verifies that VerifySafeDecision throws for an unsupported outcome.
    /// </summary>
    [Fact]
    public void VerifySafeDecisionRejectsUnsupportedOutcome()
    {
        var decision = GovernanceDecision.Allow();
        SetAutoProperty(decision, nameof(GovernanceDecision.Outcome), (GovernanceDecisionOutcome)int.MaxValue);

        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifySafeDecision(decision, "unsupported decision"));

        Assert.Contains("unsupported outcome", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that outcomes requiring reasons are rejected when none are provided.
    /// </summary>
    [Theory]
    [InlineData(GovernanceDecisionOutcome.Warning)]
    [InlineData(GovernanceDecisionOutcome.Denied)]
    [InlineData(GovernanceDecisionOutcome.Deferred)]
    [InlineData(GovernanceDecisionOutcome.AcknowledgmentRequired)]
    [InlineData(GovernanceDecisionOutcome.EscalationRecommended)]
    public void VerifySafeDecisionRejectsReasonRequiredOutcomeWithoutReasons(GovernanceDecisionOutcome outcome)
    {
        var decision = GovernanceDecision.Allow();
        SetAutoProperty(decision, nameof(GovernanceDecision.Outcome), outcome);

        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifySafeDecision(decision, "reason-required decision"));

        Assert.Contains("at least one reason code", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a null reason inside a decision is rejected.
    /// </summary>
    [Fact]
    public void VerifySafeDecisionRejectsNullReason()
    {
        var decision = GovernanceDecision.Warning("contract.warning", "Warning.");
        SetAutoProperty(decision, nameof(GovernanceDecision.Reasons), new OperationReason?[] { null });

        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifySafeDecision(decision, "null-reason decision"));

        Assert.Contains("null reason", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that blank reason codes or messages are rejected.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VerifySafeDecisionRejectsBlankReasonMembers(bool blankCode)
    {
        OperationReason reason = CreateMalformedReason(
            blankCode ? " " : "contract.reason",
            blankCode ? "Reason message." : " ");
        var decision = GovernanceDecision.Warning("contract.warning", "Warning.");
        SetAutoProperty(decision, nameof(GovernanceDecision.Reasons), new[] { reason });

        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifySafeDecision(decision, "malformed-reason decision"));

        Assert.Contains(blankCode ? "empty code" : "empty message", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that invalid capability grants must not return Allow.
    /// </summary>
    [Fact]
    public void VerifyInvalidCapabilityGrantRejectsAllow()
    {
        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifyInvalidCapabilityGrantDoesNotAllow(GovernanceDecision.Allow()));

        Assert.Contains("must not return Allow", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies telemetry correlation ID preservation is enforced.
    /// </summary>
    [Fact]
    public void VerifyTelemetryRejectsCorrelationMismatch()
    {
        var context = new GovernanceEvaluationContext(correlationId: "expected-correlation");
        var decision = GovernanceDecision.Allow(correlationId: "different-correlation");

        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifyTelemetryFromContext(decision, context));

        Assert.Contains("preserve the supplied correlation ID", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies required policy telemetry presence is enforced when expected.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VerifyTelemetryRejectsMissingRequiredPolicyTelemetry(bool requireVersion)
    {
        var context = new GovernanceEvaluationContext(
            policyVersion: requireVersion ? "policy-v1" : null,
            policyHash: requireVersion ? null : "policy-hash");

        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifyTelemetryFromContext(GovernanceDecision.Allow(), context));

        Assert.Contains(requireVersion ? "policy version" : "policy hash", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that absent optional telemetry does not cause failure.
    /// </summary>
    [Fact]
    public void VerifyTelemetryAcceptsAbsentOptionalTelemetry()
    {
        var context = new GovernanceEvaluationContext();
        var decision = GovernanceDecision.Allow();

        GovernanceDecision verified = GovernanceDecisionContract.VerifyTelemetryFromContext(decision, context);

        Assert.Same(decision, verified);
    }

    /// <summary>
    /// Verifies that null context argument triggers ArgumentNullException.
    /// </summary>
    [Fact]
    public void VerifyTelemetryRejectsNullContext()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            GovernanceDecisionContract.VerifyTelemetryFromContext<GovernanceEvaluationContext>(
                GovernanceDecision.Allow(),
                null!));
    }

    /// <summary>
    /// Verifies that VerifyAuditResidue rejects null input.
    /// </summary>
    [Fact]
    public void VerifyAuditResidueRejectsNull()
    {
        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifyAuditResidue(null));

        Assert.Contains("must not be null", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies required string properties on audit residue are validated.
    /// </summary>
    [Theory]
    [InlineData(nameof(TestAuditResidue.EventId), "event ID")]
    [InlineData(nameof(TestAuditResidue.ActorId), "actor ID")]
    [InlineData(nameof(TestAuditResidue.OperationName), "operation name")]
    [InlineData(nameof(TestAuditResidue.Outcome), "outcome")]
    public void VerifyAuditResidueRejectsMissingRequiredStrings(string propertyName, string expectedMessagePart)
    {
        var residue = new TestAuditResidue();
        typeof(TestAuditResidue).GetProperty(propertyName)!.SetValue(residue, " ");

        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifyAuditResidue(residue));

        Assert.Contains(expectedMessagePart, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies audit residue rejects null reason-code collection.
    /// </summary>
    [Fact]
    public void VerifyAuditResidueRejectsNullReasonCodes()
    {
        var residue = new TestAuditResidue { ReasonCodes = null! };

        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifyAuditResidue(residue));

        Assert.Contains("reason-code collection", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies audit residue rejects blank reason codes.
    /// </summary>
    [Fact]
    public void VerifyAuditResidueRejectsBlankReasonCode()
    {
        var residue = new TestAuditResidue { ReasonCodes = new[] { "contract.reason", " " } };

        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifyAuditResidue(residue));

        Assert.Contains("empty reason code at index 1", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies audit residue rejects null metadata collection.
    /// </summary>
    [Fact]
    public void VerifyAuditResidueRejectsNullMetadata()
    {
        var residue = new TestAuditResidue { Metadata = null! };

        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifyAuditResidue(residue));

        Assert.Contains("metadata collection", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies VerifyAuditResidue returns the original valid residue instance.
    /// </summary>
    [Fact]
    public void VerifyAuditResidueReturnsOriginalValidResidue()
    {
        var residue = new TestAuditResidue();

        IDecisionReceipt verified = GovernanceDecisionContract.VerifyAuditResidue(residue);

        Assert.Same(residue, verified);
    }

    private static OperationReason CreateMalformedReason(string code, string message)
    {
        var reason = (OperationReason)RuntimeHelpers.GetUninitializedObject(typeof(OperationReason));
        SetAutoProperty(reason, nameof(OperationReason.Code), code);
        SetAutoProperty(reason, nameof(OperationReason.Message), message);
        SetAutoProperty(
            reason,
            nameof(OperationReason.Metadata),
            new Dictionary<string, string>(StringComparer.Ordinal));
        return reason;
    }

    private static void SetAutoProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        FieldInfo field = typeof(TTarget).GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Backing field for '{propertyName}' was not found.");
        field.SetValue(target, value);
    }

    private sealed class TestAuditResidue : IDecisionReceipt
    {
        public string EventId { get; set; } = "contract-event";
        public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
        public string ActorId { get; set; } = "contract-actor";
        public GovernanceActorType ActorType { get; set; } = GovernanceActorType.System;
        public string? ActorDisplayName { get; set; } = "Contract Actor";
        public string OperationName { get; set; } = "contract.operation";
        public string Outcome { get; set; } = "Allowed";
        public IReadOnlyList<string> ReasonCodes { get; set; } = Array.Empty<string>();
        public string? CorrelationId { get; set; } = "contract-correlation";
        public string? TraceId { get; set; } = "contract-trace";
        public string? PolicyVersion { get; set; } = "contract-policy-v1";
        public string? PolicyHash { get; set; } = "contract-policy-hash";
        public IReadOnlyDictionary<string, string> Metadata { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
