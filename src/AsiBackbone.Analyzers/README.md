# AsiBackbone.Analyzers

Roslyn analyzer safety rails for Accountable Systems Infrastructure governance flows.

> **New to AsiBackbone?** Start with the concept, not this package: [Intent to Execution: An Accountability Pattern](https://asibackbone.github.io/AsiBackbone/articles/intent-to-execution-pattern.html) and the [documentation site](https://cdcavell.github.io/AsiBackbone/). This README covers one package in the family.

> **Important:**
> This package does not add runtime enforcement, persistence, transactions, audit storage, outbox delivery, legal protection, or compliance certification.

## Rule inventory and severity

| Rule | Default severity | Production posture | Summary |
| --- | --- | --- | --- |
| `ASIB001` | Warning | Advisory safety rail | Governance artifact created or returned and then discarded. |
| `ASIB002` | Warning | Recommended production CI error | Local-development signing is registered or instantiated inside a production environment branch. |

Analyzer warnings are advisory by default. Hosts can keep exploratory rules as warnings while elevating high-risk production misconfiguration rules, such as `ASIB002`, to errors in production CI with `.editorconfig`.

## ASIB001 - Persist or continue AsiBackbone governance artifact

`ASIB001` warns when an AsiBackbone governance artifact is created or returned and then discarded.

Examples include discarded governance decisions, audit residue, outbox entries, capability grants, handshake acknowledgments, and governance emission results.

```csharp
GovernanceDecision.Allow(); // ASIB001
_ = GovernanceDecision.Allow(); // ASIB001
```

Persist the artifact, pass it to a durable audit/outbox path, return it to the caller, or route it into a host-owned continuation before execution.

```csharp
GovernanceDecision decision = GovernanceDecision.Allow();
return decision;
```

## ASIB002 - Do not wire local-development signing in production branches

`ASIB002` warns when a local-development signing type is registered, instantiated, or passed into a service-registration call inside an explicit production branch.

```csharp
if (builder.Environment.IsProduction())
{
    builder.Services.AddSingleton<LocalDevelopmentSigningService>(); // ASIB002
}
```

```csharp
if (environment.EnvironmentName == "Production")
{
    builder.Services.AddSingleton(LocalDevelopmentSigningOptions.Create()); // ASIB002
}
```

The rule intentionally requires a recognizable production signal, such as `IHostEnvironment.IsProduction()` or an `EnvironmentName == "Production"` comparison. It does not warn merely because a non-test project references the package; that avoids noisy diagnostics for samples, local-only hosts, and explicit development wiring.

Use a host-owned production signing provider, managed-key adapter, or verification boundary instead of the local-development provider on production paths. Consider elevating this rule to an error in production CI:

```ini
dotnet_diagnostic.ASIB002.severity = error
```

## Suppression

Prefer fixing the flow. When a host application owns a custom persistence, outbox, or production-review abstraction the analyzer cannot see, use one of the normal Roslyn suppression mechanisms:

- `#pragma warning disable ASIB001`
- `#pragma warning disable ASIB002`
- `.editorconfig` severity configuration
- `SuppressMessageAttribute`

`ASIB001` also honors a host-defined marker attribute named `AsiBackbonePersistenceHandledAttribute` on the containing method or type. The package does not ship that marker as a runtime contract; hosts that want this pattern may define it in their own application or shared infrastructure project.

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
internal sealed class AsiBackbonePersistenceHandledAttribute : Attribute;
```

`ASIB002` honors a host-defined marker attribute named `AsiBackboneProductionConfigurationReviewedAttribute` on the containing method or type for rare cases where the host intentionally demonstrates or rejects a production-local-development path and has reviewed the risk.

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
internal sealed class AsiBackboneProductionConfigurationReviewedAttribute : Attribute;
```

## Non-goals

- Compile-time proof of transactional correctness.
- Replacement for durable audit storage, outbox persistence, transaction tests, or integration tests.
- Runtime enforcement or execution gating.
- Production key custody, tamper-evidence, legal non-repudiation, compliance certification, or managed-key security.
- EF Core, ASP.NET Core, cloud, robotics, or provider-specific dependencies.
