# ASIB900: Obsolete Policy Evaluator Constructors

> Historical 5.x diagnostic guide. The five partial constructors and ASIB900 diagnostic metadata were removed in 6.0. Warning suppression cannot restore them; use the [5.x to 6.0 migration guide](upgrade-500-to-600.md).

`ASIB900` is reported when code calls one of the five partial-argument
`DefaultAsiBackbonePolicyEvaluator<TContext>` constructor overloads. These
overloads are obsolete in the `5.x` line and will be removed in `6.0`.

This is a compile-time deprecation only. The overloads keep their current
behavior for the rest of `5.x`, and binaries compiled against them continue to
run.

## Who is affected

A project is affected if it constructs the evaluator directly with fewer than
all five dependencies, for example:

```csharp
new DefaultAsiBackbonePolicyEvaluator<MyPolicyContext>(constraints);
new DefaultAsiBackbonePolicyEvaluator<MyPolicyContext>(constraints, decisionPolicy);
new DefaultAsiBackbonePolicyEvaluator<MyPolicyContext>(constraints, decisionPolicy: null, options: options);
```

Projects with `TreatWarningsAsErrors` enabled will fail to build with `ASIB900`
after upgrading until the call sites are migrated or the diagnostic is
suppressed.

A project is **not** affected if it:

- registers the evaluator by type with dependency injection, such as
  `services.AddSingleton<IAsiBackbonePolicyEvaluator<TContext>, DefaultAsiBackbonePolicyEvaluator<TContext>>()`;
  or
- calls the constructor that accepts constraints, threat model contributors,
  decision policy, options, and logger. That constructor is not obsolete and
  remains supported in `6.0`.

## Migrate to the builder

Replace each obsolete constructor call with
`DefaultAsiBackbonePolicyEvaluator.CreateBuilder<TContext>()`. Set only what the
original call supplied; omitted values keep the same defaults as before.

| Obsolete argument | Builder call |
| --- | --- |
| `constraints` | `.AddConstraints(constraints)` or `.AddConstraint(constraint)` |
| `threatModelContributors` | `.AddThreatModelContributors(contributors)` |
| `decisionPolicy` | `.WithDecisionPolicy(decisionPolicy)` |
| `options` | `.WithOptions(options)` |
| `logger` | `.WithLogger(logger)` |

Before:

```csharp
var evaluator = new DefaultAsiBackbonePolicyEvaluator<MyPolicyContext>(
    constraints,
    decisionPolicy: new HighRiskDecisionPolicy(),
    options: new AsiBackbonePolicyEvaluatorOptions
    {
        ShortCircuitOnFirstDenial = true
    });
```

After:

```csharp
var evaluator = DefaultAsiBackbonePolicyEvaluator.CreateBuilder<MyPolicyContext>()
    .AddConstraints(constraints)
    .WithDecisionPolicy(new HighRiskDecisionPolicy())
    .WithOptions(new AsiBackbonePolicyEvaluatorOptions
    {
        ShortCircuitOnFirstDenial = true
    })
    .Build();
```

Behavior is unchanged. In particular, an evaluator built without options still
uses the fail-closed default `DenyWhenNoConstraints = true`, exactly as the
obsolete overloads did when `options` was omitted or `null`.

Each `Build()` call creates an independent evaluator that snapshots the
constraints and contributors added so far; later builder changes do not affect
evaluators that were already built.

## Temporary suppression

If a project cannot migrate immediately, suppress only this diagnostic:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);ASIB900</NoWarn>
</PropertyGroup>
```

Remove the suppression before upgrading to `6.0`, where the obsolete overloads
no longer exist and suppression cannot restore them.

## Related documentation

- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [API Compatibility and Semantic Versioning](api-compatibility-and-semver.md)
