using AsiBackbone.Core.Classification;
using Xunit;

namespace AsiBackbone.Core.Tests.Classification;

/// <summary>
/// Unit tests for provider-neutral DLP and classification failure policy resolution.
/// </summary>
public sealed class DefaultAsiBackboneDlpFailurePolicyResolverTests
{
    /// <summary>
    /// Verifies that the default behavior for a low-risk service unavailable failure is to warn and allow, and that the resolution reflects this behavior correctly.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of resolving the DLP failure policy and asserting the expected behavior.
    /// </returns>
    [Fact]
    public async Task ResolveLowRiskServiceUnavailableWarnsAndAllowsByDefault()
    {
        var resolver = new DefaultDlpFailurePolicyResolver();
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            DlpIntentRiskLevel.Low,
            intentCategory: "metadata-only-emission",
            environment: "Development");

        DlpFailurePolicyResolution resolution = await resolver.ResolveAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.Equal(DlpFailureBehavior.WarnAndAllow, resolution.Behavior);
        Assert.True(resolution.IsFailOpen);
        Assert.False(resolution.IsFailClosed);
        Assert.True(resolution.Decision.IsWarning);
        Assert.True(resolution.Decision.CanProceed);
        Assert.Contains(DlpFailureReasonCodes.ServiceUnavailable, resolution.Decision.ReasonCodes);
        Assert.Equal("service_unavailable", resolution.Reason.Metadata["dlp.failure_kind"]);
        Assert.Equal("low", resolution.Reason.Metadata["dlp.risk_level"]);
        Assert.Equal("warn_and_allow", resolution.Reason.Metadata["dlp.behavior"]);
        Assert.Equal("metadata-only-emission", resolution.Reason.Metadata["dlp.intent_category"]);
        Assert.Equal("Development", resolution.Reason.Metadata["dlp.environment"]);
    }

    /// <summary>
    /// Verifies that the default behavior for a medium-risk timeout failure is to require acknowledgment, and that the resolution reflects this behavior correctly.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of resolving the DLP failure policy and asserting the expected behavior.
    /// </returns>
    [Fact]
    public async Task ResolveMediumRiskTimeoutRequiresAcknowledgmentByDefault()
    {
        var resolver = new DefaultDlpFailurePolicyResolver();
        var context = DlpFailurePolicyContext.TimeoutFailure(
            DlpIntentRiskLevel.Medium,
            timeout: TimeSpan.FromSeconds(2),
            correlationId: "corr-dlp-timeout",
            traceId: "trace-dlp-timeout",
            policyVersion: "policy-v1",
            policyHash: "hash-v1");

        DlpFailurePolicyResolution resolution = await resolver.ResolveAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.Equal(DlpFailureBehavior.RequireAcknowledgment, resolution.Behavior);
        Assert.True(resolution.Decision.RequiresAcknowledgment);
        Assert.False(resolution.Decision.CanProceed);
        Assert.Contains(DlpFailureReasonCodes.Timeout, resolution.Decision.ReasonCodes);
        Assert.Equal("DLP/classification screening timed out after 2000 ms.", resolution.Reason.Message);
        Assert.Equal("2000", resolution.Reason.Metadata["dlp.timeout_ms"]);
        Assert.Equal("corr-dlp-timeout", resolution.Decision.CorrelationId);
        Assert.Equal("trace-dlp-timeout", resolution.Decision.TraceId);
        Assert.Equal("policy-v1", resolution.Decision.PolicyVersion);
        Assert.Equal("hash-v1", resolution.Decision.PolicyHash);
    }

    /// <summary>
    /// Verifies that the default behavior for a high-risk indeterminate result failure is to deny access, and that the resolution reflects this behavior correctly.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of resolving the DLP failure policy and asserting the expected behavior.
    /// </returns>
    [Fact]
    public async Task ResolveHighRiskIndeterminateResultFailsClosedByDefault()
    {
        var resolver = new DefaultDlpFailurePolicyResolver();
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.IndeterminateResult,
            DlpIntentRiskLevel.High);

        DlpFailurePolicyResolution resolution = await resolver.ResolveAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.Equal(DlpFailureBehavior.Deny, resolution.Behavior);
        Assert.True(resolution.IsFailClosed);
        Assert.False(resolution.IsFailOpen);
        Assert.True(resolution.Decision.IsDenied);
        Assert.False(resolution.Decision.CanProceed);
        Assert.Contains(DlpFailureReasonCodes.IndeterminateResult, resolution.Decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that custom risk default behaviors are honored when no exact override exists.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of resolving the DLP failure policy and asserting the expected behavior.
    /// </returns>
    [Fact]
    public async Task CustomRiskDefaultBehaviorsAreHonoredWhenNoExactOverrideExists()
    {
        var resolver = new DefaultDlpFailurePolicyResolver(
            new DlpFailurePolicyOptions
            {
                LowRiskBehavior = DlpFailureBehavior.Allow,
                MediumRiskBehavior = DlpFailureBehavior.Defer,
                HighRiskBehavior = DlpFailureBehavior.Escalate
            });

        DlpFailurePolicyResolution lowResolution = await resolver.ResolveAsync(
            DlpFailurePolicyContext.Create(
                DlpClassificationFailureKind.ServiceUnavailable,
                DlpIntentRiskLevel.Low),
            TestContext.Current.CancellationToken);
        DlpFailurePolicyResolution mediumResolution = await resolver.ResolveAsync(
            DlpFailurePolicyContext.Create(
                DlpClassificationFailureKind.ServiceUnavailable,
                DlpIntentRiskLevel.Medium),
            TestContext.Current.CancellationToken);
        DlpFailurePolicyResolution highResolution = await resolver.ResolveAsync(
            DlpFailurePolicyContext.Create(
                DlpClassificationFailureKind.ServiceUnavailable,
                DlpIntentRiskLevel.High),
            TestContext.Current.CancellationToken);

        Assert.Equal(DlpFailureBehavior.Allow, lowResolution.Behavior);
        Assert.True(lowResolution.Decision.IsAllowed);
        Assert.True(lowResolution.Decision.CanProceed);

        Assert.Equal(DlpFailureBehavior.Defer, mediumResolution.Behavior);
        Assert.True(mediumResolution.Decision.IsDeferred);
        Assert.False(mediumResolution.Decision.CanProceed);

        Assert.Equal(DlpFailureBehavior.Escalate, highResolution.Behavior);
        Assert.True(highResolution.Decision.EscalationRecommended);
        Assert.False(highResolution.Decision.CanProceed);
    }

    /// <summary>
    /// Verifies that an exact behavior override takes precedence over the default behavior for a given risk level and failure kind.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of resolving the DLP failure policy and asserting the expected behavior.
    /// </returns>
    [Fact]
    public async Task ExactBehaviorOverrideWinsOverRiskDefaultBehavior()
    {
        var options = new DlpFailurePolicyOptions
        {
            MediumRiskBehavior = DlpFailureBehavior.Deny
        };
        options.BehaviorOverrides[new DlpFailurePolicyKey(
            DlpIntentRiskLevel.Medium,
            DlpClassificationFailureKind.Timeout)] = DlpFailureBehavior.Allow;

        var resolver = new DefaultDlpFailurePolicyResolver(options);
        var context = DlpFailurePolicyContext.TimeoutFailure(DlpIntentRiskLevel.Medium);

        DlpFailurePolicyResolution resolution = await resolver.ResolveAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.Equal(DlpFailureBehavior.Allow, resolution.Behavior);
        Assert.True(resolution.Decision.IsAllowed);
        Assert.True(resolution.Decision.CanProceed);
        Assert.Empty(resolution.Decision.ReasonCodes);
        Assert.Equal(DlpFailureReasonCodes.Timeout, resolution.Reason.Code);
    }

    /// <summary>
    /// Verifies that an override can escalate a high-risk blocked result, even if the default behavior for high risk is different.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of resolving the DLP failure policy and asserting the expected behavior.
    /// </returns>
    [Fact]
    public async Task OverrideCanEscalateHighRiskBlockedResult()
    {
        var options = new DlpFailurePolicyOptions();
        options.BehaviorOverrides[new DlpFailurePolicyKey(
            DlpIntentRiskLevel.High,
            DlpClassificationFailureKind.BlockedResult)] = DlpFailureBehavior.Escalate;

        var resolver = new DefaultDlpFailurePolicyResolver(options);
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.BlockedResult,
            DlpIntentRiskLevel.High);

        DlpFailurePolicyResolution resolution = await resolver.ResolveAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.Equal(DlpFailureBehavior.Escalate, resolution.Behavior);
        Assert.True(resolution.Decision.EscalationRecommended);
        Assert.Contains(DlpFailureReasonCodes.BlockedResult, resolution.Decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that an override can defer a low-risk classified result, even if the default behavior for low risk is different.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of resolving the DLP failure policy and asserting the expected behavior.
    /// </returns>
    [Fact]
    public async Task OverrideCanDeferLowRiskClassifiedResult()
    {
        var options = new DlpFailurePolicyOptions();
        options.BehaviorOverrides[new DlpFailurePolicyKey(
            DlpIntentRiskLevel.Low,
            DlpClassificationFailureKind.ClassifiedResult)] = DlpFailureBehavior.Defer;

        var resolver = new DefaultDlpFailurePolicyResolver(options);
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ClassifiedResult,
            DlpIntentRiskLevel.Low,
            metadata: new Dictionary<string, string>
            {
                [" classification.source "] = " unit-test "
            });

        DlpFailurePolicyResolution resolution = await resolver.ResolveAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.Equal(DlpFailureBehavior.Defer, resolution.Behavior);
        Assert.True(resolution.Decision.IsDeferred);
        Assert.Contains(DlpFailureReasonCodes.ClassifiedResult, resolution.Decision.ReasonCodes);
        Assert.Equal("unit-test", resolution.Reason.Metadata["classification.source"]);
    }

    /// <summary>
    /// Verifies that the DlpFailurePolicyResolution.Create method correctly maps every DlpFailureBehavior to the expected governance decision and associated properties.
    /// </summary>
    /// <param name="behavior">The DlpFailureBehavior to test.</param>
    /// <exception cref="InvalidOperationException">Thrown when an invalid DlpFailureBehavior is provided.</exception>
    [Theory]
    [InlineData(DlpFailureBehavior.Allow)]
    [InlineData(DlpFailureBehavior.WarnAndAllow)]
    [InlineData(DlpFailureBehavior.Deny)]
    [InlineData(DlpFailureBehavior.Defer)]
    [InlineData(DlpFailureBehavior.RequireAcknowledgment)]
    [InlineData(DlpFailureBehavior.Escalate)]
    public void CreateResolutionMapsEveryBehaviorToExpectedGovernanceDecision(DlpFailureBehavior behavior)
    {
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            DlpIntentRiskLevel.Low,
            correlationId: "correlation-250",
            traceId: "trace-250",
            policyVersion: "policy-v250",
            policyHash: "hash-250");

        var resolution = DlpFailurePolicyResolution.Create(context, behavior);

        Assert.Same(context, resolution.Context);
        Assert.Equal(behavior, resolution.Behavior);
        Assert.Equal(DlpFailureReasonCodes.ServiceUnavailable, resolution.Reason.Code);
        Assert.Equal("service_unavailable", resolution.Reason.Metadata["dlp.failure_kind"]);
        Assert.Equal("low", resolution.Reason.Metadata["dlp.risk_level"]);
        Assert.Equal(ToMetadataValue(behavior), resolution.Reason.Metadata["dlp.behavior"]);
        Assert.Equal("correlation-250", resolution.Decision.CorrelationId);
        Assert.Equal("trace-250", resolution.Decision.TraceId);
        Assert.Equal("policy-v250", resolution.Decision.PolicyVersion);
        Assert.Equal("hash-250", resolution.Decision.PolicyHash);

        switch (behavior)
        {
            case DlpFailureBehavior.Allow:
                Assert.True(resolution.Decision.IsAllowed);
                Assert.True(resolution.CanProceed);
                Assert.True(resolution.IsFailOpen);
                Assert.False(resolution.IsFailClosed);
                Assert.False(resolution.Decision.HasReasons);
                Assert.Empty(resolution.Decision.ReasonCodes);
                break;
            case DlpFailureBehavior.WarnAndAllow:
                Assert.True(resolution.Decision.IsWarning);
                Assert.True(resolution.CanProceed);
                Assert.True(resolution.IsFailOpen);
                Assert.False(resolution.IsFailClosed);
                AssertDecisionHasSingleReason(resolution);
                break;
            case DlpFailureBehavior.Deny:
                Assert.True(resolution.Decision.IsDenied);
                Assert.False(resolution.CanProceed);
                Assert.False(resolution.IsFailOpen);
                Assert.True(resolution.IsFailClosed);
                AssertDecisionHasSingleReason(resolution);
                break;
            case DlpFailureBehavior.Defer:
                Assert.True(resolution.Decision.IsDeferred);
                Assert.False(resolution.CanProceed);
                Assert.False(resolution.IsFailOpen);
                Assert.False(resolution.IsFailClosed);
                AssertDecisionHasSingleReason(resolution);
                break;
            case DlpFailureBehavior.RequireAcknowledgment:
                Assert.True(resolution.Decision.RequiresAcknowledgment);
                Assert.False(resolution.CanProceed);
                Assert.False(resolution.IsFailOpen);
                Assert.False(resolution.IsFailClosed);
                AssertDecisionHasSingleReason(resolution);
                break;
            case DlpFailureBehavior.Escalate:
                Assert.True(resolution.Decision.EscalationRecommended);
                Assert.False(resolution.CanProceed);
                Assert.False(resolution.IsFailOpen);
                Assert.False(resolution.IsFailClosed);
                AssertDecisionHasSingleReason(resolution);
                break;
            case DlpFailureBehavior.Unspecified:
            default:
                throw new InvalidOperationException("Unexpected DLP failure behavior under test.");
        }
    }

    /// <summary>
    /// Verifies that the reason codes and messages for each DlpClassificationFailureKind remain stable and consistent with expected values.
    /// </summary>
    /// <param name="failureKind">The type of DLP classification failure.</param>
    /// <param name="expectedReasonCode">The expected reason code.</param>
    /// <param name="expectedMessage">The expected reason message.</param>
    [Theory]
    [InlineData(DlpClassificationFailureKind.ServiceUnavailable, DlpFailureReasonCodes.ServiceUnavailable, "DLP/classification screening service was unavailable.")]
    [InlineData(DlpClassificationFailureKind.IndeterminateResult, DlpFailureReasonCodes.IndeterminateResult, "DLP/classification screening returned an indeterminate result.")]
    [InlineData(DlpClassificationFailureKind.BlockedResult, DlpFailureReasonCodes.BlockedResult, "DLP/classification screening returned a blocked result.")]
    [InlineData(DlpClassificationFailureKind.ClassifiedResult, DlpFailureReasonCodes.ClassifiedResult, "DLP/classification screening returned a classified result that requires configured policy handling.")]
    public void FailureKindMessagesRemainStable(
        DlpClassificationFailureKind failureKind,
        string expectedReasonCode,
        string expectedMessage)
    {
        var context = DlpFailurePolicyContext.Create(
            failureKind,
            DlpIntentRiskLevel.Low);

        var resolution = DlpFailurePolicyResolution.Create(
            context,
            DlpFailureBehavior.WarnAndAllow);

        Assert.Equal(expectedReasonCode, resolution.Reason.Code);
        Assert.Equal(expectedMessage, resolution.Reason.Message);
        Assert.Contains(expectedReasonCode, resolution.Decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that when a timeout failure occurs without a specified timeout value, the reason message is generic and the timeout metadata is omitted.
    /// </summary>
    [Fact]
    public void TimeoutWithoutTimeoutValueUsesGenericMessageAndOmitsTimeoutMetadata()
    {
        var resolution = DlpFailurePolicyResolution.Create(
            DlpFailurePolicyContext.TimeoutFailure(DlpIntentRiskLevel.Low),
            DlpFailureBehavior.WarnAndAllow);

        Assert.Equal(DlpFailureReasonCodes.Timeout, resolution.Reason.Code);
        Assert.Equal("DLP/classification screening timed out.", resolution.Reason.Message);
        Assert.False(resolution.Reason.Metadata.ContainsKey("dlp.timeout_ms"));
    }

    /// <summary>
    /// Verifies that the reason codes for each DlpClassificationFailureKind are correctly mapped to the expected reason codes, ensuring that API failures and classification failures are distinguished appropriately.
    /// </summary>
    /// <param name="failureKind">The type of DLP classification failure.</param>
    /// <param name="expectedReasonCode">The expected reason code.</param>
    /// <returns>
    /// A task that represents the asynchronous operation of resolving the DLP failure policy and asserting the expected reason codes.
    /// </returns>
    [Theory]
    [InlineData(DlpClassificationFailureKind.ServiceUnavailable, DlpFailureReasonCodes.ServiceUnavailable)]
    [InlineData(DlpClassificationFailureKind.Timeout, DlpFailureReasonCodes.Timeout)]
    [InlineData(DlpClassificationFailureKind.IndeterminateResult, DlpFailureReasonCodes.IndeterminateResult)]
    [InlineData(DlpClassificationFailureKind.BlockedResult, DlpFailureReasonCodes.BlockedResult)]
    [InlineData(DlpClassificationFailureKind.ClassifiedResult, DlpFailureReasonCodes.ClassifiedResult)]
    public async Task ReasonCodesDistinguishApiFailuresFromClassificationFailures(
        DlpClassificationFailureKind failureKind,
        string expectedReasonCode)
    {
        var resolver = new DefaultDlpFailurePolicyResolver(
            new DlpFailurePolicyOptions
            {
                LowRiskBehavior = DlpFailureBehavior.WarnAndAllow
            });
        var context = DlpFailurePolicyContext.Create(
            failureKind,
            DlpIntentRiskLevel.Low);

        DlpFailurePolicyResolution resolution = await resolver.ResolveAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedReasonCode, resolution.Reason.Code);
        Assert.Contains(expectedReasonCode, resolution.Decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that providing an invalid DlpIntentRiskLevel value to the DlpFailurePolicyContext.Create method throws an ArgumentOutOfRangeException, ensuring that only valid risk levels are accepted.
    /// </summary>
    [Fact]
    public void InvalidRiskLevelThrowsArgumentOutOfRangeException()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DlpFailurePolicyContext.Create(
                DlpClassificationFailureKind.ServiceUnavailable,
                (DlpIntentRiskLevel)999));
    }

    /// <summary>
    /// Verifies that the default DlpIntentRiskLevel value is Unspecified rather than a usable risk tier, so that an unset
    /// field cannot resolve to the most permissive policy.
    /// </summary>
    [Fact]
    public void DefaultRiskLevelIsUnspecifiedRatherThanLow()
    {
        Assert.Equal(DlpIntentRiskLevel.Unspecified, default);
        Assert.NotEqual(DlpIntentRiskLevel.Low, default);
    }

    /// <summary>
    /// Verifies that an unspecified DlpIntentRiskLevel is rejected by DlpFailurePolicyContext.Create instead of being
    /// treated as the lowest risk tier, so an omitted risk level fails closed at the boundary.
    /// </summary>
    [Fact]
    public void UnspecifiedRiskLevelThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DlpFailurePolicyContext.Create(
                DlpClassificationFailureKind.ServiceUnavailable,
                DlpIntentRiskLevel.Unspecified));

        Assert.Equal("riskLevel", exception.ParamName);
        Assert.Contains("must be assigned", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the default DlpFailureBehavior value is Unspecified rather than Allow, so that an unset behavior
    /// cannot silently permit an operation after a screening failure.
    /// </summary>
    [Fact]
    public void DefaultFailureBehaviorIsUnspecifiedRatherThanAllow()
    {
        Assert.Equal(DlpFailureBehavior.Unspecified, default);
        Assert.NotEqual(DlpFailureBehavior.Allow, default);
    }

    /// <summary>
    /// Verifies that an unspecified DlpFailureBehavior configured on a risk tier is rejected during resolution rather than
    /// resolving the screening failure into an allow.
    /// </summary>
    [Theory]
    [InlineData(DlpIntentRiskLevel.Low)]
    [InlineData(DlpIntentRiskLevel.Medium)]
    [InlineData(DlpIntentRiskLevel.High)]
    public async Task UnspecifiedTierBehaviorThrowsArgumentOutOfRangeException(DlpIntentRiskLevel riskLevel)
    {
        var options = new DlpFailurePolicyOptions();

        switch (riskLevel)
        {
            case DlpIntentRiskLevel.Low:
                options.LowRiskBehavior = DlpFailureBehavior.Unspecified;
                break;
            case DlpIntentRiskLevel.Medium:
                options.MediumRiskBehavior = DlpFailureBehavior.Unspecified;
                break;
            case DlpIntentRiskLevel.High:
                options.HighRiskBehavior = DlpFailureBehavior.Unspecified;
                break;
            case DlpIntentRiskLevel.Unspecified:
            default:
                throw new InvalidOperationException("Unexpected DLP risk level under test.");
        }

        var resolver = new DefaultDlpFailurePolicyResolver(options);
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            riskLevel);

        ArgumentOutOfRangeException exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await resolver.ResolveAsync(context, TestContext.Current.CancellationToken));

        Assert.Contains("must be configured", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that an unspecified DlpFailureBehavior supplied through BehaviorOverrides is rejected during resolution.
    /// </summary>
    [Fact]
    public async Task UnspecifiedOverrideBehaviorThrowsArgumentOutOfRangeException()
    {
        var options = new DlpFailurePolicyOptions();
        options.BehaviorOverrides[new DlpFailurePolicyKey(
            DlpIntentRiskLevel.High,
            DlpClassificationFailureKind.ServiceUnavailable)] = DlpFailureBehavior.Unspecified;

        var resolver = new DefaultDlpFailurePolicyResolver(options);
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            DlpIntentRiskLevel.High);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await resolver.ResolveAsync(context, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that DlpFailurePolicyResolution.Create rejects an unspecified behavior rather than materializing a
    /// governance decision a host would act on.
    /// </summary>
    [Fact]
    public void UnspecifiedBehaviorIsRejectedByResolutionCreate()
    {
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            DlpIntentRiskLevel.High);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DlpFailurePolicyResolution.Create(context, DlpFailureBehavior.Unspecified));

        Assert.Equal("behavior", exception.ParamName);
        Assert.Contains("must be resolved", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that providing an invalid DlpClassificationFailureKind value to the DlpFailurePolicyContext.Create method or the DlpFailureReasonCodes.GetFor method throws an ArgumentOutOfRangeException, ensuring that only valid failure kinds are accepted.
    /// </summary>
    [Fact]
    public void InvalidFailureKindThrowsArgumentOutOfRangeException()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DlpFailurePolicyContext.Create(
                (DlpClassificationFailureKind)999,
                DlpIntentRiskLevel.Low));

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DlpFailureReasonCodes.GetFor((DlpClassificationFailureKind)999));
    }

    /// <summary>
    /// Verifies that providing an invalid DlpFailureBehavior value to the DlpFailurePolicyOptions or the DlpFailurePolicyContext.Create method throws an ArgumentOutOfRangeException, ensuring that only valid behaviors are accepted.
    /// </summary>
    [Theory]
    [InlineData(DlpIntentRiskLevel.Low)]
    [InlineData(DlpIntentRiskLevel.Medium)]
    [InlineData(DlpIntentRiskLevel.High)]
    public async Task InvalidDefaultBehaviorThrowsArgumentOutOfRangeException(DlpIntentRiskLevel riskLevel)
    {
        var options = new DlpFailurePolicyOptions();

        switch (riskLevel)
        {
            case DlpIntentRiskLevel.Low:
                options.LowRiskBehavior = (DlpFailureBehavior)999;
                break;
            case DlpIntentRiskLevel.Medium:
                options.MediumRiskBehavior = (DlpFailureBehavior)999;
                break;
            case DlpIntentRiskLevel.High:
                options.HighRiskBehavior = (DlpFailureBehavior)999;
                break;
            case DlpIntentRiskLevel.Unspecified:
            default:
                throw new InvalidOperationException("Unexpected DLP risk level under test.");
        }

        var resolver = new DefaultDlpFailurePolicyResolver(options);
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            riskLevel);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await resolver.ResolveAsync(context, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that providing an invalid DlpFailureBehavior value in the BehaviorOverrides dictionary of DlpFailurePolicyOptions throws an ArgumentOutOfRangeException when resolving a policy, ensuring that only valid behaviors are accepted.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task InvalidOverrideBehaviorThrowsArgumentOutOfRangeException()
    {
        var options = new DlpFailurePolicyOptions();
        options.BehaviorOverrides[new DlpFailurePolicyKey(
            DlpIntentRiskLevel.Low,
            DlpClassificationFailureKind.ServiceUnavailable)] = (DlpFailureBehavior)999;

        var resolver = new DefaultDlpFailurePolicyResolver(options);
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            DlpIntentRiskLevel.Low);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await resolver.ResolveAsync(context, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that providing an invalid DlpFailureBehavior value to the DlpFailurePolicyResolution.Create method throws an ArgumentOutOfRangeException, ensuring that only valid behaviors are accepted.
    /// </summary>
    [Fact]
    public void InvalidResolutionBehaviorThrowsArgumentOutOfRangeException()
    {
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            DlpIntentRiskLevel.Low);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DlpFailurePolicyResolution.Create(context, (DlpFailureBehavior)999));
    }

    /// <summary>
    /// Verifies that passing a null DlpFailurePolicyContext to the resolver or resolution creation methods throws an ArgumentNullException, ensuring that context is required for policy resolution.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task NullContextThrowsArgumentNullException()
    {
        var resolver = new DefaultDlpFailurePolicyResolver();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await resolver.ResolveAsync(null!, TestContext.Current.CancellationToken));

        _ = Assert.Throws<ArgumentNullException>(() =>
            DlpFailurePolicyResolution.Create(null!, DlpFailureBehavior.Allow));

        var options = new DlpFailurePolicyOptions();
        _ = Assert.Throws<ArgumentNullException>(() => options.GetBehavior(null!));
    }

    /// <summary>
    /// Verifies that if the cancellation token is canceled before the resolution process begins, the resolver throws an OperationCanceledException, ensuring that cancellation is honored promptly.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ResolveHonorsCancellationBeforeResolution()
    {
        var resolver = new DefaultDlpFailurePolicyResolver();
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            DlpIntentRiskLevel.Low);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await resolver.ResolveAsync(context, cancellationTokenSource.Token));
    }

    /// <summary>
    /// Verifies that when the metadata dictionary is null or empty, the DlpFailurePolicyContext and DlpFailurePolicyResolution handle it safely without throwing exceptions, and that the metadata remains empty in the resolution.
    /// </summary>
    [Fact]
    public void NullAndEmptyMetadataRemainSafe()
    {
        var nullMetadataContext = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            DlpIntentRiskLevel.Low,
            metadata: null);
        var emptyMetadataContext = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            DlpIntentRiskLevel.Low,
            metadata: new Dictionary<string, string>());

        var nullMetadataResolution = DlpFailurePolicyResolution.Create(
            nullMetadataContext,
            DlpFailureBehavior.WarnAndAllow);
        var emptyMetadataResolution = DlpFailurePolicyResolution.Create(
            emptyMetadataContext,
            DlpFailureBehavior.WarnAndAllow);

        Assert.False(nullMetadataContext.HasMetadata);
        Assert.False(emptyMetadataContext.HasMetadata);
        Assert.False(nullMetadataResolution.Reason.Metadata.ContainsKey(string.Empty));
        Assert.False(emptyMetadataResolution.Reason.Metadata.ContainsKey(string.Empty));
        Assert.Equal("service_unavailable", nullMetadataResolution.Reason.Metadata["dlp.failure_kind"]);
        Assert.Equal("service_unavailable", emptyMetadataResolution.Reason.Metadata["dlp.failure_kind"]);
    }

    /// <summary>
    /// Verifies that the metadata normalization process correctly ignores blank keys and trims whitespace from both keys and values, ensuring that the resulting metadata dictionary is clean and usable.
    /// </summary>
    [Fact]
    public void MetadataNormalizationIgnoresBlankKeysAndTrimsKeysAndValues()
    {
        var context = DlpFailurePolicyContext.Create(
            DlpClassificationFailureKind.ServiceUnavailable,
            DlpIntentRiskLevel.Low,
            intentCategory: " ",
            environment: " ",
            metadata: new Dictionary<string, string>
            {
                [" "] = "ignored",
                [" host.key "] = " value ",
                ["empty-value"] = null!
            });

        var resolution = DlpFailurePolicyResolution.Create(
            context,
            DlpFailureBehavior.WarnAndAllow);

        Assert.True(context.HasMetadata);
        Assert.Equal("value", context.Metadata["host.key"]);
        Assert.Equal(string.Empty, context.Metadata["empty-value"]);
        Assert.False(context.Metadata.ContainsKey(string.Empty));
        Assert.Equal("value", resolution.Reason.Metadata["host.key"]);
        Assert.Equal(string.Empty, resolution.Reason.Metadata["empty-value"]);
        Assert.False(resolution.Reason.Metadata.ContainsKey(string.Empty));
        Assert.False(resolution.Reason.Metadata.ContainsKey("dlp.intent_category"));
        Assert.False(resolution.Reason.Metadata.ContainsKey("dlp.environment"));
    }

    /// <summary>
    /// Verifies that the intent category and environment metadata are included in the reason metadata only when they are non-blank, ensuring that empty or whitespace-only values do not appear in the metadata dictionary.
    /// </summary>
    [Fact]
    public void IntentCategoryAndEnvironmentAreIncludedOnlyWhenNonblank()
    {
        var blankResolution = DlpFailurePolicyResolution.Create(
            DlpFailurePolicyContext.Create(
                DlpClassificationFailureKind.ServiceUnavailable,
                DlpIntentRiskLevel.Low,
                intentCategory: " ",
                environment: " "),
            DlpFailureBehavior.WarnAndAllow);
        var nonblankResolution = DlpFailurePolicyResolution.Create(
            DlpFailurePolicyContext.Create(
                DlpClassificationFailureKind.ServiceUnavailable,
                DlpIntentRiskLevel.Low,
                intentCategory: " external-api ",
                environment: " Production "),
            DlpFailureBehavior.WarnAndAllow);

        Assert.False(blankResolution.Reason.Metadata.ContainsKey("dlp.intent_category"));
        Assert.False(blankResolution.Reason.Metadata.ContainsKey("dlp.environment"));
        Assert.Equal("external-api", nonblankResolution.Reason.Metadata["dlp.intent_category"]);
        Assert.Equal("Production", nonblankResolution.Reason.Metadata["dlp.environment"]);
    }

    private static void AssertDecisionHasSingleReason(DlpFailurePolicyResolution resolution)
    {
        Assert.True(resolution.Decision.HasReasons);
        Assert.Equal(resolution.Reason.Code, Assert.Single(resolution.Decision.ReasonCodes));
        Assert.Same(resolution.Decision.Reasons[0], Assert.Single(resolution.Decision.Reasons));
        Assert.Equal(resolution.Reason.Code, resolution.Decision.Reasons[0].Code);
        Assert.Equal(resolution.Reason.Message, resolution.Decision.Reasons[0].Message);
    }

    private static string ToMetadataValue(DlpFailureBehavior behavior)
    {
        string text = behavior.ToString();

        return string.Concat(
            text.Select((character, index) =>
                index > 0 && char.IsUpper(character)
                    ? "_" + char.ToLowerInvariant(character)
                    : char.ToLowerInvariant(character).ToString()));
    }
}
