using AsiBackbone.Core.Constraints;
using Xunit;

namespace AsiBackbone.Core.Tests.Constraints;

/// <summary>
/// Tests for <see cref="IGovernanceConstraint{TContext}"/> implementations.
/// </summary>
public sealed class IAsiBackboneConstraintTests
{
    /// <summary>
    /// Verifies that a constraint can evaluate a typed context and return the expected result.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous test operation. The task result is not used.
    /// </returns>
    [Fact]
    public async Task ConstraintCanEvaluateTypedContext()
    {
        var constraint = new TestConstraint();
        var context = new TestContext("document.approve");

        ConstraintEvaluationResult result = await constraint.EvaluateAsync(context, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("test.constraint", constraint.Name);
        Assert.True(result.CanProceed);
        Assert.Equal(ConstraintEvaluationOutcome.Allowed, result.Outcome);
    }

    /// <summary>
    /// Verifies that a constraint can return NotApplicable for contexts it does not handle.
    /// </summary>
    [Fact]
    public async Task ConstraintCanReturnNotApplicableForUnhandledContext()
    {
        var constraint = new TestConstraint();
        var context = new TestContext("document.archive");

        ConstraintEvaluationResult result = await constraint.EvaluateAsync(
            context,
            Xunit.TestContext.Current.CancellationToken);

        Assert.True(result.CanProceed);
        Assert.True(result.IsNotApplicable);
        Assert.Equal(ConstraintEvaluationOutcome.NotApplicable, result.Outcome);
        Assert.False(result.HasReasons);
        Assert.Empty(result.ReasonCodes);
    }

    /// <summary>
    /// Verifies that a constraint implementation can honor cancellation before evaluation.
    /// </summary>
    [Fact]
    public async Task ConstraintCanHonorCancellation()
    {
        var constraint = new CancellationAwareTestConstraint();
        var context = new TestContext("document.approve");

        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await constraint.EvaluateAsync(context, cancellationTokenSource.Token));
    }

    private sealed record TestContext(string IntentName);

    private sealed class TestConstraint : IGovernanceConstraint<TestContext>
    {
        public string Name => "test.constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.IntentName == "document.approve"
                ? ValueTask.FromResult(ConstraintEvaluationResult.Allow())
                : ValueTask.FromResult(ConstraintEvaluationResult.NotApplicable());
        }
    }

    private sealed class CancellationAwareTestConstraint : IGovernanceConstraint<TestContext>
    {
        public string Name => "test.cancellation-aware.constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(context);

            return ValueTask.FromResult(ConstraintEvaluationResult.Allow());
        }
    }
}
