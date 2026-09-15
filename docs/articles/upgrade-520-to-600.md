# Upgrade Guide: 5.2.x to 6.0.0

AsiBackbone 6.0 is a SemVer-major release. This guide records the public API
removals whose documented deprecation windows completed in the 5.x line.

## Removed public APIs

| Removed in 6.0 | Replacement |
| --- | --- |
| Partial `DefaultAsiBackbonePolicyEvaluator<TContext>` constructors | `DefaultAsiBackbonePolicyEvaluator.CreateBuilder<TContext>()` or the supported full-dependency constructor |
| `RequireGovernancePolicy<TPolicy>()` route-builder extension | `MarkGovernancePolicy<TPolicy>()` |
| `RequireGovernancePolicy<TBuilder>(..., Type)` route-builder extension | `MarkGovernancePolicy(..., Type)` |

## Policy evaluator construction

The five constructors deprecated as `ASIB900` in 5.2.x are removed in 6.0.
Prefer the builder in application code:

```csharp
var evaluator = DefaultAsiBackbonePolicyEvaluator
    .CreateBuilder<MyPolicyContext>()
    .AddConstraints(constraints)
    .AddThreatModelContributors(threatModelContributors)
    .WithDecisionPolicy(decisionPolicy)
    .WithOptions(options)
    .WithLogger(logger)
    .Build();
```

Direct construction remains available through the single supported constructor:

```csharp
var evaluator = new DefaultAsiBackbonePolicyEvaluator<MyPolicyContext>(
    constraints,
    threatModelContributors,
    decisionPolicy,
    options,
    logger);
```

The production-only `ASIB900` constants and test-project suppressions are removed
with the obsolete overloads. Historical 5.x migration documentation remains
available for version-specific traceability.

## Endpoint policy marker extensions

Minimal API and endpoint-convention callers should replace the removed
`RequireGovernancePolicy` extension methods with `MarkGovernancePolicy`:

```csharp
app.MapPost("/governed", handler)
    .MarkGovernancePolicy<MyDecisionPolicy>();
```

For a non-generic endpoint convention builder:

```csharp
builder.MarkGovernancePolicy(typeof(MyPolicyMarker));
```

The marker behavior is unchanged: it records host-owned policy metadata and does
not resolve a policy type or select a constraint set by itself.

The `[RequireGovernancePolicy]` controller/action attribute remains supported.
Only the two route-builder extension methods were removed.

## Removal inventory for issue #783

The repository-wide obsolete inventory identified two completed 5.x deprecation
areas for removal at the 6.0 boundary:

1. Five `ASIB900` partial constructors on
   `DefaultAsiBackbonePolicyEvaluator<TContext>`.
2. Two `RequireGovernancePolicy` endpoint route-builder extension methods.

Historical release notes and migration records that describe the 5.x
deprecation state are intentionally preserved. Current product guidance and
tests use the 6.0 surface.
