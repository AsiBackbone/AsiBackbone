# Core Policy Evaluator Pipeline

This article documents the host-neutral policy evaluation loop for `AsiBackbone.Core`.

The Core evaluator proves the governance spine without requiring ASP.NET Core, Entity Framework Core, a database, a web host, robotics integration, or an AI model runtime.

```text
intent or request
  -> policy evaluation context
  -> constraint evaluation
  -> governance decision
  -> audit residue
  -> optional in-memory audit ledger
```

## Ownership model

The current stable package-family ownership model is:

| Area | Responsibility |
| --- | --- |
| `AsiBackbone.Core` | Policy evaluator contracts, the default evaluator, decision composition, constraint contracts, decisions, audit residue, and audit sink contracts. |
| `AsiBackbone.Storage.InMemory` | In-process audit ledger support for tests, samples, and local validation hosts. |
| `AsiBackbone.AspNetCore` | Thin HTTP host adapters for service registration, current actor resolution, request correlation, audit enrichment, HTTP result mapping, and acknowledgment challenge helpers. |
| `AsiBackbone.EntityFrameworkCore` | EF Core model configuration and durable accountability persistence while preserving host-owned `DbContext`, provider, migrations, and database lifecycle. |

A future package split may move shared contracts into a dedicated abstractions package. For the current stable package family, the contracts remain in Core so the evaluator can be used without requiring a larger package restructuring.

## Default evaluator

`DefaultAsiBackbonePolicyEvaluator<TContext>` accepts a framework-neutral context and a collection of `IAsiBackboneConstraint<TContext>` instances.

The evaluator runs each constraint and composes the resulting `ConstraintEvaluationResult` values into a single `GovernanceDecision`.

Composition rules are intentionally conservative:

1. Deny wins when any constraint blocks the request.
2. Warning is returned when no constraint blocks but at least one constraint warns.
3. Allow is returned when constraints exist and no constraint blocks or warns.
4. Not-applicable constraint results do not block the request.
5. An optional `IAsiBackboneDecisionPolicy<TContext>` can raise the composed decision to deferred, acknowledgment-required, or escalation-recommended.
6. When the supplied constraint collection is empty and `AsiBackbonePolicyEvaluatorOptions.DenyWhenNoConstraints` remains at its `3.x` default of `true`, the evaluator returns a denied decision with reason code `asibackbone.policy.no_constraints`.
7. When `AsiBackbonePolicyEvaluatorOptions.ShortCircuitOnFirstDenial` is enabled, the evaluator stops after the first blocked constraint result and preserves reasons produced up to that point.
8. When full evaluation finds a denial, warning-only reasons are not copied into the final denied decision; the denied decision remains focused on blocking rationale.
9. When `AsiBackbonePolicyEvaluatorOptions.TreatConstraintExceptionAsDenial` remains at its `3.x` default of `true`, an eligible non-cancellation, non-critical exception thrown by a constraint becomes a denied decision with reason code `asibackbone.policy.constraint_exception`.
10. Threat-model contributor exceptions also fail closed by default with reason code `asibackbone.threat.contributor_exception`.

The evaluator propagates correlation, policy version, and policy hash metadata from the evaluation context into the composed governance decision.

When an optional `ILogger<DefaultAsiBackbonePolicyEvaluator<TContext>>` is supplied and evaluation runs with zero constraints while `DenyWhenNoConstraints` is explicitly set to `false`, the evaluator emits a warning. This makes the intentional permissive empty-policy path visible in operational logs.

## Evaluator construction

In 6.0, use the evaluator builder for manual construction or the single constructor accepting constraints, threat contributors, decision policy, options, and logger. Pass null for optional dependencies that the host does not supply. Preserve configured options and diagnostics when wiring dependency injection. See [Upgrade from 5.x to 6.0](upgrade-500-to-600.md) for removed overloads.

A DI registration that preserves the configured profile and operational diagnostics should resolve options and logger from the service provider:

```csharp
builder.Services.AddSingleton<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>>(serviceProvider =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<AsiBackbonePolicyEvaluatorOptions>>()
        .Value;

    var logger = serviceProvider
        .GetService<ILogger<DefaultAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>>>();

    return new DefaultAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>(
        serviceProvider.GetServices<IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>>(),
        serviceProvider.GetServices<IThreatModelContributor<AsiBackboneConstraintEvaluationContext>>(),
        decisionPolicy: serviceProvider.GetService<IAsiBackboneDecisionPolicy<AsiBackboneConstraintEvaluationContext>>(),
        options: options,
        logger: logger);
});
```

For intentionally permissive local samples, tests, or migration flows, using explicit option overrides is acceptable as long as the host clearly intends the behavior. For governed production surfaces, prefer the explicit overload that carries the host's configured options, threat contributors, decision policy, and logger.

## Minimal usage example

```csharp
var evaluator = DefaultAsiBackbonePolicyEvaluator.CreateBuilder<MyPolicyContext>()
    .AddConstraint(new AuthenticatedActorConstraint())
    .AddConstraint(new OwnershipConstraint())
    .AddConstraint(new RiskConstraint())
    .WithDecisionPolicy(new HighRiskDecisionPolicy())
    .Build();

GovernanceDecision decision = await evaluator.EvaluateAsync(
    context,
    cancellationToken);
```

For host-owned orchestration examples, see [Custom Decision Policy Examples](custom-decision-policy-examples.md). That article covers warning preservation, acknowledgment-required outcomes, regional overlays, gateway readiness checks, and the difference between policy evaluation and host-owned execution.

## Empty-policy behavior

The `3.x` default keeps `AsiBackbonePolicyEvaluatorOptions.DenyWhenNoConstraints` set to `true`. That means an evaluator created with an empty constraint collection produces a denied decision with reason code:

```text
asibackbone.policy.no_constraints
```

This default exists because an empty collection may mean dependency-injection, configuration, feature-flag, database, or policy-discovery failure.

Hosts that intentionally run an unconstrained local validation flow can opt out:

```csharp
var evaluator = DefaultAsiBackbonePolicyEvaluator.CreateBuilder<MyPolicyContext>()
    .WithOptions(new AsiBackbonePolicyEvaluatorOptions
    {
        DenyWhenNoConstraints = false
    })
    .Build();
```

If a logger is supplied, the evaluator emits a warning when this permissive empty-policy path is used. Treat that warning as an operational signal, not as a substitute for startup validation.

## Constraint authoring rule

Constraints should return explicit `ConstraintEvaluationResult` values for expected policy outcomes.

Use `ConstraintEvaluationResult.Deny(...)` when a known rule intentionally blocks a request:

```csharp
return ValueTask.FromResult(ConstraintEvaluationResult.Deny(
    "policy.region.denied",
    "The requested region is not permitted by the active policy."));
```

Do not throw an exception merely to deny a request:

```csharp
// Avoid this pattern for expected policy outcomes.
throw new InvalidOperationException("The requested region is not permitted.");
```

Exception-as-denial exists so the evaluator can fail closed when a constraint unexpectedly faults. It is not a policy-authoring shortcut. A returned denial means the rule intentionally blocked the request; `asibackbone.policy.constraint_exception` means the evaluator denied because a constraint faulted.

## Constraint exception behavior

The `3.x` default keeps `AsiBackbonePolicyEvaluatorOptions.TreatConstraintExceptionAsDenial` set to `true`. When enabled, a non-cancellation, non-critical exception thrown by a constraint becomes a denied `GovernanceDecision` with reason code:

```text
asibackbone.policy.constraint_exception
```

The generated denial preserves correlation ID, policy version, and policy hash. If a decision policy is configured, the denied decision is passed through that policy with a synthetic denied constraint result so downstream audit and policy code can observe the denial path.

Public reason messages intentionally do not include exception messages, stack traces, connection strings, raw payloads, secrets, tokens, or user input. When a logger is supplied, the exception object is attached to an error-level log entry. Hosts remain responsible for log redaction, retention, and access control.

Hosts that intentionally require fail-fast exception propagation can opt out:

```csharp
var evaluator = DefaultAsiBackbonePolicyEvaluator.CreateBuilder<MyPolicyContext>()
    .AddConstraints(constraintsFromConfiguration)
    .WithDecisionPolicy(new HighRiskDecisionPolicy())
    .WithOptions(new AsiBackbonePolicyEvaluatorOptions
    {
        TreatConstraintExceptionAsDenial = false
    })
    .Build();
```

Use the opt-out only when the host's exception, transaction, retry, telemetry, or incident boundary must observe the original exception directly and still records enough evidence for the governed attempt.

`OperationCanceledException` is not converted into a denial; cancellation continues to propagate. Critical host/runtime failures also continue to propagate.

See [Constraint Exception Policy](constraint-exception-policy.md) for the design note and recommended host posture.

## Warning-only reason handling when denial occurs

`DefaultAsiBackbonePolicyEvaluator<TContext>` treats warning-only reasons as advisory audit context and denial reasons as the blocking rationale. When `ShortCircuitOnFirstDenial` is `false`, the evaluator keeps running after a denied constraint so it can aggregate every denial reason produced by the full active constraint structure. As soon as a denial appears in this full-evaluation mode, accumulated warning-only reasons are cleared from the composed decision and later warnings are ignored.

This differs from `ShortCircuitOnFirstDenial = true`. In fast-abort mode, the evaluator stops as soon as the first denial is seen. Warnings produced before that abort point remain in the denied decision because they are part of the evaluated path, while later constraints are intentionally skipped and cannot add denial or warning reasons.

## Optional fast-abort on first blocked result

By default, the evaluator runs every registered constraint so the resulting decision, constraint result set, and downstream decision policy have the fullest available denial-reason visibility. This comprehensive path is the safest default for audit receipts, diagnostics, and policy review.

Latency-sensitive hosts can opt into first-denial fast-abort behavior:

```csharp
var evaluator = DefaultAsiBackbonePolicyEvaluator.CreateBuilder<MyPolicyContext>()
    .AddConstraints(constraintsFromConfiguration)
    .WithDecisionPolicy(new HighRiskDecisionPolicy())
    .WithOptions(new AsiBackbonePolicyEvaluatorOptions
    {
        ShortCircuitOnFirstDenial = true
    })
    .Build();
```

Use this mode only when the host explicitly prefers latency or throughput over complete constraint visibility. Keep the default full-evaluation mode for audit-heavy, diagnostic, or reviewer-facing paths.

After the decision is produced, a host or gateway can create audit residue and write it through an audit sink:

```csharp
AuditResidue residue = AuditResidue.FromDecision(
    actor,
    operationName,
    decision,
    metadata: context.Metadata);
```

The evaluator does not execute the protected operation. It returns a decision and supporting context; the host decides whether to continue, deny, defer, require acknowledgment, escalate, or retry.
