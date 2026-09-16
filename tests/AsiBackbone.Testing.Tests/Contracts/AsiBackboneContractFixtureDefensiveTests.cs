using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Results;
using AsiBackbone.Testing.Contracts;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AsiBackbone.Testing.Tests.Contracts;

/// <summary>
/// Covers null factories, cancellation, exception wrapping, and explicit contract failures in reusable fixtures.
/// </summary>
public sealed class AsiBackboneContractFixtureDefensiveTests
{
    /// <summary>
    /// Verifies that a null evaluator factory result is rejected by the policy evaluator contract.
    /// </summary>
    [Fact]
    public async Task PolicyEvaluatorRejectsNullEvaluatorFactoryResult()
    {
        var contract = new PolicyEvaluatorContract(evaluator: null, context: CreateContext());

        GovernanceContractViolationException exception = await Assert.ThrowsAsync<GovernanceContractViolationException>(
            async () => await contract.VerifyEvaluatorReturnsSafeDecisionAsync(TestContext.Current.CancellationToken));

        Assert.Contains("provide an evaluator instance", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a null context factory result is rejected by the policy evaluator contract.
    /// </summary>
    [Fact]
    public async Task PolicyEvaluatorRejectsNullContextFactoryResult()
    {
        var contract = new PolicyEvaluatorContract(new AllowingPolicyEvaluator(), context: null);

        GovernanceContractViolationException exception = await Assert.ThrowsAsync<GovernanceContractViolationException>(
            async () => await contract.VerifyEvaluatorReturnsSafeDecisionAsync(TestContext.Current.CancellationToken));

        Assert.Contains("provide an evaluation context", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that cancellation from the evaluator is propagated.
    /// </summary>
    [Fact]
    public async Task PolicyEvaluatorPropagatesCancellation()
    {
        var contract = new PolicyEvaluatorContract(new CancelingPolicyEvaluator(), CreateContext());

        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await contract.VerifyEvaluatorReturnsSafeDecisionAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Ensures contract violations thrown by evaluators are not double-wrapped.
    /// </summary>
    [Fact]
    public async Task PolicyEvaluatorDoesNotDoubleWrapContractViolation()
    {
        var expected = new GovernanceContractViolationException("contract evaluator failure");
        var contract = new PolicyEvaluatorContract(new ContractViolatingPolicyEvaluator(expected), CreateContext());

        GovernanceContractViolationException actual = await Assert.ThrowsAsync<GovernanceContractViolationException>(
            async () => await contract.VerifyEvaluatorReturnsSafeDecisionAsync(TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
    }

    /// <summary>
    /// Verifies decision policy rejects null factories and null results appropriately.
    /// </summary>
    [Fact]
    public async Task DecisionPolicyRejectsNullFactories()
    {
        var nullPolicy = new DecisionPolicyContract(policy: null, context: CreateContext());
        var nullContext = new DecisionPolicyContract(new PassthroughDecisionPolicy(), context: null);
        var nullDecision = new DecisionPolicyContract(
            new PassthroughDecisionPolicy(),
            CreateContext(),
            returnNullDecision: true);
        var nullResults = new DecisionPolicyContract(
            new PassthroughDecisionPolicy(),
            CreateContext(),
            returnNullResults: true);

        Assert.Contains(
            "provide a decision policy instance",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullPolicy.VerifyDecisionPolicyReturnsSafeDecisionAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "provide an evaluation context",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullContext.VerifyDecisionPolicyReturnsSafeDecisionAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "provide a composed decision",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullDecision.VerifyDecisionPolicyReturnsSafeDecisionAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "provide a constraint-result collection",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullResults.VerifyDecisionPolicyReturnsSafeDecisionAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies decision policy propagates cancellation and contract-violation exceptions.
    /// </summary>
    [Fact]
    public async Task DecisionPolicyPropagatesCancellationAndContractViolation()
    {
        var canceling = new DecisionPolicyContract(new CancelingDecisionPolicy(), CreateContext());
        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await canceling.VerifyDecisionPolicyReturnsSafeDecisionAsync(TestContext.Current.CancellationToken));

        var expected = new GovernanceContractViolationException("contract policy failure");
        var violating = new DecisionPolicyContract(new ContractViolatingDecisionPolicy(expected), CreateContext());
        GovernanceContractViolationException actual = await Assert.ThrowsAsync<GovernanceContractViolationException>(
            async () => await violating.VerifyDecisionPolicyReturnsSafeDecisionAsync(TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
    }

    /// <summary>
    /// Ensures decision policies wrap implementation failures into contract violations.
    /// </summary>
    [Fact]
    public async Task DecisionPolicyWrapsImplementationFailure()
    {
        var contract = new DecisionPolicyContract(new ThrowingDecisionPolicy(), CreateContext());

        GovernanceContractViolationException exception = await Assert.ThrowsAsync<GovernanceContractViolationException>(
            async () => await contract.VerifyDecisionPolicyReturnsSafeDecisionAsync(TestContext.Current.CancellationToken));

        Assert.Contains("must return safe decisions", exception.Message, StringComparison.Ordinal);
        _ = Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    /// <summary>
    /// Verifies constraint contracts reject null factories and contexts.
    /// </summary>
    [Fact]
    public async Task ConstraintRejectsNullFactories()
    {
        var nullConstraint = new ConstraintContract(constraint: null, context: CreateContext());
        var nullContext = new ConstraintContract(new AllowingConstraint(), context: null);

        Assert.Contains(
            "provide a constraint instance",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullConstraint.VerifyConstraintReturnsSafeResultAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "provide an evaluation context",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullContext.VerifyConstraintReturnsSafeResultAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies constraints that require reasons reject results without reasons.
    /// </summary>
    [Theory]
    [InlineData(ConstraintEvaluationOutcome.Denied)]
    [InlineData(ConstraintEvaluationOutcome.Warning)]
    public async Task ConstraintRejectsReasonRequiredResultWithoutReasons(ConstraintEvaluationOutcome outcome)
    {
        var contract = new ConstraintContract(new MalformedConstraint(CreateConstraintResult(outcome, Array.Empty<OperationReason>())), CreateContext());

        GovernanceContractViolationException exception = await Assert.ThrowsAsync<GovernanceContractViolationException>(
            async () => await contract.VerifyConstraintReturnsSafeResultAsync(TestContext.Current.CancellationToken));

        Assert.Contains("without a reason code", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies constraint contracts reject null or blank reason members.
    /// </summary>
    [Fact]
    public async Task ConstraintRejectsNullAndBlankReasonMembers()
    {
        ConstraintEvaluationResult nullReasonResult = CreateConstraintResult(
            ConstraintEvaluationOutcome.Denied,
            new[]
            {
                OperationReason.Create("contract.reason", "Reason.")
            });

        SetAutoProperty(
            nullReasonResult,
            nameof(ConstraintEvaluationResult.Reasons),
            new OperationReason?[] { null });

        var nullReason = new ConstraintContract(
            new MalformedConstraint(nullReasonResult),
            CreateContext());

        Assert.Contains(
            "null reason",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullReason.VerifyConstraintReturnsSafeResultAsync(
                    TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);

        var blankCode = new ConstraintContract(
            new MalformedConstraint(CreateConstraintResult(
                ConstraintEvaluationOutcome.Warning,
                new[] { CreateMalformedReason(" ", "Message") })),
            CreateContext());

        Assert.Contains(
            "empty reason code",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await blankCode.VerifyConstraintReturnsSafeResultAsync(
                    TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);

        var blankMessage = new ConstraintContract(
            new MalformedConstraint(CreateConstraintResult(
                ConstraintEvaluationOutcome.Warning,
                new[] { CreateMalformedReason("contract.reason", " ") })),
            CreateContext());

        Assert.Contains(
            "empty reason message",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await blankMessage.VerifyConstraintReturnsSafeResultAsync(
                    TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies constraint contracts propagate cancellations and contract violations.
    /// </summary>
    [Fact]
    public async Task ConstraintPropagatesCancellationAndContractViolation()
    {
        var canceling = new ConstraintContract(new CancelingConstraint(), CreateContext());
        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await canceling.VerifyConstraintReturnsSafeResultAsync(TestContext.Current.CancellationToken));

        var expected = new GovernanceContractViolationException("constraint contract failure");
        var violating = new ConstraintContract(new ContractViolatingConstraint(expected), CreateContext());
        GovernanceContractViolationException actual = await Assert.ThrowsAsync<GovernanceContractViolationException>(
            async () => await violating.VerifyConstraintReturnsSafeResultAsync(TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
    }

    /// <summary>
    /// Verifies audit sink contracts reject null factories and residues.
    /// </summary>
    [Fact]
    public async Task AuditSinkRejectsNullFactories()
    {
        var nullSink = new AuditSinkContract(sink: null, residue: new TestAuditResidue());
        var nullResidue = new AuditSinkContract(new AcceptingAuditSink(), residue: null);

        Assert.Contains(
            "provide an audit sink instance",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullSink.VerifyAuditSinkAcceptsValidResidueAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "provide decision receipt",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullResidue.VerifyAuditSinkAcceptsValidResidueAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies audit sink contracts propagate cancellations and contract violations.
    /// </summary>
    [Fact]
    public async Task AuditSinkPropagatesCancellationAndContractViolation()
    {
        var canceling = new AuditSinkContract(new CancelingAuditSink(), new TestAuditResidue());
        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await canceling.VerifyAuditSinkAcceptsValidResidueAsync(TestContext.Current.CancellationToken));

        var expected = new GovernanceContractViolationException("audit contract failure");
        var violating = new AuditSinkContract(new ContractViolatingAuditSink(expected), new TestAuditResidue());
        GovernanceContractViolationException actual = await Assert.ThrowsAsync<GovernanceContractViolationException>(
            async () => await violating.VerifyAuditSinkAcceptsValidResidueAsync(TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
    }

    /// <summary>
    /// Verifies capability fixture rejects null factories and related inputs.
    /// </summary>
    [Fact]
    public async Task CapabilityFixtureRejectsNullFactories()
    {
        var nullValidator = new CapabilityContract(validator: null);
        var nullContext = new CapabilityContract(new DenyingCapabilityValidator(), returnNullContext: true);
        var nullDescriptor = new CapabilityContract(new DenyingCapabilityValidator(), returnNullDescriptor: true);
        var nullDecision = new CapabilityContract(new DenyingCapabilityValidator(), returnNullDecision: true);

        Assert.Contains(
            "provide a validator instance",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullValidator.VerifyKnownInvalidCapabilityGrantDoesNotAllowAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "provide an HTTP context",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullContext.VerifyKnownInvalidCapabilityGrantDoesNotAllowAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "provide an endpoint descriptor",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullDescriptor.VerifyKnownInvalidCapabilityGrantDoesNotAllowAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "provide a current decision",
            (await Assert.ThrowsAsync<GovernanceContractViolationException>(
                async () => await nullDecision.VerifyKnownInvalidCapabilityGrantDoesNotAllowAsync(TestContext.Current.CancellationToken))).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies capability fixtures propagate cancellations and contract violations.
    /// </summary>
    [Fact]
    public async Task CapabilityFixturePropagatesCancellationAndContractViolation()
    {
        var canceling = new CapabilityContract(new CancelingCapabilityValidator());
        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await canceling.VerifyKnownInvalidCapabilityGrantDoesNotAllowAsync(TestContext.Current.CancellationToken));

        var expected = new GovernanceContractViolationException("capability contract failure");
        var violating = new CapabilityContract(new ContractViolatingCapabilityValidator(expected));
        GovernanceContractViolationException actual = await Assert.ThrowsAsync<GovernanceContractViolationException>(
            async () => await violating.VerifyKnownInvalidCapabilityGrantDoesNotAllowAsync(TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
    }

    /// <summary>
    /// Ensures capability fixtures wrap implementation failures into contract violations.
    /// </summary>
    [Fact]
    public async Task CapabilityFixtureWrapsImplementationFailure()
    {
        var contract = new CapabilityContract(new ThrowingCapabilityValidator());

        GovernanceContractViolationException exception = await Assert.ThrowsAsync<GovernanceContractViolationException>(
            async () => await contract.VerifyKnownInvalidCapabilityGrantDoesNotAllowAsync(TestContext.Current.CancellationToken));

        Assert.Contains("must fail closed", exception.Message, StringComparison.Ordinal);
        _ = Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private static GovernanceEvaluationContext CreateContext()
    {
        return new(correlationId: "contract-correlation", policyVersion: "policy-v1", policyHash: "policy-hash");
    }

    private static ConstraintEvaluationResult CreateConstraintResult(
        ConstraintEvaluationOutcome outcome,
        IReadOnlyList<OperationReason> reasons)
    {
        ConstructorInfo constructor = typeof(ConstraintEvaluationResult).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ConstraintEvaluationOutcome), typeof(IReadOnlyList<OperationReason>)],
            modifiers: null)
            ?? throw new InvalidOperationException("Constraint result constructor was not found.");

        return (ConstraintEvaluationResult)constructor.Invoke([outcome, reasons]);
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

    private sealed class PolicyEvaluatorContract(
        IGovernancePolicyEvaluator<GovernanceEvaluationContext>? evaluator,
        GovernanceEvaluationContext? context)
        : GovernancePolicyEvaluatorContract<GovernanceEvaluationContext>
    {
        protected override IGovernancePolicyEvaluator<GovernanceEvaluationContext> CreateEvaluator()
        {
            return evaluator!;
        }

        protected override GovernanceEvaluationContext CreateEvaluationContext()
        {
            return context!;
        }
    }

    private sealed class DecisionPolicyContract(
        IGovernanceDecisionPolicy<GovernanceEvaluationContext>? policy,
        GovernanceEvaluationContext? context,
        bool returnNullDecision = false,
        bool returnNullResults = false)
        : GovernanceDecisionPolicyContract<GovernanceEvaluationContext>
    {
        protected override IGovernanceDecisionPolicy<GovernanceEvaluationContext> CreateDecisionPolicy()
        {
            return policy!;
        }

        protected override GovernanceEvaluationContext CreateEvaluationContext()
        {
            return context!;
        }

        protected override GovernanceDecision CreateComposedDecision(GovernanceEvaluationContext evaluationContext)
        {
            return returnNullDecision ? null! : base.CreateComposedDecision(evaluationContext);
        }

        protected override IReadOnlyList<ConstraintEvaluationResult> CreateConstraintResults()
        {
            return returnNullResults ? null! : base.CreateConstraintResults();
        }
    }

    private sealed class ConstraintContract(
        IGovernanceConstraint<GovernanceEvaluationContext>? constraint,
        GovernanceEvaluationContext? context)
        : GovernanceConstraintContract<GovernanceEvaluationContext>
    {
        protected override IGovernanceConstraint<GovernanceEvaluationContext> CreateConstraint()
        {
            return constraint!;
        }

        protected override GovernanceEvaluationContext CreateEvaluationContext()
        {
            return context!;
        }
    }

    private sealed class AuditSinkContract(IDecisionReceiptSink? sink, IDecisionReceipt? residue)
        : DecisionReceiptSinkContract
    {
        protected override IDecisionReceiptSink CreateAuditSink()
        {
            return sink!;
        }

        protected override IDecisionReceipt CreateAuditResidue()
        {
            return residue!;
        }
    }

    private sealed class CapabilityContract(
        IEndpointCapabilityGrantValidator? validator,
        bool returnNullContext = false,
        bool returnNullDescriptor = false,
        bool returnNullDecision = false)
        : EndpointCapabilityGrantValidatorContract
    {
        protected override IEndpointCapabilityGrantValidator CreateValidator()
        {
            return validator!;
        }

        protected override HttpContext CreateHttpContext()
        {
            return returnNullContext ? null! : base.CreateHttpContext();
        }

        protected override EndpointGovernanceDescriptor CreateCapabilityDescriptor()
        {
            return returnNullDescriptor ? null! : base.CreateCapabilityDescriptor();
        }

        protected override GovernanceDecision CreateCurrentDecision()
        {
            return returnNullDecision ? null! : base.CreateCurrentDecision();
        }
    }

    private sealed class AllowingPolicyEvaluator : IGovernancePolicyEvaluator<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceDecision.Allow(context.CorrelationId, policyVersion: context.PolicyVersion, policyHash: context.PolicyHash));
        }
    }

    private sealed class CancelingPolicyEvaluator : IGovernancePolicyEvaluator<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<GovernanceDecision>(new OperationCanceledException());
        }
    }

    private sealed class ContractViolatingPolicyEvaluator(GovernanceContractViolationException exception)
        : IGovernancePolicyEvaluator<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<GovernanceDecision>(exception);
        }
    }

    private sealed class PassthroughDecisionPolicy : IGovernanceDecisionPolicy<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            GovernanceEvaluationContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(composedDecision);
        }
    }

    private sealed class CancelingDecisionPolicy : IGovernanceDecisionPolicy<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            GovernanceEvaluationContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<GovernanceDecision>(new OperationCanceledException());
        }
    }

    private sealed class ContractViolatingDecisionPolicy(GovernanceContractViolationException exception)
        : IGovernanceDecisionPolicy<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            GovernanceEvaluationContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<GovernanceDecision>(exception);
        }
    }

    private sealed class ThrowingDecisionPolicy : IGovernanceDecisionPolicy<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            GovernanceEvaluationContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<GovernanceDecision>(new InvalidOperationException("Decision policy failed."));
        }
    }

    private sealed class AllowingConstraint : IGovernanceConstraint<GovernanceEvaluationContext>
    {
        public string Name => "contract.allow";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ConstraintEvaluationResult.Allow());
        }
    }

    private sealed class MalformedConstraint(ConstraintEvaluationResult result)
        : IGovernanceConstraint<GovernanceEvaluationContext>
    {
        public string Name => "contract.malformed";
        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CancelingConstraint : IGovernanceConstraint<GovernanceEvaluationContext>
    {
        public string Name => "contract.cancel";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<ConstraintEvaluationResult>(new OperationCanceledException());
        }
    }

    private sealed class ContractViolatingConstraint(GovernanceContractViolationException exception)
        : IGovernanceConstraint<GovernanceEvaluationContext>
    {
        public string Name => "contract.violation";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<ConstraintEvaluationResult>(exception);
        }
    }

    private sealed class AcceptingAuditSink : IDecisionReceiptSink
    {
        public ValueTask WriteAsync(IDecisionReceipt residue, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CancelingAuditSink : IDecisionReceiptSink
    {
        public ValueTask WriteAsync(IDecisionReceipt residue, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException(new OperationCanceledException());
        }
    }

    private sealed class ContractViolatingAuditSink(GovernanceContractViolationException exception) : IDecisionReceiptSink
    {
        public ValueTask WriteAsync(IDecisionReceipt residue, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException(exception);
        }
    }

    private sealed class DenyingCapabilityValidator : IEndpointCapabilityGrantValidator
    {
        public ValueTask<GovernanceDecision> ValidateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            GovernanceDecision currentDecision,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceDecision.Deny("contract.denied", "Capability denied."));
        }
    }

    private sealed class CancelingCapabilityValidator : IEndpointCapabilityGrantValidator
    {
        public ValueTask<GovernanceDecision> ValidateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            GovernanceDecision currentDecision,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<GovernanceDecision>(new OperationCanceledException());
        }
    }

    private sealed class ContractViolatingCapabilityValidator(GovernanceContractViolationException exception)
        : IEndpointCapabilityGrantValidator
    {
        public ValueTask<GovernanceDecision> ValidateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            GovernanceDecision currentDecision,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<GovernanceDecision>(exception);
        }
    }

    private sealed class ThrowingCapabilityValidator : IEndpointCapabilityGrantValidator
    {
        public ValueTask<GovernanceDecision> ValidateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            GovernanceDecision currentDecision,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<GovernanceDecision>(new InvalidOperationException("Capability validator failed."));
        }
    }

    private sealed class TestAuditResidue : IDecisionReceipt
    {
        public string EventId => "contract-event";
        public DateTimeOffset OccurredUtc => DateTimeOffset.UtcNow;
        public string ActorId => "contract-actor";
        public GovernanceActorType ActorType => GovernanceActorType.System;
        public string? ActorDisplayName => "Contract Actor";
        public string OperationName => "contract.operation";
        public string Outcome => "Allowed";
        public IReadOnlyList<string> ReasonCodes => Array.Empty<string>();
        public string? CorrelationId => "contract-correlation";
        public string? TraceId => "contract-trace";
        public string? PolicyVersion => "policy-v1";
        public string? PolicyHash => "policy-hash";
        public IReadOnlyDictionary<string, string> Metadata => new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
