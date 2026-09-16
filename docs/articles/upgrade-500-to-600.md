# Upgrade from 5.x to 6.0

Version 6.0 removes seven public members whose obsolete compatibility windows have completed. These are intentional major-version API breaks. Rebuild consumers against the 6.0 packages after migrating.

## Complete obsolete-member inventory

The repository-wide inventory of source attributes (`Obsolete` / `ObsoleteAttribute`), public API baselines, warning suppressions, analyzer diagnostics, tests, samples, and documentation found exactly these seven obsolete public members. No other obsolete aliases or forwarding APIs were found.

| Removed API | Supported replacement |
| --- | --- |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, decisionPolicy = null)` | Builder with `AddConstraints` and `WithDecisionPolicy` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, decisionPolicy, options)` | Builder with `AddConstraints`, `WithDecisionPolicy`, and `WithOptions` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, decisionPolicy, options, logger)` | Builder with `AddConstraints`, `WithDecisionPolicy`, `WithOptions`, and `WithLogger` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, threatModelContributors, decisionPolicy = null)` | Builder with `AddConstraints`, `AddThreatModelContributors`, and `WithDecisionPolicy` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, threatModelContributors, decisionPolicy, options)` | Builder with `AddConstraints`, `AddThreatModelContributors`, `WithDecisionPolicy`, and `WithOptions` |
| `RequireGovernancePolicy<TPolicy>(RouteHandlerBuilder)` | `MarkGovernancePolicy<TPolicy>()` for decision-policy types; `MarkGovernancePolicy(typeof(TPolicy))` for plain marker types |
| `RequireGovernancePolicy<TBuilder>(TBuilder, Type)` | `MarkGovernancePolicy(builder, policyType)` |

Start manual construction with `DefaultAsiBackbonePolicyEvaluator.CreateBuilder<TContext>()`, configure the dependencies, and call `Build()`. For any removed evaluator constructor, direct construction and dependency injection can instead use the supported full-dependency constructor:

```csharp
var evaluator = new DefaultAsiBackbonePolicyEvaluator<MyPolicyContext>(
    constraints,
    threatModelContributors: null,
    decisionPolicy: null,
    options: null,
    logger: null);
```

Supply the host's actual contributors, decision policy, options, and logger wherever configured. Null optional dependencies retain the evaluator's defaults; empty constraints still deny by default.

Type-based dependency injection registration now has only the full constructor to activate. It requires all five dependencies to be resolvable, including the concrete evaluator options object. Hosts using the options pattern should use a factory instead, passing `IOptions<AsiBackbonePolicyEvaluatorOptions>.Value` to `WithOptions` (or the full constructor), and resolving the other configured dependencies explicitly. The sample and template use this factory pattern so unregistered optional dependencies retain their defaults.

`MarkGovernancePolicy` records the same policy metadata. It does not resolve the policy or select/enforce constraints solely from the marker. The non-obsolete `RequireGovernancePolicyAttribute` remains supported.

## Diagnostics and compatibility validation

The internal `AsiBackboneObsoletions` helper and its ASIB900 message, ID, and URL constants existed solely for the removed constructors and were deleted. ASIB900 came from the compiler's obsolete attribute support, not a dedicated Roslyn analyzer; no analyzer diagnostic needed removal. Obsolete-only forwarding and attribute tests and ASIB900 project suppressions were removed. Behavioral evaluator tests now use the full constructor; builder and marker replacement tests remain.

The managed API baselines intentionally remove only the seven inventoried members. Package compatibility validation retains its previous-release comparison, with narrowly scoped removal suppressions for the affected assemblies; other compatibility failures still fail packing. Historical 4.x/5.x release notes and migration guidance remain available.
