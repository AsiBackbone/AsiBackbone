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

## Boundary notes

This sample does not:

- make Aspire a dependency of Core or any stable runtime package,
- introduce a `AsiBackbone.Aspire` package,
- require cloud Key Vault, managed keys, or production databases,
- require the deprecated Aspire workload,
- provide compliance, durability, production signing, or tamper-evidence by itself,
- replace host-owned execution, storage, deployment, monitoring, or operational policy.

The AppHost is local orchestration only. The governed API remains responsible for its own sample services and host-owned execution boundary.
