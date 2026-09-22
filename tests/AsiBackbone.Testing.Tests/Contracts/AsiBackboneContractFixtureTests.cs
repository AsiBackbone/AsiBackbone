using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Testing.Contracts;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AsiBackbone.Testing.Tests.Contracts;

/// <summary>
/// Tests for the AsiBackbone contract fixtures to ensure that the test harnesses and contracts behave as expected.
/// </summary>
public sealed class AsiBackboneContractFixtureTests
{
    /// <summary>
    /// Verifies that the policy evaluator contract passes when using the test harness policy evaluator with a deny-all policy configuration.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task PolicyEvaluatorContractPassesForHarnessEvaluator()
    {
        GovernanceTestHarnessOptions options = new();
        _ = options.DenyAllPolicies("contract.policy_denied", "Denied by contract test.");
        var evaluator = new GovernanceTestHarnessPolicyEvaluator(options);
        var contract = new HarnessPolicyEvaluatorContract(evaluator);

        GovernanceDecision decision = await contract.VerifyEvaluatorReturnsSafeDecisionAsync(TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal("contract-correlation", decision.CorrelationId);
        Assert.Equal("test-harness", decision.PolicyVersion);
        Assert.Equal("contract-policy-hash", decision.PolicyHash);
        Assert.Contains("contract.policy_denied", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that the capability grant contract passes when using the test harness capability grant validator with a deny-all configuration for capability grants.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task CapabilityGrantContractPassesWhenInvalidGrantFailsClosed()
    {
        GovernanceTestHarnessOptions options = new();
        _ = options.DenyCapabilityGrants("contract.capability_denied", "Capability grant denied by contract test.");
        var validator = new GovernanceTestHarnessEndpointCapabilityGrantValidator(options);
        var contract = new HarnessCapabilityGrantValidatorContract(validator);

        GovernanceDecision decision = await contract.VerifyKnownInvalidCapabilityGrantDoesNotAllowAsync(TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Contains("contract.capability_denied", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that the capability grant contract fails when using a capability grant validator that incorrectly allows an invalid grant, ensuring that the contract correctly identifies this violation.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task CapabilityGrantContractFailsWhenInvalidGrantAllows()
    {
        var contract = new HarnessCapabilityGrantValidatorContract(new AllowingCapabilityGrantValidator());

        GovernanceContractViolationException exception = await Assert.ThrowsAsync<GovernanceContractViolationException>(
            async () => await contract.VerifyKnownInvalidCapabilityGrantDoesNotAllowAsync(TestContext.Current.CancellationToken));

        Assert.Contains("must not return Allow", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the decision receipt sink contract passes when using the test decision receipt sink, ensuring that valid decision receipts are accepted and recorded correctly.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task AuditSinkContractPassesForTestAuditSink()
    {
        var auditSink = new GovernanceTestDecisionReceiptSink();
        var contract = new TestAuditSinkContract(auditSink);

        await contract.VerifyDecisionReceiptSinkAcceptsValidReceiptAsync(TestContext.Current.CancellationToken);

        _ = Assert.Single(auditSink.Entries);
        Assert.Equal("contract-event", auditSink.Entries[0].EventId);
    }

    /// <summary>
    /// Verifies that the decision receipt sink contract fails when an decision receipt is missing a required event ID, ensuring that the contract correctly identifies this violation.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public void DecisionContractRejectsDecisionReceiptWithoutEventId()
    {
        var residue = new TestDecisionReceipt
        {
            EventId = ""
        };

        GovernanceContractViolationException exception = Assert.Throws<GovernanceContractViolationException>(
            () => GovernanceDecisionContract.VerifyDecisionReceipt(residue));

        Assert.Contains("event ID", exception.Message, StringComparison.Ordinal);
    }

    private sealed class HarnessPolicyEvaluatorContract(
        IGovernancePolicyEvaluator<GovernanceEvaluationContext> evaluator)
        : GovernancePolicyEvaluatorContract<GovernanceEvaluationContext>
    {
        protected override IGovernancePolicyEvaluator<GovernanceEvaluationContext> CreateEvaluator()
        {
            return evaluator;
        }

        protected override GovernanceEvaluationContext CreateEvaluationContext()
        {
            return new GovernanceEvaluationContext(
                correlationId: "contract-correlation",
                policyVersion: "contract-policy-v1",
                policyHash: "contract-policy-hash");
        }
    }

    private sealed class HarnessCapabilityGrantValidatorContract(
        IEndpointCapabilityGrantValidator validator)
        : EndpointCapabilityGrantValidatorContract
    {
        protected override IEndpointCapabilityGrantValidator CreateValidator()
        {
            return validator;
        }
    }

    private sealed class TestAuditSinkContract(GovernanceTestDecisionReceiptSink auditSink) : DecisionReceiptSinkContract
    {
        protected override IDecisionReceiptSink CreateDecisionReceiptSink()
        {
            return auditSink;
        }

        protected override IDecisionReceipt CreateDecisionReceipt()
        {
            return new TestDecisionReceipt();
        }
    }

    private sealed class AllowingCapabilityGrantValidator : IEndpointCapabilityGrantValidator
    {
        public ValueTask<GovernanceDecision> ValidateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            GovernanceDecision currentDecision,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceDecision.Allow(
                currentDecision.CorrelationId,
                currentDecision.TraceId,
                currentDecision.PolicyVersion,
                currentDecision.PolicyHash));
        }
    }

    private sealed class TestDecisionReceipt : IDecisionReceipt
    {
        public string EventId { get; init; } = "contract-event";

        public DateTimeOffset OccurredUtc { get; init; } = DateTimeOffset.UtcNow;

        public string ActorId { get; init; } = "contract-actor";

        public GovernanceActorType ActorType { get; init; } = GovernanceActorType.System;

        public string? ActorDisplayName { get; init; } = "Contract Actor";

        public string OperationName { get; init; } = "contract.operation";

        public string Outcome { get; init; } = "Allowed";

        public IReadOnlyList<string> ReasonCodes { get; init; } = Array.Empty<string>();

        public string? CorrelationId { get; init; } = "contract-correlation";

        public string? TraceId { get; init; } = "contract-trace";

        public string? PolicyVersion { get; init; } = "contract-policy-v1";

        public string? PolicyHash { get; init; } = "contract-policy-hash";

        public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["contract"] = "true"
        };
    }
}
