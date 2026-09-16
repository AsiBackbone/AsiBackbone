# NetCoreApplicationTemplate Host Validation

`NetCoreApplicationTemplate` can be used as an optional external local validation host for AsiBackbone.

This path is intentionally documentation-only. The canonical in-repository sample remains the [Plain ASP.NET Core Host Sample](plain-aspnetcore-host-sample.md). This article explains how to validate AsiBackbone against a fuller enterprise-style host baseline without making `NetCoreApplicationTemplate` a dependency of AsiBackbone.

## Boundary statement

AsiBackbone provides a plain ASP.NET Core sample as the canonical integration baseline. `NetCoreApplicationTemplate` is documented separately as an optional external validation host for developers who want to test AsiBackbone against a fuller enterprise-style application baseline.

## Repository layout

Use sibling local repositories during validation:

```text
/workspace/
  AsiBackbone/
  NetCoreApplicationTemplate/
```

The important rule is one-way consumption:

```text
NetCoreApplicationTemplate host
  -> references AsiBackbone packages or local AsiBackbone projects

AsiBackbone source projects
  -> do not reference NetCoreApplicationTemplate projects
```

## Recommended validation modes

### Package-reference mode

Use this mode when validating published or locally packed packages.

From the NetCoreApplicationTemplate host project, reference the packages needed for the validation path:

```powershell
dotnet add package AsiBackbone.Core
dotnet add package AsiBackbone.AspNetCore
dotnet add package AsiBackbone.Storage.InMemory
dotnet add package AsiBackbone.EntityFrameworkCore
```

Use package-reference mode to confirm that a consuming application can integrate AsiBackbone the same way an external developer would.

### Local project-reference mode

Use this mode while developing both repositories locally.

From the NetCoreApplicationTemplate host project, add references to the AsiBackbone source projects:

```powershell
dotnet add reference ..\AsiBackbone\src\AsiBackbone.Core\AsiBackbone.Core.csproj
dotnet add reference ..\AsiBackbone\src\AsiBackbone.AspNetCore\AsiBackbone.AspNetCore.csproj
dotnet add reference ..\AsiBackbone\src\AsiBackbone.Storage.InMemory\AsiBackbone.Storage.InMemory.csproj
dotnet add reference ..\AsiBackbone\src\AsiBackbone.EntityFrameworkCore\AsiBackbone.EntityFrameworkCore.csproj
```

Adjust the relative paths to match your local folder layout. The references should be added only to the NetCoreApplicationTemplate-generated host application or validation branch. Do not add a reference from any AsiBackbone project back to NetCoreApplicationTemplate.

## Service registration sketch

In the NetCoreApplicationTemplate host startup path, wire AsiBackbone like any other optional module:

```csharp
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.Core.Audit;
using AsiBackbone.EntityFrameworkCore.Audit;
using AsiBackbone.Storage.InMemory.Audit;
using Microsoft.EntityFrameworkCore;

builder.Services.AddAsiBackboneAspNetCore();

builder.Services.AddSingleton<InMemoryAuditLedger>();
builder.Services.AddSingleton<IDecisionReceiptSink>(serviceProvider =>
    serviceProvider.GetRequiredService<InMemoryAuditLedger>());

builder.Services.AddScoped<IGovernanceAuditLedgerStore, EfCoreAuditLedgerStore>();
```

If the host already has a `DbContext`, either register that context as `DbContext` for the validation path or adapt the ledger store registration to resolve the host-owned context explicitly.

```csharp
builder.Services.AddScoped<DbContext>(serviceProvider =>
    serviceProvider.GetRequiredService<ApplicationDbContext>());
```

The exact context name will depend on the generated host.

## Host-owned DbContext integration

In the NetCoreApplicationTemplate host `DbContext`, apply the AsiBackbone model configuration contribution:

```csharp
using AsiBackbone.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyAsiBackboneConfigurations();
    }
}
```

The host continues to own:

- database provider
- connection string
- migrations
- deployment strategy
- schema lifecycle
- operational data retention policy

AsiBackbone contributes model configuration and storage helpers only.

## Minimal validation endpoint sketch

Use a temporary endpoint or controller action in the NetCoreApplicationTemplate validation branch to prove decision and audit flow.

```csharp
app.MapGet("/asi-backbone/validation", async (
    HttpContext httpContext,
    IGovernancePolicyEvaluator<GovernanceEvaluationContext> evaluator,
    IDecisionReceiptSink auditSink,
    IGovernanceAuditLedgerStore ledgerStore,
    CancellationToken cancellationToken) =>
{
    var context = new GovernanceEvaluationContext(
        correlationId: httpContext.TraceIdentifier,
        policyVersion: "netcore-template-validation-v1",
        policyHash: "netcore-template-validation-hash",
        metadata: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = "NetCoreApplicationTemplate",
            ["validation"] = "external-local",
            ["risk"] = "consequential"
        });

    GovernanceDecision decision = await evaluator.EvaluateAsync(context, cancellationToken);

    DecisionReceipt receipt = DecisionReceipt.FromDecision(
        GovernanceActorContext.Human("validation-user", "Validation User"),
        "netcore-template.validation",
        decision,
        metadata: context.Metadata);

    await auditSink.WriteAsync(receipt, cancellationToken);

    AuditLedgerRecord record = AuditLedgerRecord.FromResidue(receipt);
    await ledgerStore.AppendAsync(record, cancellationToken);

    return Results.Ok(new
    {
        decision = decision.Outcome.ToString(),
        decision.ReasonCodes,
        decision.CorrelationId,
        decision.PolicyVersion,
        decision.PolicyHash,
        auditEventId = receipt.EventId,
        ledgerRecordId = record.RecordId
    });
});
```

This sketch assumes the host already registered an `IGovernancePolicyEvaluator<GovernanceEvaluationContext>` and any constraints or decision policy needed for the validation. The plain ASP.NET Core sample shows a minimal working pattern for those registrations.

## Validation checklist

A successful NetCoreApplicationTemplate validation should prove:

- the host app builds with AsiBackbone references
- `AddAsiBackboneAspNetCore()` can be called from the host startup path
- the host can define or register policy constraints
- the host can evaluate a governance decision
- decision receipt can be written to an in-memory validation ledger
- EF Core audit ledger records can be persisted through the host-owned `DbContext`
- the host owns database provider, migrations, and connection strings
- no AsiBackbone project references NetCoreApplicationTemplate

## Build and run checklist

From the NetCoreApplicationTemplate host repository:

```powershell
dotnet restore
dotnet build
```

Run the host according to that repository's normal local development process. Then call the temporary validation endpoint:

```text
GET /asi-backbone/validation
```

## Expected result

The endpoint should return a governance decision and audit identifiers. The exact outcome depends on the host-registered constraints and decision policy.

For a minimal validation, the response should prove these fields are flowing:

- `decision`
- `reasonCodes`
- `correlationId`
- `policyVersion`
- `policyHash`
- `auditEventId`
- `ledgerRecordId`

## Cleanup guidance

The NetCoreApplicationTemplate validation endpoint should remain local-only or development-only unless the consuming application intentionally exposes a supported diagnostics path.

Do not ship a temporary validation endpoint in production without host-owned authorization, logging, rate limiting, and exposure review.
