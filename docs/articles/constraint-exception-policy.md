# Constraint Exception Policy

This design note defines the intended behavior when an `IAsiBackboneConstraint<TContext>` throws during policy evaluation.

AsiBackbone is audit-first governance infrastructure. For the `3.x` stable line, ordinary policy constraint exceptions fail closed by default so a governed attempt can still produce a `GovernanceDecision`, stable reason code, policy metadata, and downstream audit residue. Hosts can still opt out when they intentionally want exceptions to propagate to an existing host error boundary.

## Default behavior: fail closed as a denied decision

The `3.x` stable default is fail-closed conversion for eligible ordinary constraint exceptions.

When a constraint throws and `AsiBackbonePolicyEvaluatorOptions.TreatConstraintExceptionAsDenial` is `true`, an expected non-cancellation, non-critical exception thrown by a constraint becomes a denied `GovernanceDecision` with reason code:

```text
asibackbone.policy.constraint_exception
```

The generated denial preserves:

- `CorrelationId`
- `PolicyVersion`
- `PolicyHash`

If a decision policy is configured, the generated denied decision is passed through that policy with a synthetic denied `ConstraintEvaluationResult` so downstream policy code can still observe a denial path.

## Consumer rule: return expected denials; do not throw them

`TreatConstraintExceptionAsDenial` is a fail-closed safety boundary, not the recommended way to author normal policy denials.

A constraint should return `ConstraintEvaluationResult.Deny(...)` when the request is understood, the policy is available, and the active rule intentionally blocks the operation. That keeps the decision explicit, cheap, reason-coded, and audit-friendly.

```csharp
public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
    IAsiBackboneConstraintEvaluationContext context,
    CancellationToken cancellationToken = default)
{
    if (context.Metadata.TryGetValue("region", out string? region) &&
        StringComparer.OrdinalIgnoreCase.Equals(region, "restricted"))
    {
        return ValueTask.FromResult(ConstraintEvaluationResult.Deny(
            "policy.region.denied",
            "The requested region is not permitted by the active policy."));
    }

    return ValueTask.FromResult(ConstraintEvaluationResult.Allow());
}
```

Do **not** intentionally throw exceptions to express routine policy denial:

```csharp
public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
    IAsiBackboneConstraintEvaluationContext context,
    CancellationToken cancellationToken = default)
{
    // Avoid this pattern for expected policy outcomes.
    throw new InvalidOperationException("The requested region is not permitted.");
}
```

Throwing for routine denial makes the denial path slower, less semantically precise, and easier to confuse with a broken dependency, malformed policy input, or host incident. The evaluator will still fail closed by default if this happens, but consumers should treat the resulting `asibackbone.policy.constraint_exception` reason code as an abnormal evaluator-failure denial rather than a normal policy-authored denial.

Use exceptions only for unexpected failures such as unavailable host-owned dependencies, corrupted policy inputs, invalid constraint configuration, or other conditions where the constraint cannot honestly produce an ordinary allow, warning, denial, or not-applicable result.

## Denial reason-code distinction

Consumers should keep normal policy denial and converted exception denial separate in logs, audit receipts, dashboards, and incident review:

| Path | Recommended meaning | Typical reason code shape |
| --- | --- | --- |
| Constraint returns `Deny(...)` | The rule intentionally blocked the request. | `policy.<area>.denied` or another host-owned stable code. |
| Evaluator converts a thrown constraint exception | The evaluator failed closed because a constraint faulted. | `asibackbone.policy.constraint_exception`. |

This distinction lets a host say whether a request was denied by policy or denied because the policy evaluation path faulted.

## Opt-out behavior: fail fast to the host

Hosts that need fail-fast exception propagation can opt out explicitly:

```csharp
var evaluator = DefaultAsiBackbonePolicyEvaluator.CreateBuilder<MyPolicyContext>()
    .AddConstraints(constraintsFromConfiguration)
    .WithDecisionPolicy(decisionPolicy)
    .WithOptions(new AsiBackbonePolicyEvaluatorOptions
    {
        TreatConstraintExceptionAsDenial = false
    })
    .Build();
```

When `TreatConstraintExceptionAsDenial` is `false`, the evaluator does not convert ordinary constraint exceptions into governance denials. The exception propagates to the host application so host exception handling, retry, circuit-breaker, telemetry, transaction, or incident policy can decide what happens next.

This opt-out is appropriate when the host has reliable exception-to-audit handling or needs to distinguish infrastructure failure from policy denial outside the evaluator.

## Critical host/runtime failures still propagate

`TreatConstraintExceptionAsDenial` is for expected policy/constraint failures. It is not a host corruption, runtime-failure, or infrastructure-failure suppression switch.

The evaluator does **not** convert cancellation or critical host/runtime failures into ordinary denied governance decisions. Those exceptions continue to propagate to the host error boundary.

### Modern .NET catchability boundary

Modern .NET does not guarantee that corrupted-state or process-failure conditions reach an ordinary `catch (Exception)` filter. A real stack overflow is generally process-ending, and a process-corrupting access violation may bypass normal managed recovery paths. AsiBackbone therefore treats the critical exception list as a defensive boundary for catchable cases, manually constructed exception instances, wrapper exceptions, or platform-specific delivery behavior. It should not be read as a claim that these failures are safe to catch, recover from, or convert into governance decisions.

The critical exception filter currently includes:

- `OutOfMemoryException`
- `StackOverflowException` where catchable by the runtime, such as manually constructed or wrapped instances used in tests
- `AccessViolationException` where catchable by the runtime; process-corrupting access violations should still be handled as host/runtime failures
- `AppDomainUnloadedException`
- `BadImageFormatException`
- `InvalidProgramException`
- wrapper exceptions whose `InnerException` contains one of the critical exception types above

This keeps fail-closed governance separate from systemic host failure. A critical runtime condition should be visible to the host's incident, restart, health-check, or crash-handling path rather than being reported as a normal policy denial.

## Sensitive exception details

Public decision reasons intentionally do not include exception messages, stack traces, connection strings, raw payloads, secrets, tokens, or user input.

The default public reason message is:

```text
A policy constraint failed during evaluation. The operation was denied by the evaluator failure policy.
```

Hosts may override the reason code and message, but should keep them curated, bounded, and free of sensitive data.

When a logger is supplied to `DefaultAsiBackbonePolicyEvaluator<TContext>`, the converted exception is logged at error level with the exception object attached to the log entry. This is host-owned operational telemetry, not public decision output. Hosts should apply their normal log redaction, retention, and access-control policy.

The log event for a converted constraint exception is:

```text
EventId: 4120
Name: ConstraintExceptionDeniedError
Meaning: exception-as-denial path for a policy constraint failure
```

The log message includes the constraint name, exception type, correlation ID, policy version, and policy hash. It intentionally distinguishes exception-as-denial from a normal constraint-returned denial.

## Cancellation remains cancellation

`OperationCanceledException` is not converted into a denied decision. It continues to propagate so host cancellation semantics remain intact.

## Recommended host posture

Use the default fail-closed behavior when:

- every governed attempt should produce a decision artifact;
- ASP.NET Core endpoint governance or another host layer should be able to audit the resulting denial normally;
- the host wants a consistent fail-closed policy surface for unexpected ordinary constraint failures;
- downstream systems rely on reason codes rather than exception propagation.

Set `TreatConstraintExceptionAsDenial = false` when:

- the host already has reliable exception-to-audit handling;
- exceptions should abort a transaction and be handled by a central failure path;
- the host wants to distinguish infrastructure failure from policy denial outside the evaluator;
- retry, circuit-breaker, or incident policy must observe the original exception directly.

Do not use `TreatConstraintExceptionAsDenial` to hide critical runtime failures, broken host dependencies, corrupted process state, or other incidents that should be escalated through host operations.

Do not author normal constraint denials by throwing exceptions. Return `ConstraintEvaluationResult.Deny(...)` for expected policy blocks, and reserve thrown exceptions for unexpected fault conditions that the evaluator may convert into an abnormal fail-closed denial.

## Boundary

This option does not make exception data safe, sanitized, or compliance-ready by itself. It only converts eligible evaluator outcomes into a denied decision with stable reason codes. Host applications still own logging policy, audit persistence, redaction, telemetry export, retention, critical-failure escalation, and incident response.

## Related documentation

- [Core Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Safe Audit and Telemetry Data Guidance](safe-audit-telemetry-data.md)
- [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md)
- [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
