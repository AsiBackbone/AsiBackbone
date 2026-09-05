# Host-Owned Execution Enforcement

This article documents the execution boundary that every AsiBackbone adopter must understand: AsiBackbone can produce governance decisions, audit residue, capability-boundary artifacts, and signing-ready records, but the host application remains responsible for honoring those decisions before consequential work is executed.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a governance spine and decision-flow layer. It is not an execution engine, legal certification mechanism, compliance guarantee, AI model host, or artificial superintelligence implementation.

## The host-owned execution gap

The governance spine can only protect a consequential operation when the host places the operation behind a decision gate.

```text
proposed operation
  -> build governance context
  -> evaluate constraints and decision policy
  -> receive GovernanceDecision
  -> write audit residue / outbox records as needed
  -> check decision.CanProceed according to host policy
  -> execute or stop the consequential operation
```

A bypass occurs when a host evaluates policy but lets side-effecting work continue without checking the decision outcome.

```text
proposed operation
  -> evaluate governance decision
  -> ignore or forget the decision
  -> execute side-effecting work anyway
```

That second path is a host implementation defect. AsiBackbone enables governance and auditability, but it does not guarantee compliance if the host does not honor the resulting decision.

## What AsiBackbone decides versus what the host executes

| Concern | Owner |
| --- | --- |
| Constraint contracts and decision composition | `AsiBackbone.Core` |
| Endpoint metadata and common HTTP-edge orchestration | `AsiBackbone.AspNetCore` |
| Durable audit persistence, transactions, and outbox storage | Host-owned storage/integration layer |
| Internal service methods, message consumers, job workers, CLI tools, orchestration handlers, and side-effecting operations | Host application |
| Compliance interpretation and evidence review | Host governance process |

The Core evaluator produces a `GovernanceDecision`. The host decides where and how to enforce that decision before executing work.

## Decision outcomes and execution posture

`GovernanceDecision.CanProceed` is `true` for `Allowed` and `Warning` outcomes. Other outcomes require host handling before execution can continue.

| Outcome posture | Recommended execution behavior |
| --- | --- |
| `Allowed` | Host may continue when other host checks also pass. |
| `Warning` | Host may continue if policy permits warning-with-execution and the warning is recorded. |
| `Denied` | Stop. Do not execute side-effecting work. |
| `Deferred` | Stop immediate execution. Queue, reschedule, or wait according to host workflow policy. |
| `AcknowledgmentRequired` | Stop until a valid acknowledgment workflow completes and the host has a fresh decision or explicit continuation policy. |
| `EscalationRecommended` | Stop or route to review according to host policy. Do not silently continue in regulated deployments. |
| Missing decision | Fail closed for consequential paths unless a documented lower-risk exception exists. |
| Evaluation failure | Fail closed for consequential paths unless a documented lower-risk exception exists. |

Warnings should be treated as deliberate policy decisions, not as accidental pass-through. Regulated hosts should document which warnings may continue and which warnings require review.

## HTTP endpoint governance pattern

For HTTP entry points, the ASP.NET Core package can enforce common endpoint metadata before the endpoint handler executes.

```csharp
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAsiBackboneEndpointGovernance();

app.MapPost("/payments/approve", ApprovePaymentAsync)
    .MarkGovernancePolicy<PaymentApprovalPolicy>()
    .EmitGovernanceAudit();
```

The middleware can evaluate the selected endpoint metadata, build an evaluation context, invoke the registered policy evaluator, and block execution with a safe HTTP result when policy, capability, or configuration checks fail closed.

HTTP middleware is not the whole enforcement story. It protects the HTTP edge where it is correctly registered. It does not automatically protect internal service methods, message consumers, background jobs, CLI tools, scheduled tasks, orchestration callbacks, or direct calls made outside the HTTP pipeline.

## Service-layer/manual gating pattern

Use a small host-owned wrapper for consequential service methods so manual checks are consistent and reviewable.

```csharp
public sealed class GovernedPaymentApprovalService
{
    private readonly IAsiBackbonePolicyEvaluator<PaymentApprovalContext> _evaluator;
    private readonly IAuditSink _auditSink;
    private readonly PaymentService _payments;

    public GovernedPaymentApprovalService(
        IAsiBackbonePolicyEvaluator<PaymentApprovalContext> evaluator,
        IAuditSink auditSink,
        PaymentService payments)
    {
        _evaluator = evaluator;
        _auditSink = auditSink;
        _payments = payments;
    }

    public async Task<IResult> ApproveAsync(
        PaymentApprovalContext context,
        CancellationToken cancellationToken)
    {
        GovernanceDecision decision = await _evaluator.EvaluateAsync(
            context,
            cancellationToken);

        await _auditSink.WriteAsync(
            AuditResidue.Create(
                actor: context.Actor,
                operationName: context.OperationName,
                outcome: decision.Outcome.ToString(),
                reasonCodes: decision.ReasonCodes,
                correlationId: decision.CorrelationId,
                policyVersion: decision.PolicyVersion,
                policyHash: decision.PolicyHash),
            cancellationToken);

        if (!decision.CanProceed)
        {
            return Results.Problem(
                title: "Governance decision blocked execution.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        await _payments.ApproveAsync(context.PaymentId, cancellationToken);
        return Results.Ok();
    }
}
```

The important property is not the exact wrapper shape. The important property is that the side-effecting operation is below the `decision.CanProceed` check, not beside it and not above it.

## Background worker and message-consumer pattern

Message consumers, job workers, scheduled tasks, and queue processors should evaluate governance before performing side effects.

```csharp
public async Task HandleAsync(
    PaymentApprovalMessage message,
    CancellationToken cancellationToken)
{
    PaymentApprovalContext context = PaymentApprovalContext.FromMessage(message);

    GovernanceDecision decision = await _evaluator.EvaluateAsync(
        context,
        cancellationToken);

    await _auditSink.WriteAsync(
        AuditResidue.Create(
            actor: context.Actor,
            operationName: context.OperationName,
            outcome: decision.Outcome.ToString(),
            reasonCodes: decision.ReasonCodes,
            correlationId: decision.CorrelationId,
            policyVersion: decision.PolicyVersion,
            policyHash: decision.PolicyHash),
        cancellationToken);

    if (!decision.CanProceed)
    {
        await _deadLetterOrReviewQueue.SendAsync(
            message,
            reason: string.Join(",", decision.ReasonCodes),
            cancellationToken);

        return;
    }

    await _paymentGateway.ApproveAsync(message.PaymentId, cancellationToken);
}
```

For consequential background work, missing policy configuration, missing evaluator registration, failed evaluation, or missing audit persistence should normally be treated as fail-closed. Lower-risk exceptions should be explicit and documented.

## Fail-closed handling pattern

A regulated host should prefer a fail-closed helper for repeated manual gates.

```csharp
public static async Task<GovernanceDecision> EvaluateOrDenyAsync<TContext>(
    IAsiBackbonePolicyEvaluator<TContext>? evaluator,
    TContext context,
    CancellationToken cancellationToken)
    where TContext : IAsiBackboneConstraintEvaluationContext
{
    if (evaluator is null)
    {
        return GovernanceDecision.Deny(
            "asibackbone.evaluator.missing",
            "Governance evaluator is not configured.");
    }

    try
    {
        return await evaluator.EvaluateAsync(context, cancellationToken);
    }
    catch (Exception)
    {
        return GovernanceDecision.Deny(
            "asibackbone.evaluation.failed",
            "Governance evaluation failed before execution.");
    }
}
```

This helper intentionally returns a denied decision instead of letting the caller proceed. Hosts may log the exception separately, but should avoid placing raw exception payloads or secrets into durable audit or telemetry fields.

## Internal enforcement wrapper checklist

For internal service methods that perform consequential work, prefer a small wrapper or helper that always performs the same sequence:

1. Build a minimal, safe governance context.
2. Evaluate policy with the registered evaluator.
3. Persist audit residue or lifecycle records according to host policy.
4. Stop when `decision.CanProceed` is false.
5. Stop when the decision is missing or evaluation fails.
6. Execute the side effect only after the decision is honored.
7. Preserve a correlation ID that lets reviewers connect the decision, audit record, outbox entry, and side effect.

This pattern prevents repeated ad hoc checks where one caller remembers the decision gate and another caller accidentally bypasses it.

## Regulated deployment code-review checklist

Before approving a regulated or review-sensitive deployment, verify:

- [ ] Every consequential HTTP endpoint is covered by endpoint governance middleware, explicit manual gating, or another documented control.
- [ ] Every consequential service method is reachable only through a governed wrapper or performs its own decision check.
- [ ] Message consumers and background workers fail closed when governance evaluation is missing, failed, denied, deferred, acknowledgment-required, or escalation-recommended.
- [ ] CLI tools, administrative scripts, scheduled jobs, and orchestration handlers use the same gate as the application path.
- [ ] Side-effecting calls appear below the decision check in code review.
- [ ] Denied and deferred decisions cannot fall through into execution.
- [ ] Warning decisions have documented policy for whether execution may continue.
- [ ] Acknowledgment-required decisions pause until acknowledgment handling is complete under host policy.
- [ ] Audit persistence or outbox enqueue behavior occurs before or at the required host-defined boundary.
- [ ] Missing evaluator, missing constraints, missing capability validator, missing audit sink, and evaluation exceptions fail closed unless an explicit lower-risk exception is documented.
- [ ] Tests cover allowed, warning, denied, deferred, missing-evaluator, and evaluation-failure paths.
- [ ] Operators can trace the side effect back to the governance decision by correlation ID or equivalent reference.
- [ ] Documentation states that AsiBackbone supports governance implementation, but does not certify compliance by itself.

## Non-goals

This guidance does not force every host into a single architecture. Some systems may enforce at the HTTP edge, some may enforce at service boundaries, and some may enforce in workers or gateways.

This guidance does not make Core depend on ASP.NET Core, EF Core, a queue system, a cloud provider, or a specific hosting model.

This guidance does not create legal or regulatory guarantees. The host remains responsible for implementation, testing, operational controls, evidence review, and compliance interpretation.

## Related documentation

- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
- [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Capability Grant Hardening](capability-grant-hardening.md)
- [Safe Audit and Telemetry Data Guidance](safe-audit-telemetry-data.md)
