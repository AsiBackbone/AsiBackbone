using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Storage.InMemory.Audit;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// End-to-end proof that Core primitives compose without a database or web host:
/// intent and context -> constraint evaluation -> governance decision -> audit residue.
/// </summary>
public sealed class PolicyEvaluatorEndToEndTests
{
    private const string OperationName = "document.approve";

    /// <summary>
    /// Verifies that an authenticated owner approving a low-risk document is allowed and audited.
    /// </summary>
    [Fact]
    public async Task AuthenticatedOwnerLowRiskIsAllowedAndAudited()
    {
        var ledger = new InMemoryAuditLedger();
        DocumentApprovalContext context = CreateContext(
            DocumentRisk.Low,
            requesterIsOwner: true,
            authenticated: true);

        GovernanceDecision decision = await EvaluateAsync(context);
        await RecordAsync(ledger, context, decision);

        Assert.True(decision.IsAllowed);
        Assert.True(decision.CanProceed);
        Assert.Equal(context.CorrelationId, decision.CorrelationId);
        Assert.Equal(context.PolicyVersion, decision.PolicyVersion);
        Assert.Equal(context.PolicyHash, decision.PolicyHash);

        IDecisionReceipt residue = Assert.Single(ledger.Records);
        Assert.Equal(nameof(GovernanceDecisionOutcome.Allowed), residue.Outcome);
        Assert.Equal(context.CorrelationId, residue.CorrelationId);
        Assert.Same(residue, ledger.GetByEventId(residue.EventId));
        Assert.Same(residue, Assert.Single(ledger.GetByCorrelationId(context.CorrelationId!)));
    }

    /// <summary>
    /// Verifies that a denying constraint wins and reason codes flow through decision and audit residue.
    /// </summary>
    [Fact]
    public async Task UnauthenticatedActorIsDeniedAndAudited()
    {
        var ledger = new InMemoryAuditLedger();
        DocumentApprovalContext context = CreateContext(
            DocumentRisk.Low,
            requesterIsOwner: true,
            authenticated: false);

        GovernanceDecision decision = await EvaluateAsync(context);
        await RecordAsync(ledger, context, decision);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Contains("constraint.actor_not_authenticated", decision.ReasonCodes);

        IDecisionReceipt residue = Assert.Single(ledger.Records);
        Assert.Equal(nameof(GovernanceDecisionOutcome.Denied), residue.Outcome);
        Assert.Contains("constraint.actor_not_authenticated", residue.ReasonCodes);
    }

    /// <summary>
    /// Verifies that a warning constraint remains non-blocking but audit-worthy.
    /// </summary>
    [Fact]
    public async Task ElevatedRiskProducesWarning()
    {
        DocumentApprovalContext context = CreateContext(
            DocumentRisk.Elevated,
            requesterIsOwner: true,
            authenticated: true);

        GovernanceDecision decision = await EvaluateAsync(context);

        Assert.True(decision.IsWarning);
        Assert.True(decision.CanProceed);
        Assert.Contains("constraint.elevated_risk", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that decision policy can raise an otherwise allowed request to acknowledgment required.
    /// </summary>
    [Fact]
    public async Task HighRiskIsRaisedToAcknowledgmentRequiredByDecisionPolicy()
    {
        var ledger = new InMemoryAuditLedger();
        DocumentApprovalContext context = CreateContext(
            DocumentRisk.High,
            requesterIsOwner: true,
            authenticated: true);

        GovernanceDecision decision = await EvaluateAsync(context);
        await RecordAsync(ledger, context, decision);

        Assert.True(decision.RequiresAcknowledgment);
        Assert.False(decision.CanProceed);
        Assert.Contains("decision.acknowledgment_required", decision.ReasonCodes);

        IDecisionReceipt residue = Assert.Single(ledger.Records);
        Assert.Equal(nameof(GovernanceDecisionOutcome.AcknowledgmentRequired), residue.Outcome);
    }

    /// <summary>
    /// Verifies that decision policy can raise an otherwise allowed request to escalation recommended.
    /// </summary>
    [Fact]
    public async Task CriticalRiskIsRaisedToEscalationRecommendedByDecisionPolicy()
    {
        DocumentApprovalContext context = CreateContext(
            DocumentRisk.Critical,
            requesterIsOwner: true,
            authenticated: true);

        GovernanceDecision decision = await EvaluateAsync(context);

        Assert.True(decision.EscalationRecommended);
        Assert.False(decision.CanProceed);
        Assert.Contains("decision.escalation_recommended", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that decision policy can defer a request when policy data is unavailable.
    /// </summary>
    [Fact]
    public async Task PendingPolicyIsDeferredByDecisionPolicy()
    {
        DocumentApprovalContext context = CreateContext(
            DocumentRisk.PendingPolicy,
            requesterIsOwner: true,
            authenticated: true);

        GovernanceDecision decision = await EvaluateAsync(context);

        Assert.True(decision.IsDeferred);
        Assert.False(decision.CanProceed);
        Assert.Contains("decision.policy_unavailable", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that a not-applicable constraint does not block the decision pipeline.
    /// </summary>
    [Fact]
    public async Task NoApplicableConstraintProducesAllowedDecision()
    {
        DocumentApprovalContext context = CreateContext(
            DocumentRisk.Low,
            requesterIsOwner: true,
            authenticated: true);

        var evaluator = new DefaultGovernancePolicyEvaluator<DocumentApprovalContext>(
            [new NotApplicableConstraint()], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, CancellationToken.None);

        Assert.True(decision.IsAllowed);
        Assert.True(decision.CanProceed);
        Assert.Empty(decision.ReasonCodes);
        Assert.Equal(context.CorrelationId, decision.CorrelationId);
    }

    private static async Task<GovernanceDecision> EvaluateAsync(DocumentApprovalContext context)
    {
        var evaluator = new DefaultGovernancePolicyEvaluator<DocumentApprovalContext>(
            constraints:
            [
                new AuthenticatedActorConstraint(),
                new OwnershipConstraint(),
                new ElevatedRiskConstraint()
            ],
            decisionPolicy: new DocumentRiskDecisionPolicy(), threatModelContributors: null, options: null, logger: null);

        return await evaluator.EvaluateAsync(context, CancellationToken.None);
    }

    private static async Task RecordAsync(
        InMemoryAuditLedger ledger,
        DocumentApprovalContext context,
        GovernanceDecision decision)
    {
        var residue = DecisionReceipt.Create(
            actor: context.Actor,
            operationName: context.OperationName,
            outcome: decision.Outcome.ToString(),
            reasonCodes: decision.ReasonCodes,
            correlationId: decision.CorrelationId,
            policyVersion: decision.PolicyVersion,
            policyHash: decision.PolicyHash);

        await ledger.WriteAsync(residue, CancellationToken.None);
    }

    private static DocumentApprovalContext CreateContext(
        DocumentRisk risk,
        bool requesterIsOwner,
        bool authenticated)
    {
        IGovernanceActorContext actor = GovernanceActorContext.Human(
            "user-42",
            "Test User",
            isAuthenticated: authenticated);

        return new DocumentApprovalContext
        {
            Actor = actor,
            OperationName = OperationName,
            DocumentId = "doc-1001",
            RequesterIsOwner = requesterIsOwner,
            Risk = risk,
            CorrelationId = "corr-abc-123",
            PolicyVersion = "v1",
            PolicyHash = "hash-xyz"
        };
    }

    private enum DocumentRisk
    {
        Low = 0,
        Elevated = 1,
        High = 2,
        Critical = 3,
        PendingPolicy = 4
    }

    private sealed class DocumentApprovalContext : IGovernanceEvaluationContext
    {
        public required IGovernanceActorContext Actor { get; init; }

        public required string OperationName { get; init; }

        public required string DocumentId { get; init; }

        public bool RequesterIsOwner { get; init; }

        public DocumentRisk Risk { get; init; }

        public string? CorrelationId { get; init; }

        public string? PolicyVersion { get; init; }

        public string? PolicyHash { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class AuthenticatedActorConstraint : IGovernanceConstraint<DocumentApprovalContext>
    {
        public string Name => "authenticated-actor";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            DocumentApprovalContext context,
            CancellationToken cancellationToken = default)
        {
            ConstraintEvaluationResult result = context.Actor.IsAuthenticated
                ? ConstraintEvaluationResult.Allow()
                : ConstraintEvaluationResult.Deny(
                    "constraint.actor_not_authenticated",
                    "The actor is not authenticated.");

            return new ValueTask<ConstraintEvaluationResult>(result);
        }
    }

    private sealed class OwnershipConstraint : IGovernanceConstraint<DocumentApprovalContext>
    {
        public string Name => "ownership";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            DocumentApprovalContext context,
            CancellationToken cancellationToken = default)
        {
            ConstraintEvaluationResult result = context.RequesterIsOwner
                ? ConstraintEvaluationResult.Allow()
                : ConstraintEvaluationResult.Deny(
                    "constraint.requester_not_owner",
                    "The requester does not own the document.");

            return new ValueTask<ConstraintEvaluationResult>(result);
        }
    }

    private sealed class ElevatedRiskConstraint : IGovernanceConstraint<DocumentApprovalContext>
    {
        public string Name => "elevated-risk";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            DocumentApprovalContext context,
            CancellationToken cancellationToken = default)
        {
            ConstraintEvaluationResult result = context.Risk is DocumentRisk.Elevated
                ? ConstraintEvaluationResult.Warning(
                    "constraint.elevated_risk",
                    "The document approval request has elevated risk.")
                : ConstraintEvaluationResult.NotApplicable();

            return new ValueTask<ConstraintEvaluationResult>(result);
        }
    }

    private sealed class NotApplicableConstraint : IGovernanceConstraint<DocumentApprovalContext>
    {
        public string Name => "not-applicable";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            DocumentApprovalContext context,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<ConstraintEvaluationResult>(ConstraintEvaluationResult.NotApplicable());
        }
    }

    private sealed class DocumentRiskDecisionPolicy : IGovernanceDecisionPolicy<DocumentApprovalContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            DocumentApprovalContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            if (!composedDecision.CanProceed)
            {
                return new ValueTask<GovernanceDecision>(composedDecision);
            }

            GovernanceDecision decision = context.Risk switch
            {
                DocumentRisk.Low => composedDecision,
                DocumentRisk.Elevated => composedDecision,
                DocumentRisk.High => GovernanceDecision.RequireAcknowledgment(
                    "decision.acknowledgment_required",
                    "High-risk document approvals require acknowledgment before execution.",
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash),
                DocumentRisk.Critical => GovernanceDecision.Escalate(
                    "decision.escalation_recommended",
                    "Critical-risk document approvals require supervisor review.",
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash),
                DocumentRisk.PendingPolicy => GovernanceDecision.Defer(
                    "decision.policy_unavailable",
                    "Required policy data is not available yet.",
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash),
                _ => composedDecision
            };

            return new ValueTask<GovernanceDecision>(decision);
        }
    }
}
