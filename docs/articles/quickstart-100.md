# 1.0.0 Quickstart

This quickstart shows the minimum supported setup for the initial stable `1.0.0` release line.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone provides governance-oriented software primitives for policy-shaped, auditable, acknowledgment-aware, and capability-bounded decision flow. It is not an intelligence engine and does not host, train, run, or orchestrate AI models.

## Stable `1.0.0` scope

The initial stable path focuses on public package consumption for the implemented package family:

```text
AsiBackbone.Core
AsiBackbone.Storage.InMemory
AsiBackbone.EntityFrameworkCore
AsiBackbone.AspNetCore
```

The minimum supported setup is `AsiBackbone.Core`. Add integration packages only when the host application needs them.

| Need | Package |
| --- | --- |
| Framework-neutral decisions, constraints, audit residue, handshakes, and capability-token primitives | `AsiBackbone.Core` |
| Non-durable local validation or sample audit storage | `AsiBackbone.Storage.InMemory` |
| Host-owned EF Core audit ledger persistence | `AsiBackbone.EntityFrameworkCore` |
| ASP.NET Core request correlation, actor context, result mapping, and acknowledgment challenge helpers | `AsiBackbone.AspNetCore` |

## Install the minimum package

For a console app, worker, library, or existing service that only needs framework-neutral governance primitives:

```bash
dotnet add package AsiBackbone.Core
```

For local validation with non-durable in-memory audit storage:

```bash
dotnet add package AsiBackbone.Storage.InMemory
```

For ASP.NET Core host integration:

```bash
dotnet add package AsiBackbone.AspNetCore
```

For host-owned EF Core persistence:

```bash
dotnet add package AsiBackbone.EntityFrameworkCore
```

## Basic public API example

The following example uses only public Core APIs. It creates a host-defined constraint, evaluates a request, and produces audit residue from the decision.

```csharp
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;

var evaluator = DefaultAsiBackbonePolicyEvaluator.CreateBuilder<AsiBackboneConstraintEvaluationContext>()
    .AddConstraint(new RegionRequiredConstraint())
    .Build();

var context = new AsiBackboneConstraintEvaluationContext(
    correlationId: Guid.NewGuid().ToString("N"),
    policyVersion: "policy-v1",
    policyHash: "policy-hash-v1",
    metadata: new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["region"] = "US-LA",
        ["risk"] = "routine"
    });

GovernanceDecision decision = await evaluator.EvaluateAsync(context);

AuditResidue residue = AuditResidue.FromDecision(
    AsiBackboneActorContext.Human("user-123", "Example User"),
    "example.document.approve",
    decision,
    metadata: context.Metadata);

Console.WriteLine($"Decision: {decision.Outcome}");
Console.WriteLine($"Can proceed: {decision.CanProceed}");
Console.WriteLine($"Audit event: {residue.EventId}");

internal sealed class RegionRequiredConstraint : IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>
{
    public string Name => "region.required";

    public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
        AsiBackboneConstraintEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool hasRegion = context.Metadata.TryGetValue("region", out string? region)
            && !string.IsNullOrWhiteSpace(region);

        return ValueTask.FromResult(hasRegion
            ? ConstraintEvaluationResult.Allow()
            : ConstraintEvaluationResult.Deny(
                "region.missing",
                "A region is required before this operation can proceed."));
    }
}
```

This example does not execute the requested operation. It only evaluates the request and creates audit residue. The consuming application remains responsible for deciding whether and how to execute the underlying operation.

## Optional in-memory audit storage

For local validation, tests, or sample hosts, write audit residue to `InMemoryAuditLedger`:

```csharp
using AsiBackbone.Core.Audit;
using AsiBackbone.Storage.InMemory.Audit;

var ledger = new InMemoryAuditLedger();
IAsiBackboneAuditSink sink = ledger;

await sink.WriteAsync(residue);

IAsiBackboneAuditResidue stored = ledger.Records.Single();
Console.WriteLine(stored.CorrelationId);
```

`InMemoryAuditLedger` is non-durable. It is intended for local validation, tests, examples, and simple smoke checks. Production hosts should use a durable host-owned persistence strategy.

## Optional ASP.NET Core service registration

For ASP.NET Core hosts, register the thin host adapter package:

```csharp
using AsiBackbone.AspNetCore.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddAsiBackboneAspNetCore();

WebApplication app = builder.Build();
```

The ASP.NET Core package provides integration seams. It does not register authentication, authorization, MVC, Razor Pages, Minimal API endpoints, EF Core, policy evaluators, or operational execution behavior for the host.

## Optional EF Core host-owned persistence

For EF Core persistence, the host application owns its `DbContext`, provider, connection string, migrations, deployment, and schema lifecycle. AsiBackbone contributes model configuration helpers.

```csharp
using AsiBackbone.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        _ = modelBuilder.ApplyAsiBackboneConfigurations();
    }
}
```

See [EF Core Host Ownership and Migrations](ef-core-host-ownership-and-migrations.md) before using durable persistence in a real host application.

## `1.0.0` versus later provider work

The `1.0.0` release path is intended to stabilize the public package surface for the implemented governance spine: Core primitives, in-memory validation storage, ASP.NET Core host adapters, and EF Core host-owned persistence.

Later release lines have added several capabilities as stable package surfaces. This `1.0.0` quickstart remains a historical minimum-path guide. For the current release line, see [Getting Started](getting-started.md), [First 15 Minutes: Standard API Gating](quickstart-api-gating.md), and [Progressive Adoption Ladder](progressive-adoption.md).

Those later provider packages should not be treated as required for the `1.0.0` quickstart. Later package areas should be considered stable only for the release lines where they completed their own API review, package boundary review, and release documentation.

## Next steps

- Read [API Compatibility and SemVer](api-compatibility-and-semver.md) for the stable-release compatibility promise.
- Read [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md) for metadata, identifier, signing-ready field, and host-responsibility guidance.
- Read [Policy Evaluator Pipeline](policy-evaluator-pipeline.md) for the decision-evaluation model.
- Read [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md) for web-host adapter expectations.
- Read [Schema Versioning](schema-versioning.md) for durable artifact schema guidance.
