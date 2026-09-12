using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Branch-focused unit tests for <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}"/>.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorTests
{
    /// <summary>
    /// Verifies that the constructor throws an <see cref="ArgumentNullException"/> when the constraints argument is null.
    /// </summary>
    [Fact]
    public void ConstructorThrowsForNullConstraints()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(null!));
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method throws an <see cref="ArgumentNullException"/> when the context argument is null.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateThrowsForNullContext()
    {
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>([]);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await evaluator.EvaluateAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method produces a denied decision when there are no constraints and the default options are used.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateWithNoConstraintsProducesDeniedDecisionByDefault()
    {
        TestPolicyContext context = CreateContext();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>([]);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            AsiBackbonePolicyEvaluatorOptions.DefaultNoConstraintsReasonCode,
            Assert.Single(decision.ReasonCodes));
        Assert.Equal(context.CorrelationId, decision.CorrelationId);
        Assert.Equal(context.PolicyVersion, decision.PolicyVersion);
        Assert.Equal(context.PolicyHash, decision.PolicyHash);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method logs a warning when there are no constraints and permissive empty-policy behavior is explicitly enabled.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateWithNoConstraintsAndPermissiveOptionLogsWarning()
    {
        TestPolicyContext context = CreateContext();
        var logger = new CapturingLogger<DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>>();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            constraints: [],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                DenyWhenNoConstraints = false
            },
            logger: logger);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        CapturedLogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.LogLevel);
        Assert.Equal(4110, entry.EventId.Id);
        Assert.Equal("EmptyPolicyAllowedWarning", entry.EventId.Name);
        Assert.Contains("zero constraints", entry.Message, StringComparison.Ordinal);
        Assert.Contains(context.CorrelationId!, entry.Message, StringComparison.Ordinal);
        Assert.Contains(context.PolicyVersion!, entry.Message, StringComparison.Ordinal);
        Assert.Contains(context.PolicyHash!, entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method does not log a warning when there are no constraints and the strict option is used.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateWithNoConstraintsAndStrictOptionDoesNotLogWarning()
    {
        TestPolicyContext context = CreateContext();
        var logger = new CapturingLogger<DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>>();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            constraints: [],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                DenyWhenNoConstraints = true
            },
            logger: logger);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Empty(logger.Entries);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method produces a denied decision when there are no constraints and the strict option is used.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateWithNoConstraintsAndStrictOptionProducesDeniedDecision()
    {
        TestPolicyContext context = CreateContext();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                DenyWhenNoConstraints = true
            });

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            AsiBackbonePolicyEvaluatorOptions.DefaultNoConstraintsReasonCode,
            Assert.Single(decision.ReasonCodes));
        Assert.Equal(context.CorrelationId, decision.CorrelationId);
        Assert.Equal(context.PolicyVersion, decision.PolicyVersion);
        Assert.Equal(context.PolicyHash, decision.PolicyHash);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method produces a denied decision with the configured reason code and message when there are no constraints and the strict option is used.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateWithNoConstraintsAndStrictOptionUsesConfiguredReason()
    {
        TestPolicyContext context = CreateContext();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                DenyWhenNoConstraints = true,
                NoConstraintsReasonCode = "host.policy.empty",
                NoConstraintsReasonMessage = "Host policy load produced no constraints."
            });

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal("host.policy.empty", Assert.Single(decision.ReasonCodes));
        Assert.Equal("Host policy load produced no constraints.", Assert.Single(decision.Reasons).Message);
    }

    /// <summary>
    /// Verifies that the constructor throws an <see cref="InvalidOperationException"/> when the strict option is used and the configured no-constraints reason code is null, empty, or whitespace.
    /// </summary>
    /// <param name="reasonCode">The reason code to test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConstructorThrowsForInvalidNoConstraintsReasonCode(string? reasonCode)
    {
        _ = Assert.Throws<InvalidOperationException>(() =>
            new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
                [],
                decisionPolicy: null,
                options: new AsiBackbonePolicyEvaluatorOptions
                {
                    DenyWhenNoConstraints = true,
                    NoConstraintsReasonCode = reasonCode!
                }));
    }

    /// <summary>
    /// Verifies that the constructor throws an <see cref="InvalidOperationException"/> when the strict option is used and the configured no-constraints reason message is null, empty, or whitespace.
    /// </summary>
    /// <param name="reasonMessage">The reason message to test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConstructorThrowsForInvalidNoConstraintsReasonMessage(string? reasonMessage)
    {
        _ = Assert.Throws<InvalidOperationException>(() =>
            new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
                [],
                decisionPolicy: null,
                options: new AsiBackbonePolicyEvaluatorOptions
                {
                    DenyWhenNoConstraints = true,
                    NoConstraintsReasonMessage = reasonMessage!
                }));
    }

    /// <summary>
    /// Verifies that the constructor throws an <see cref="InvalidOperationException"/> when the constraint exception option is used and the configured constraint exception reason code is null, empty, or whitespace.
    /// </summary>
    /// <param name="reasonCode">The reason code to test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConstructorThrowsForInvalidConstraintExceptionReasonCode(string? reasonCode)
    {
        _ = Assert.Throws<InvalidOperationException>(() =>
            new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
                [],
                decisionPolicy: null,
                options: new AsiBackbonePolicyEvaluatorOptions
                {
                    TreatConstraintExceptionAsDenial = true,
                    ConstraintExceptionReasonCode = reasonCode!
                }));
    }

    /// <summary>
    /// Verifies that the constructor throws an <see cref="InvalidOperationException"/> when the constraint exception option is used and the configured constraint exception reason message is null, empty, or whitespace.
    /// </summary>
    /// <param name="reasonMessage">The reason message to test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConstructorThrowsForInvalidConstraintExceptionReasonMessage(string? reasonMessage)
    {
        _ = Assert.Throws<InvalidOperationException>(() =>
            new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
                [],
                decisionPolicy: null,
                options: new AsiBackbonePolicyEvaluatorOptions
                {
                    TreatConstraintExceptionAsDenial = true,
                    ConstraintExceptionReasonMessage = reasonMessage!
                }));
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method produces a warning decision when there is a warning constraint and the strict option is used.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateWithStrictOptionAndWarningsStillProducesWarningDecision()
    {
        TestPolicyContext context = CreateContext();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.warning",
                        "The constraint produced a warning."))
            ],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                DenyWhenNoConstraints = true
            });

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsWarning);
        Assert.True(decision.CanProceed);
        Assert.Contains("constraint.warning", decision.ReasonCodes);
        Assert.DoesNotContain(AsiBackbonePolicyEvaluatorOptions.DefaultNoConstraintsReasonCode, decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method produces a denied decision when there is a denial constraint and the strict option is used.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateWithStrictOptionAndDenialsStillProducesConstraintDeniedDecision()
    {
        TestPolicyContext context = CreateContext();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Deny(
                        "constraint.denied",
                        "The constraint denied the operation."))
            ],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                DenyWhenNoConstraints = true
            });

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Equal("constraint.denied", Assert.Single(decision.ReasonCodes));
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method applies the decision policy with an empty constraint results list when there are no constraints and the strict option is used.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateWithNoConstraintsAndStrictOptionAppliesDecisionPolicyWithEmptyResults()
    {
        TestPolicyContext context = CreateContext();
        var policy = new CapturingDecisionPolicy();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [],
            policy,
            new AsiBackbonePolicyEvaluatorOptions
            {
                DenyWhenNoConstraints = true
            });

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDeferred);
        Assert.Equal("policy.deferred", Assert.Single(decision.ReasonCodes));
        Assert.Equal(1, policy.ApplyCount);
        Assert.Same(context, policy.Context);
        Assert.NotNull(policy.ComposedDecision);
        Assert.True(policy.ComposedDecision.IsDenied);
        Assert.Equal(
            AsiBackbonePolicyEvaluatorOptions.DefaultNoConstraintsReasonCode,
            Assert.Single(policy.ComposedDecision.ReasonCodes));
        Assert.NotNull(policy.ConstraintResults);
        Assert.Empty(policy.ConstraintResults);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method produces a denied governance decision for constraint exceptions by default.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateWithConstraintExceptionProducesDeniedDecisionByDefault()
    {
        TestPolicyContext context = CreateContext();
        var expectedException = new InvalidOperationException("Sensitive exception details should stay with the host.");
        var logger = new CapturingLogger<DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>>();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            constraints: [new ThrowingConstraint(expectedException)],
            decisionPolicy: null,
            options: null,
            logger: logger);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            AsiBackbonePolicyEvaluatorOptions.DefaultConstraintExceptionReasonCode,
            Assert.Single(decision.ReasonCodes));
        Assert.DoesNotContain("Sensitive", Assert.Single(decision.Reasons).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(context.CorrelationId, decision.CorrelationId);
        Assert.Equal(context.PolicyVersion, decision.PolicyVersion);
        Assert.Equal(context.PolicyHash, decision.PolicyHash);
        Assert.Same(expectedException, Assert.Single(logger.Entries).Exception);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method propagates constraint exceptions when fail-closed conversion is explicitly disabled.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateWithConstraintExceptionPropagatesWhenFailClosedDisabled()
    {
        TestPolicyContext context = CreateContext();
        var expectedException = new InvalidOperationException("Sensitive exception details should stay with the host.");

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new ThrowingConstraint(expectedException)],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                TreatConstraintExceptionAsDenial = false
            });

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken));

        Assert.Same(expectedException, exception);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method produces a denied decision when there is a constraint exception and the option to treat constraint exceptions as denials is enabled.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateWithConstraintExceptionOptionProducesDeniedDecision()
    {
        TestPolicyContext context = CreateContext();
        var expectedException = new InvalidOperationException("sensitive database connection string details");
        var logger = new CapturingLogger<DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>>();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            constraints: [new ThrowingConstraint(expectedException)],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                TreatConstraintExceptionAsDenial = true
            },
            logger: logger);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            AsiBackbonePolicyEvaluatorOptions.DefaultConstraintExceptionReasonCode,
            Assert.Single(decision.ReasonCodes));
        Assert.Equal(
            "A policy constraint failed during evaluation. The operation was denied by the evaluator failure policy.",
            Assert.Single(decision.Reasons).Message);
        Assert.DoesNotContain("connection string", Assert.Single(decision.Reasons).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(context.CorrelationId, decision.CorrelationId);
        Assert.Equal(context.PolicyVersion, decision.PolicyVersion);
        Assert.Equal(context.PolicyHash, decision.PolicyHash);

        CapturedLogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.LogLevel);
        Assert.Equal(4120, entry.EventId.Id);
        Assert.Equal("ConstraintExceptionDeniedError", entry.EventId.Name);
        Assert.Contains("throwing-constraint", entry.Message, StringComparison.Ordinal);
        Assert.Contains(context.CorrelationId!, entry.Message, StringComparison.Ordinal);
        Assert.Same(expectedException, entry.Exception);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method logs a warning when there is a constraint exception with an unnamed constraint and the option to treat constraint exceptions as denials is enabled.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateWithConstraintExceptionOptionLogsUnnamedConstraint()
    {
        TestPolicyContext context = CreateContext();
        var logger = new CapturingLogger<DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>>();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            constraints: [new ThrowingConstraint(new InvalidOperationException("failure"), " ")],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                TreatConstraintExceptionAsDenial = true
            },
            logger: logger);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        CapturedLogEntry entry = Assert.Single(logger.Entries);
        Assert.Contains("<unnamed>", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method produces a denied decision with the configured reason code and message when there is a constraint exception and the option to treat constraint exceptions as denials is enabled.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateWithConstraintExceptionOptionUsesConfiguredReason()
    {
        TestPolicyContext context = CreateContext();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new ThrowingConstraint(new InvalidOperationException("sensitive failure text"))],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                TreatConstraintExceptionAsDenial = true,
                ConstraintExceptionReasonCode = "host.constraint.exception",
                ConstraintExceptionReasonMessage = "The host policy constraint failed closed."
            });

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal("host.constraint.exception", Assert.Single(decision.ReasonCodes));
        Assert.Equal("The host policy constraint failed closed.", Assert.Single(decision.Reasons).Message);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method applies the decision policy with a synthetic denied result when there is a constraint exception and the option to treat constraint exceptions as denials is enabled.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateWithConstraintExceptionOptionAppliesDecisionPolicyWithSyntheticDeniedResult()
    {
        TestPolicyContext context = CreateContext();
        var policy = new CapturingDecisionPolicy();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(ConstraintEvaluationResult.Allow()),
                new ThrowingConstraint(new InvalidOperationException("sensitive failure text"))
            ],
            policy,
            new AsiBackbonePolicyEvaluatorOptions
            {
                TreatConstraintExceptionAsDenial = true
            });

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDeferred);
        Assert.Equal(1, policy.ApplyCount);
        Assert.NotNull(policy.ComposedDecision);
        Assert.True(policy.ComposedDecision.IsDenied);
        Assert.Equal(
            AsiBackbonePolicyEvaluatorOptions.DefaultConstraintExceptionReasonCode,
            Assert.Single(policy.ComposedDecision.ReasonCodes));
        Assert.NotNull(policy.ConstraintResults);
        Assert.Equal(2, policy.ConstraintResults.Count);
        Assert.True(policy.ConstraintResults[1].IsDenied);
        Assert.Equal(
            AsiBackbonePolicyEvaluatorOptions.DefaultConstraintExceptionReasonCode,
            Assert.Single(policy.ConstraintResults[1].ReasonCodes));
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method propagates <see cref="OperationCanceledException"/> even when the option to treat constraint exceptions as denials is enabled.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateWithConstraintExceptionOptionStillPropagatesOperationCanceledException()
    {
        TestPolicyContext context = CreateContext();
        var expectedException = new OperationCanceledException("Host cancellation should not be converted to denial.");

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new ThrowingConstraint(expectedException)],
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                TreatConstraintExceptionAsDenial = true
            });

        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken));

        Assert.Same(expectedException, exception);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}"/> constructor materializes a non-list enumerable of constraints into an internal list.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ConstructorMaterializesNonListConstraintEnumerable()
    {
        TestPolicyContext context = CreateContext();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            CreateConstraintEnumerable(
                ConstraintEvaluationResult.Warning(
                    "constraint.warning",
                    "The constraint produced a warning.")));

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsWarning);
        Assert.True(decision.CanProceed);
        Assert.Contains("constraint.warning", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}"/> constructor defensively copies the caller-owned list of constraints so that subsequent mutations to the caller's list do not affect the evaluator's behavior.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ConstructorDefensivelyCopiesCallerOwnedConstraintList()
    {
        TestPolicyContext context = CreateContext();
        var callerOwnedConstraints = new List<IAsiBackboneConstraint<TestPolicyContext>>
        {
            new StaticConstraint(
                ConstraintEvaluationResult.Warning(
                    "constraint.initial_warning",
                    "The original constraint produced a warning."))
        };

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(callerOwnedConstraints);

        callerOwnedConstraints.Clear();
        callerOwnedConstraints.Add(
            new StaticConstraint(
                ConstraintEvaluationResult.Deny(
                    "constraint.later_denied",
                    "A caller mutation should not affect the evaluator.")));

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsWarning);
        Assert.True(decision.CanProceed);
        Assert.Contains("constraint.initial_warning", decision.ReasonCodes);
        Assert.DoesNotContain("constraint.later_denied", decision.ReasonCodes);
    }

    private static readonly string[] Expected = ["first", "second"];

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method runs constraints in the order they were supplied after materializing an array of constraints.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateRunsConstraintsInSuppliedOrderAfterArrayMaterialization()
    {
        TestPolicyContext context = CreateContext();
        var observedOrder = new List<string>();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new DelegateConstraint(
                    (_, _) =>
                    {
                        observedOrder.Add("first");
                        return ConstraintEvaluationResult.Allow();
                    }),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        observedOrder.Add("second");
                        return ConstraintEvaluationResult.Warning(
                            "constraint.warning",
                            "The second constraint produced a warning.");
                    })
            ]);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(Expected, observedOrder);
        Assert.True(decision.IsWarning);
        Assert.True(decision.CanProceed);
        Assert.Contains("constraint.warning", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method honors cancellation before any constraints are evaluated.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateHonorsCancellationBeforeConstraintRuns()
    {
        TestPolicyContext context = CreateContext();
        int evaluationCount = 0;

        var constraint = new DelegateConstraint(
            (_, _) =>
            {
                evaluationCount++;
                return ConstraintEvaluationResult.Allow();
            });

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>([constraint]);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await evaluator.EvaluateAsync(context, cancellationTokenSource.Token));

        Assert.Equal(0, evaluationCount);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method produces a denied decision when there is a warning constraint followed by a denial constraint, and the denial takes precedence over the warning.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateComposesDeniedDecisionBeforeWarnings()
    {
        TestPolicyContext context = CreateContext();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.warning",
                        "The constraint produced a warning.")),
                new StaticConstraint(
                    ConstraintEvaluationResult.Deny(
                        "constraint.denied",
                        "The constraint denied the operation."))
            ]);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Contains("constraint.denied", decision.ReasonCodes);
        Assert.DoesNotContain("constraint.warning", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}.EvaluateAsync"/> method applies the decision policy with a read-only list of constraint results.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateAppliesDecisionPolicyWithReadOnlyConstraintResults()
    {
        TestPolicyContext context = CreateContext();
        using var cancellationTokenSource = new CancellationTokenSource();

        var policy = new CapturingDecisionPolicy();

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(ConstraintEvaluationResult.Allow()),
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.warning",
                        "The constraint produced a warning."))
            ],
            policy);

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            context,
            cancellationTokenSource.Token);

        Assert.True(decision.IsDeferred);
        Assert.Equal("policy.deferred", Assert.Single(decision.ReasonCodes));

        Assert.Equal(1, policy.ApplyCount);
        Assert.Same(context, policy.Context);
        Assert.NotNull(policy.ComposedDecision);
        Assert.True(policy.ComposedDecision.IsWarning);
        Assert.Equal(cancellationTokenSource.Token, policy.CancellationToken);

        IReadOnlyList<ConstraintEvaluationResult> constraintResults =
            Assert.IsType<IReadOnlyList<ConstraintEvaluationResult>>(policy.ConstraintResults, exactMatch: false);

        Assert.Equal(2, constraintResults.Count);

        IList<ConstraintEvaluationResult> listView =
            Assert.IsType<IList<ConstraintEvaluationResult>>(policy.ConstraintResults, exactMatch: false);

        _ = Assert.Throws<NotSupportedException>(() =>
            listView.Add(ConstraintEvaluationResult.Allow()));
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-branch-123",
            PolicyVersion = "v-branch",
            PolicyHash = "hash-branch"
        };
    }

    private static IEnumerable<IAsiBackboneConstraint<TestPolicyContext>> CreateConstraintEnumerable(
        params ConstraintEvaluationResult[] results)
    {
        foreach (ConstraintEvaluationResult result in results)
        {
            yield return new StaticConstraint(result);
        }
    }

    private sealed class TestPolicyContext : IAsiBackboneConstraintEvaluationContext
    {
        public string? CorrelationId { get; init; }

        public string? PolicyVersion { get; init; }

        public string? PolicyHash { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class StaticConstraint(ConstraintEvaluationResult result) : IAsiBackboneConstraint<TestPolicyContext>
    {
        private readonly ConstraintEvaluationResult result = result;

        public string Name => "static-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<ConstraintEvaluationResult>(result);
        }
    }

    private sealed class ThrowingConstraint(Exception exception, string name = "throwing-constraint") : IAsiBackboneConstraint<TestPolicyContext>
    {
        public string Name => name;

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            throw exception;
        }
    }

    private sealed class DelegateConstraint(
        Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate) : IAsiBackboneConstraint<TestPolicyContext>
    {
        private readonly Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate = evaluate;

        public string Name => "delegate-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<ConstraintEvaluationResult>(
                evaluate(context, cancellationToken));
        }
    }

    private sealed class CapturingDecisionPolicy : IAsiBackboneDecisionPolicy<TestPolicyContext>
    {
        public int ApplyCount { get; private set; }

        public TestPolicyContext? Context { get; private set; }

        public GovernanceDecision? ComposedDecision { get; private set; }

        public IReadOnlyList<ConstraintEvaluationResult>? ConstraintResults { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public ValueTask<GovernanceDecision> ApplyAsync(
            TestPolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            Context = context;
            ComposedDecision = composedDecision;
            ConstraintResults = constraintResults;
            CancellationToken = cancellationToken;

            return new ValueTask<GovernanceDecision>(
                GovernanceDecision.Defer(
                    "policy.deferred",
                    "The capturing policy deferred the operation.",
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash));
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<CapturedLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new CapturedLogEntry(logLevel, eventId, formatter(state, exception), exception));
        }
    }

    private sealed record CapturedLogEntry(
        LogLevel LogLevel,
        EventId EventId,
        string Message,
        Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
