# AsiBackbone Aspire AppHost Sample

This sample explores a local .NET Aspire orchestration path for the existing `samples/PlainAspNetCoreHost` governed ASP.NET Core API.

It is intentionally a **sample**, not a new stable package. The current repository boundaries make an AppHost sample the safest first step because Aspire should remain optional and isolated from Core and the stable package family.

## What this sample runs

The AppHost launches:

- `asi-backbone-api` — the existing Plain ASP.NET Core host sample.

The governed API demonstrates:

- policy evaluation through `GovernanceDecision`,
- acknowledgment-required sample decisions,
- in-memory decision receipt,
- host-owned SQLite ledger persistence,
- local-development signing and verification,
- endpoint-governance metadata for Minimal API and controller routes.

## Prerequisites

- .NET SDK matching the repository `global.json`.
- A browser for the Aspire dashboard.

The AppHost uses the Aspire SDK and hosting package through NuGet references. It does not require installing the deprecated Aspire workload.

## Run locally

From the repository root:

```powershell
dotnet run --project samples/AsiBackboneAspireAppHost/AsiBackbone.Samples.AspireAppHost.csproj
```

The Aspire dashboard should open during local development. Select the `asi-backbone-api` resource and use its endpoint link.

Useful governed API paths:

```http
GET /sample/decision
GET /sample/audit/{correlationId}
GET /sample/ledger/{correlationId}
POST /sample/ergonomic/minimal
POST /sample/ergonomic/controller
```

## What to look for in the dashboard

Use the dashboard to inspect the local resource and console output for the governed API. The sample API response itself remains the primary governance evidence path because it returns the governance decision, reason codes, audit event identifier, ledger record identifier, canonical hash, local-development signing metadata, and verification status.

Future Aspire-oriented work may add deeper OpenTelemetry governance-emission wiring once the desired sample topology is stable.

## Refreshing the NuGet lock files

This project commits two lock files instead of one:

- `packages.win-x64.lock.json`
- `packages.linux-x64.lock.json`

The Aspire AppHost SDK adds host-specific `Aspire.Dashboard.Sdk.<rid>` and `Aspire.Hosting.Orchestration.<rid>` packages, so the restore graph differs by operating system. The project selects its lock file with `NuGetLockFilePath` set to `packages.$(NETCoreSdkRuntimeIdentifier).lock.json`. Windows development uses the `win-x64` file, and the Linux CI runners use the `linux-x64` file under `--locked-mode`.

Refresh both files whenever this project's restore graph changes. Common causes are an `Aspire.AppHost.Sdk` version change in the project file, a `PackageVersion` change in `Directory.Packages.props` for any package this project restores (including centrally pinned transitive packages such as `MessagePack`), or a project reference change. Updating only the file for your own platform passes locally but fails CI with `NU1004`.

From the repository root on any platform, restore the AppHost project once per runtime identifier. Overriding `NETCoreSdkRuntimeIdentifier` makes the SDK resolve that platform's packages and write that platform's lock file:

```powershell
dotnet restore samples/AsiBackboneAspireAppHost/AsiBackbone.Samples.AspireAppHost.csproj `
  -p:NETCoreSdkRuntimeIdentifier=win-x64 `
  --force-evaluate

dotnet restore samples/AsiBackboneAspireAppHost/AsiBackbone.Samples.AspireAppHost.csproj `
  -p:NETCoreSdkRuntimeIdentifier=linux-x64 `
  --force-evaluate
```

On Linux or macOS shells, replace the trailing backticks with backslashes.

Restore the AppHost project rather than `AsiBackbone.slnx`. The `-p:` override is global, so a solution-wide restore would apply the overridden runtime identifier to every project in the solution.

Then confirm the native platform still restores in locked mode and commit both lock files together:

```powershell
dotnet restore AsiBackbone.slnx --locked-mode
git add samples/AsiBackboneAspireAppHost/packages.win-x64.lock.json samples/AsiBackboneAspireAppHost/packages.linux-x64.lock.json
```

## Boundary notes

This sample does not:

- make Aspire a dependency of Core or any stable runtime package,
- introduce a `AsiBackbone.Aspire` package,
- require cloud Key Vault, managed keys, or production databases,
- require the deprecated Aspire workload,
- provide compliance, durability, production signing, or tamper-evidence by itself,
- replace host-owned execution, storage, deployment, monitoring, or operational policy.

The AppHost is local orchestration only. The governed API remains responsible for its own sample services and host-owned execution boundary.
