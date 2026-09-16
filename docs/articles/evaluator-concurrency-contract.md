# Evaluator Concurrency Contract

This article defines the supported concurrency and construction-snapshot behavior of `DefaultGovernancePolicyEvaluator<TContext>`.

The contract is intentionally narrow. AsiBackbone can keep its own evaluator state stable, but it cannot make arbitrary host-provided constraints, threat contributors, decision policies, contexts, loggers, or external dependencies thread-safe.

## Construction snapshots

The evaluator captures the following inputs when it is constructed:

- the ordered constraint sequence;
- the ordered threat-model contributor sequence;
- the configured evaluator option values.

Later mutation of the caller-owned constraint or contributor collections does not add, remove, or reorder extensions inside the existing evaluator instance.

`GovernancePolicyOptions` remains mutable while a host configures it. Evaluator construction validates and freezes the supplied instance. Attempts to change it afterward throw `InvalidOperationException`. Create a separate options instance before constructing another evaluator when a different posture is required.

This prevents a long-lived evaluator from silently changing behavior because another component retained and modified the original options reference.

## Concurrent evaluator use

The default evaluator does not retain per-evaluation constraint results, denial accumulators, warning accumulators, threat assessments, or contexts in shared mutable fields. Each `EvaluateAsync` invocation creates and owns its working state.

An evaluator instance may therefore be invoked concurrently when all objects used by those invocations satisfy their own concurrency requirements.

Host-provided implementations must be safe for the lifetime and registration scope in which the host shares them:

- `IGovernanceConstraint<TContext>` implementations;
- `IThreatModelContributor<TContext>` implementations;
- `IGovernanceDecisionPolicy<TContext>` implementations;
- evaluation contexts and any mutable objects reachable from them;
- loggers, caches, clients, repositories, policy stores, and other dependencies used by extensions.

A singleton evaluator that contains a stateful, non-thread-safe constraint is not made concurrency-safe by the evaluator. Hosts should use immutable or stateless extensions where practical, synchronize mutable state explicitly, or choose a dependency-injection lifetime that matches the extension's guarantees.

## Ordering guarantee

Within one evaluation, constraints and threat contributors run in the order captured during evaluator construction.

Concurrent evaluations may interleave at the host-extension level. The evaluator does not serialize calls across requests and does not guarantee global ordering between separate evaluations.

Do not rely on one concurrent evaluation completing before another unless the host provides that coordination outside the evaluator.

## Result artifacts

`GovernanceDecision`, `ConstraintEvaluationResult`, and `DecisionReceipt` are immutable snapshots after creation:

- scalar properties are get-only;
- reason and reason-code collections are normalized into private read-only snapshots;
- audit metadata is normalized into a read-only snapshot.

These artifacts are suitable for concurrent reading. They do not make objects supplied to later host operations thread-safe, and they do not provide transaction isolation for persistence, emission, or execution.

## Context ownership

The evaluator reads the supplied context during evaluation. The host must not mutate context values or mutable data reachable through the context while that evaluation is using them unless the context implementation explicitly supports concurrent access.

For predictable decisions, prefer immutable request-scoped contexts and read-only metadata snapshots.

## Regression coverage boundary

The test suite includes concurrent evaluations using stateless extensions and verifies deterministic result shape and correlation propagation. It also verifies that construction captures collection ordering and locks the configured option posture.

Those tests are regression coverage for this documented contract. They do **not** prove thread safety for every runtime, host extension, dependency, scheduling pattern, or deployment topology.

## Recommended host posture

- Build the evaluator after policy registration is complete.
- Treat its constraint and contributor ordering as fixed for that evaluator instance.
- Do not attempt to reconfigure a constructed evaluator by mutating its options.
- Use stateless or explicitly concurrency-safe singleton extensions.
- Use scoped or transient lifetimes for extensions that carry request-specific mutable state.
- Keep evaluation contexts immutable for the duration of evaluation.
- Treat persistence, outbox emission, replay protection, and external execution concurrency as separate host-owned boundaries.

## Related documentation

- [Core Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Constraint Exception Policy](constraint-exception-policy.md)
- [Threat Model Contributors](threat-model-contributors.md)
- [Production Hardening: Evaluator and Outbox](production-hardening-evaluator-and-outbox.md)
