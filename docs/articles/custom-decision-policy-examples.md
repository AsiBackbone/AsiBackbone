# Custom Decision Policy Examples

> [!NOTE]
> This page is a package/API example. For policy-design education, alternative patterns, and tradeoffs, see [Constraint-Conditioned Decision Model](https://asibackbone.github.io/Learning/architecture/constraint-conditioned-decision-model.html) and the [Learning executable sample catalog](https://asibackbone.github.io/Learning/samples/).

This article shows host-owned `IAsiBackboneDecisionPolicy<TContext>` patterns for composing constraint results into a final governance decision.

Use custom decision policies when individual constraints are not enough to describe the final host posture. Constraints should answer narrow rule questions. A decision policy can then apply broader orchestration rules such as local overlays, risk thresholds, acknowledgment requirements, or escalation routing.

> [!IMPORTANT]
> A decision policy still does not execute the protected action. It only returns a `GovernanceDecision`. The host application remains responsible for enforcing `decision.CanProceed`, writing audit residue, validating capability grants, and performing or refusing the actual operation.

## Decision policy boundary

The default evaluator first composes constraint results. Then it calls the optional decision policy:

```text
context
  -> constraints produce allow / warning / deny / not-applicable results
  -> default evaluator composes a GovernanceDecision
  -> optional IAsiBackboneDecisionPolicy can preserve, narrow, defer, require acknowledgment, or recommend escalation
  -> host writes audit residue and owns execution
```

A good custom policy should:

- preserve important reason codes instead of hiding them;
- avoid side effects such as sending email, calling external APIs, or performing the protected action;
- keep policy inputs in safe context metadata rather than raw request bodies, tokens, secrets, or unnecessary PII;
- return a decision that the host can audit and enforce.

## Example 1: strict deny-wins policy with warning preservation

The default evaluator already treats a denied constraint as blocking. Some hosts also want warnings that occurred before or alongside the denial to remain visible in the final decision receipt.

```csharp
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Results;

public sealed class StrictDenyWinsDecisionPolicy<TContext> : IAsiBackboneDecisionPolicy<TContext>
    where TContext : IAsiBackboneConstraintEvaluationContext
{
    public ValueTask<GovernanceDecision> ApplyAsync(
        TContext context,
        GovernanceDecision composedDecision,
        IReadOnlyList<ConstraintEvaluationResult> constraintResults,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        OperationReason[] warnings = [.. constraintResults
            .Where(static result => result.IsWarning)
            .SelectMany(static result => result.Reasons)];

        OperationReason[] denials = [.. constraintResults
            .Where(static result => result.IsDenied)
            .SelectMany(static result => result.Reasons)];

        if (denials.Length > 0)
        {
            return ValueTask.FromResult(GovernanceDecision.Deny(
                warnings.Concat(denials),
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }

        if (warnings.Length > 0)
        {
            return ValueTask.FromResult(GovernanceDecision.Warning(
                warnings,
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }

        return ValueTask.FromResult(composedDecision);
    }
}
```

Use this pattern when audit reviewers need to see both blocking reasons and non-blocking warning context in the same final decision.

## Example 2: regional overlay with acknowledgment requirement

A global policy may allow an operation while a regional or local policy narrows that permission. This example keeps the global composed decision intact when it already blocks execution, then applies a local region list and a high-risk acknowledgment rule.

```csharp
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;

public sealed class RegionalOverlayDecisionPolicy<TContext>(IReadOnlySet<string> supportedRegions) :
    IAsiBackboneDecisionPolicy<TContext>
    where TContext : IAsiBackboneConstraintEvaluationContext
{
    public ValueTask<GovernanceDecision> ApplyAsync(
        TContext context,
        GovernanceDecision composedDecision,
        IReadOnlyList<ConstraintEvaluationResult> constraintResults,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Never loosen an existing block from constraints or upstream policy composition.
        if (!composedDecision.CanProceed)
        {
            return ValueTask.FromResult(composedDecision);
        }

        string? region = context.Metadata.GetValueOrDefault("region");
        string? risk = context.Metadata.GetValueOrDefault("risk");

        if (string.IsNullOrWhiteSpace(region) || !supportedRegions.Contains(region))
        {
            return ValueTask.FromResult(GovernanceDecision.Deny(
                "regional.unsupported",
                "The requested region is not enabled for this operation.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }

        if (string.Equals(risk, "high", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(GovernanceDecision.RequireAcknowledgment(
                "regional.acknowledgment_required",
                "The local overlay requires acknowledgment for this high-risk operation.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }

        return ValueTask.FromResult(composedDecision);
    }
}
```

Registration example:

```csharp
builder.Services.AddSingleton<IAsiBackboneDecisionPolicy<MyPolicyContext>>(
    new RegionalOverlayDecisionPolicy<MyPolicyContext>(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "US-LA",
            "US-TX"
        }));
```

Use this pattern when global policy defines the broad rule but local jurisdictions, tenants, regions, agencies, or business units can narrow the allowed surface.

## Example 3: gateway readiness policy

Gateway-style operations often need more than a simple allow result. For example, the host may require proof that a capability grant was validated and that a decision receipt is ready before external execution proceeds.

```csharp
public sealed class GatewayReadinessDecisionPolicy<TContext> : IAsiBackboneDecisionPolicy<TContext>
    where TContext : IAsiBackboneConstraintEvaluationContext
{
    public ValueTask<GovernanceDecision> ApplyAsync(
        TContext context,
        GovernanceDecision composedDecision,
        IReadOnlyList<ConstraintEvaluationResult> constraintResults,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!composedDecision.CanProceed)
        {
            return ValueTask.FromResult(composedDecision);
        }

        bool capabilityValidated = context.Metadata.TryGetValue("capability.validated", out string? capabilityValue)
            && string.Equals(capabilityValue, "true", StringComparison.OrdinalIgnoreCase);
        bool receiptReady = context.Metadata.TryGetValue("audit.receipt_ready", out string? receiptValue)
            && string.Equals(receiptValue, "true", StringComparison.OrdinalIgnoreCase);

        if (!capabilityValidated || !receiptReady)
        {
            return ValueTask.FromResult(GovernanceDecision.Escalate(
                "gateway.readiness_missing",
                "External execution requires validated capability and audit receipt readiness.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }

        return ValueTask.FromResult(composedDecision);
    }
}
```

This policy still does not validate the capability token, sign the receipt, or call the external gateway. Those steps remain host-owned. The policy only checks safe facts that the host placed into the evaluation context.

## Example 4: latency-sensitive orchestration

For high-throughput paths, a host may intentionally prefer first-denial fast-abort behavior over full reason aggregation. That choice belongs in evaluator options, not inside hidden constraint side effects.

```csharp
var evaluator = new DefaultAsiBackbonePolicyEvaluator<MyPolicyContext>(
    constraints: constraintsFromConfiguration,
    decisionPolicy: new RegionalOverlayDecisionPolicy<MyPolicyContext>(supportedRegions),
    options: new AsiBackbonePolicyEvaluatorOptions
    {
        ShortCircuitOnFirstDenial = true
    });
```

Use this only when the host accepts the audit tradeoff: constraints after the first blocking result will not run, so their reason codes and telemetry side effects will not exist for that evaluation.

## Host-owned execution pattern

After a decision policy returns, keep enforcement explicit:

```csharp
GovernanceDecision decision = await evaluator.EvaluateAsync(context, cancellationToken);

AuditResidue residue = AuditResidue.FromDecision(
    actor,
    operationName,
    decision,
    metadata: context.Metadata);

await auditSink.WriteAsync(residue, cancellationToken);

if (!decision.CanProceed)
{
    return Results.Forbid();
}

// Host-owned execution starts only after the governance decision allows continuation.
await orderService.ApproveAsync(orderId, cancellationToken);
```

This keeps the policy layer observable and testable without turning it into an implicit execution engine.

## Choosing an outcome

| Return outcome | Use when |
| --- | --- |
| `Allowed` | Constraints passed and no additional host review is needed. |
| `Warning` | The operation can continue, but the warning reason should be preserved for review. |
| `Denied` | The operation must not continue. |
| `AcknowledgmentRequired` | A qualified actor may continue only after an explicit acknowledgment workflow. |
| `Deferred` | The decision needs more information, later processing, or another system before continuation. |
| `EscalationRecommended` | A human or higher-level governance path should review before execution. |

## Related documentation

- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)
- [Acknowledgment Workflow](dynamic-liability-handshake.md)
- [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md)
- [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)

## Product API reference

Use the [Generated API Reference](../api/) for exact members and signatures. Key surfaces used by this page include [`IAsiBackboneDecisionPolicy<TContext>`](xref:AsiBackbone.Core.Evaluation.IAsiBackboneDecisionPolicy`1), [`IAsiBackbonePolicyEvaluator<TContext>`](xref:AsiBackbone.Core.Evaluation.IAsiBackbonePolicyEvaluator`1), and [`GovernanceDecision`](xref:AsiBackbone.Core.Decisions.GovernanceDecision).
