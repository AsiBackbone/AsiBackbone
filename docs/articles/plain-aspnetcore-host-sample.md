# Plain ASP.NET Core Host Sample

> [!NOTE]
> This is a compile-ready package integration sample. For vendor-neutral architecture teaching and standalone executable exercises, use [ASI Backbone Learning](https://asibackbone.github.io/Learning/) and its [Executable Samples](https://asibackbone.github.io/Learning/samples/).

The plain ASP.NET Core host sample is the canonical in-repository validation path for AsiBackbone.

It exists to prove that AsiBackbone integrates with a standard ASP.NET Core application without requiring `NetCoreApplicationTemplate` or another application template.

For a request/decision/audit walkthrough with representative JSON output, see [Reference Deployment: Plain ASP.NET Core Host Evidence](reference-deployment.md).

## Location

```text
samples/PlainAspNetCoreHost/
```

## Purpose

The sample demonstrates a minimal host-owned integration path:

```text
HTTP request
  -> host builds policy context
  -> Core evaluates constraints
  -> host decision policy may require acknowledgment
  -> audit residue is written
  -> EF Core ledger record is persisted by the host-owned DbContext
```

## Demonstrated package usage

The sample references the current package projects directly:

- `AsiBackbone.Core`
- `AsiBackbone.AspNetCore`
- `AsiBackbone.Storage.InMemory`
- `AsiBackbone.EntityFrameworkCore`

This keeps the sample close to source during development while preserving the same boundary expected from package consumers.

## Host-owned infrastructure

The sample owns the web host, `DbContext`, database provider, connection string, and runtime endpoints.

The AsiBackbone EF Core package contributes model configuration only:

```csharp
modelBuilder.ApplyAsiBackboneConfigurations();
```

That is the important boundary: AsiBackbone contributes persistence shape; the host owns infrastructure.

## Run the sample

From the repository root:

```powershell
dotnet run --project samples/PlainAspNetCoreHost/AsiBackbone.Samples.PlainAspNetCoreHost.csproj
```

Open the sample decision endpoint:

```text
GET /sample/decision
```

The response shows the governance decision, reason codes, correlation identifier, policy version, policy hash, audit event identifier, and ledger record identifier.

Use the returned correlation identifier to inspect audit records:

```text
GET /sample/audit/{correlationId}
GET /sample/ledger/{correlationId}
```

## Relationship to NetCoreApplicationTemplate

AsiBackbone provides the plain ASP.NET Core sample as the canonical integration baseline. `NetCoreApplicationTemplate` is documented separately as an optional external validation host for developers who want to test AsiBackbone against a fuller enterprise-style application baseline.

The external validation path is useful for compatibility checks, but it is not required by AsiBackbone and does not change the package dependency direction.

## Boundary notes

This sample does not:

- require `NetCoreApplicationTemplate`
- create a second application template
- implement ASI
- host AI models
- execute robotics or physical control flows
- choose production persistence, migrations, or deployment strategy for consuming applications

## Product API reference

Use the [Generated API Reference](../api-reference.md) for exact public types, members, and signatures exercised by the sample. The package documentation and released source remain authoritative for runtime behavior.
