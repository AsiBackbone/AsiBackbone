# Plain ASP.NET Core Host Sample

This sample is the canonical in-repository validation host for `AsiBackbone`.
It proves that AsiBackbone can be wired into a plain ASP.NET Core application without requiring `NetCoreApplicationTemplate` or any other host template.

## What this sample demonstrates

- ASP.NET Core service registration through `AddAsiBackboneAspNetCore()`.
- Host-owned policy evaluation using Core constraints and a host-defined decision policy.
- Host-owned EF Core infrastructure using a plain `DbContext` and SQLite provider.
- EF Core model contribution through `ApplyAsiBackboneConfigurations()`.
- In-memory decision receipt for local validation.
- EF Core audit ledger persistence through the host-owned `DbContext`.

## What this sample intentionally avoids

- It does not depend on `NetCoreApplicationTemplate`.
- It does not define a reusable application template.
- It does not host, train, or execute AI models.
- It does not implement robotics or physical execution.
- It does not make database-provider or migration choices for consuming applications.

`NetCoreApplicationTemplate` remains a useful optional external validation host, but this plain ASP.NET Core sample is the minimal baseline that proves AsiBackbone can run without it.

## Run the sample

From the repository root:

```powershell
dotnet run --project samples/PlainAspNetCoreHost/AsiBackbone.Samples.PlainAspNetCoreHost.csproj
```

Then open:

```text
GET /sample/decision
```

The endpoint builds a sample policy context, evaluates constraints, applies a decision policy, writes decision receipt to the in-memory ledger, and persists an audit ledger record through the host-owned EF Core context.

You can query the in-memory and EF Core audit paths by correlation identifier returned from `/sample/decision`:

```text
GET /sample/audit/{correlationId}
GET /sample/ledger/{correlationId}
```

## EF Core note

The sample uses SQLite for a local validation path because the repository already carries the EF Core SQLite package version centrally. The important boundary is that the host owns the `DbContext`, provider, connection string, migrations, and deployment lifecycle.

The sample `DbContext` calls:

```csharp
modelBuilder.ApplyAsiBackboneConfigurations();
```

That allows the AsiBackbone EF Core package to contribute model configuration without owning the database context.

## Relationship to NetCoreApplicationTemplate

AsiBackbone provides this plain ASP.NET Core sample as the canonical integration baseline. `NetCoreApplicationTemplate` is documented separately as an optional external validation host for developers who want to test AsiBackbone against a fuller enterprise-style application baseline.