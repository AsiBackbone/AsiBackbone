# Roslyn Analyzers

`AsiBackbone.Analyzers` provides compile-time safety rails for host applications that use AsiBackbone governance primitives.

The analyzer package is intentionally separate from `AsiBackbone.Core`. Core remains framework-neutral and does not depend on Roslyn, ASP.NET Core, EF Core, observability providers, or storage providers.

## Intent

AsiBackbone keeps persistence, transaction boundaries, outbox behavior, signing key custody, verification paths, and execution ownership in the host application. That boundary is important, but it also means a host can accidentally create a governance artifact and then continue without preserving decision receipt, durable outbox state, acknowledgment evidence, capability-grant checks, production signing review, or a safe continuation path.

The analyzer package helps catch simple, recognizable mistakes at build time. It is developer ergonomics, not proof of correctness.

## Rule IDs and severity posture

| Rule | Default severity | Recommended posture | Summary |
| --- | --- | --- | --- |
| `ASIB001` | Warning | Advisory safety rail | Governance artifact created or returned and then discarded. |
| `ASIB002` | Warning | Elevate to error in production CI when local-development signing must be prohibited | Local-development signing is registered, instantiated, or passed through a production branch. |

The package does not make builds fail by default. Hosts that want build-breaking behavior should configure it explicitly:

```ini
dotnet_diagnostic.ASIB002.severity = error
```

Keep exploratory or workflow guidance as warnings unless the host has decided a rule represents a production release gate.

## ASIB001 - Persist or continue AsiBackbone governance artifact

`ASIB001` warns when a recognized governance artifact is created or returned and then discarded.

Examples of recognized artifacts include:

- `GovernanceDecision`
- `DecisionReceipt`
- `AuditLedgerRecord`
- `GovernanceOutboxEntry`
- `GovernanceEmissionEnvelope`
- `GovernanceEmissionResult`
- `CapabilityTokenGrant`
- `CapabilityGrantValidationResult`
- `CapabilityGrantUseResult`
- `LiabilityHandshakeAcknowledgment`

### Diagnostic examples

```csharp
GovernanceDecision.Allow();
```

```csharp
_ = GovernanceDecision.Allow();
```

Both examples create a governance decision and discard it. The analyzer cannot see durable audit persistence, outbox enqueue, a return path, or another host-owned continuation.

### Safer examples

```csharp
GovernanceDecision decision = GovernanceDecision.Allow();
return decision;
```

```csharp
GovernanceDecision decision = GovernanceDecision.Allow();
await auditLedger.AppendAsync(record, cancellationToken);
return decision;
```

The analyzer intentionally starts conservatively. It does not try to prove that every stored or returned artifact is durably persisted. Runtime tests, transaction tests, and integration tests still matter.

## ASIB002 - Do not wire local-development signing in production branches

`ASIB002` warns when a recognized local-development signing type is used under an explicit production environment branch.

Examples of recognized local-development types include:

- `LocalDevelopmentSigningService`
- `LocalDevelopmentSigningOptions`
- other public types under `AsiBackbone.Signing.LocalDevelopment`

### Diagnostic examples

```csharp
if (builder.Environment.IsProduction())
{
    builder.Services.AddSingleton<LocalDevelopmentSigningService>();
}
```

```csharp
if (environment.EnvironmentName == "Production")
{
    builder.Services.AddSingleton(LocalDevelopmentSigningOptions.Create());
}
```

The rule is intentionally narrow. It looks for a reliable static signal that the local-development provider is being wired under a production branch, such as `IHostEnvironment.IsProduction()` or `EnvironmentName == "Production"`. It does not warn merely because a non-test project references the local-development signing package, because many hosts keep sample, smoke-test, local-only, or documentation proof paths in non-test assemblies.

### Safer examples

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<LocalDevelopmentSigningService>();
}
```

```csharp
if (builder.Environment.IsProduction())
{
    builder.Services.AddSingleton<IGovernanceSigningService, HostOwnedManagedKeySigningService>();
}
```

For production-like environments, use a host-owned managed-key adapter, HSM-backed provider, cloud key client, or verification boundary that is reviewed as part of the host application's production security model.

## Suppression and custom host-owned persistence

Prefer fixing the flow. When a custom host architecture already handles persistence, safe continuation, or reviewed production configuration in a way the analyzer cannot recognize, use normal Roslyn suppression tools:

```csharp
#pragma warning disable ASIB001
GovernanceDecision.Allow();
#pragma warning restore ASIB001
```

Hosts may also configure severity in `.editorconfig`:

```ini
dotnet_diagnostic.ASIB001.severity = warning
dotnet_diagnostic.ASIB002.severity = error
```

or disable a rule for a specific project:

```ini
dotnet_diagnostic.ASIB001.severity = none
dotnet_diagnostic.ASIB002.severity = none
```

For custom persistence wrappers, `ASIB001` honors a host-defined marker attribute named `AsiBackbonePersistenceHandledAttribute` on the containing method or type.

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
internal sealed class AsiBackbonePersistenceHandledAttribute : Attribute;

[AsiBackbonePersistenceHandled]
public async Task ExecuteThroughCustomPipelineAsync()
{
    GovernanceDecision.Allow();
    await customGovernancePipeline.FlushAsync();
}
```

For rare reviewed production-configuration examples, `ASIB002` honors a host-defined marker attribute named `AsiBackboneProductionConfigurationReviewedAttribute` on the containing method or type.

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
internal sealed class AsiBackboneProductionConfigurationReviewedAttribute : Attribute;
```

These markers are name-based and host-owned. They are not runtime contracts shipped by Core.

## Non-goals

The analyzer package does not:

- make Core depend on Roslyn;
- require EF Core, ASP.NET Core, or any cloud provider;
- prove transactional correctness;
- replace durable audit storage, outbox persistence, transaction tests, or integration tests;
- provide production key custody or managed-key hardening;
- block builds by default unless the host chooses to treat analyzer warnings as errors;
- certify compliance or legal adequacy.

Use the analyzers as early-warning rails around common persistence, continuation, and production-configuration mistakes, not as the only safety mechanism.
