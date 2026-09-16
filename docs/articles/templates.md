# dotnet new Templates

`AsiBackbone.Templates` removes blank-page adoption friction by generating a runnable ASP.NET Core host with AsiBackbone governance wiring already in place.

> [!IMPORTANT]
> The generated project is a local scaffold. It is not a production persistence design, production signing provider, compliance guarantee, or replacement for host-owned authentication, authorization, security review, deployment, monitoring, or execution controls.

## Install

```powershell
dotnet new install AsiBackbone.Templates
```

For local repository validation, install the generated `.nupkg` from the package artifact directory instead:

```powershell
dotnet new install ./artifacts/packages/AsiBackbone.Templates.3.0.0.nupkg
```

## Create a governed Web API

```powershell
dotnet new asibackbone-webapi -n MyGovernedApi --hostStyle plain
```

Then run the generated app:

```powershell
cd MyGovernedApi
dotnet restore
dotnet run
```

## Host-style selection

The template supports a `--hostStyle` choice parameter.

| Host style | Use when | Notes |
| --- | --- | --- |
| `plain` | You want the smallest standard ASP.NET Core Web API scaffold. | Uses direct package registration and ordinary ASP.NET Core project structure. |
| `netcoretemplate` | You want a generated host that can be adapted toward the NetCoreApplicationTemplate style. | Does not make NetCoreApplicationTemplate a dependency of AsiBackbone or the generated app. |

Both host styles currently generate the same runnable core project shape, with the selected style captured in `appsettings.json` and the generated README. The `netcoretemplate` style is intentionally a compatibility bridge and documentation cue rather than a hard dependency on the external template repository.

## Generated project contents

The generated project includes:

- `AddAsiBackboneAspNetCore()` registration;
- `UseAsiBackboneEndpointGovernance()` middleware;
- a sample governance constraint and decision policy;
- a Minimal API endpoint using fluent endpoint-governance metadata;
- a controller action using `[RequireGovernancePolicy]`, `[RequireCapabilityGrant]`, and `[EmitGovernanceAudit]`;
- non-durable `InMemoryAuditLedger` storage for local inspection;
- `AsiBackbone.Analyzers` as a development-time analyzer reference;
- a generated README with next steps and production-boundary notes.

## Useful generated endpoints

```http
GET /
GET /sample/decision
GET /sample/audit/{correlationId}
POST /sample/minimal/execute
POST /sample/controller/execute
```

The `/sample/decision` endpoint returns the decision outcome, reason codes, correlation ID, policy metadata, and audit event ID. Use the correlation ID with `/sample/audit/{correlationId}` to inspect the local in-memory decision receipt.

## Local defaults and production boundaries

The template intentionally favors safe local defaults:

| Concern | Template default | Production expectation |
| --- | --- | --- |
| Evaluator failure posture | Uses the `3.x` fail-closed evaluator defaults: empty policy denies, eligible ordinary constraint exceptions deny, and threat-contributor exceptions deny. | Keep this posture for governed production paths unless the host has a documented exception-to-audit boundary and intentionally opts out. |
| Audit storage | Non-durable in-memory audit ledger | Host-owned durable audit/outbox persistence when records must survive restart. |
| Capability validation | Sample scope check for `sample.execute` | Host-owned scope, expiry, replay, actor binding, and downstream authorization checks. |
| Signing | Not configured by default | Add a production signing/key-management provider only when the host has a concrete trust model. |
| Authentication and authorization | Not configured by default | Add normal ASP.NET Core auth and policy enforcement for protected hosts. |
| Execution | Sample response only | Host application owns real business execution and refusal behavior. |

The generated scaffold is meant to demonstrate the fail-closed decision boundary, not to claim production completeness. If a generated host intentionally sets `TreatConstraintExceptionAsDenial = false`, that should be treated as a local-validation or host-owned incident-boundary decision and documented in the generated application.

## CI validation

The repository validates the template package by packing local packages, installing the generated `AsiBackbone.Templates` package, generating both supported host styles, asserting the generated `.csproj` fallback `PackageReference` versions match the repository package version, restoring against the local package output plus NuGet, and building each generated project.

This proves the template can be installed and used from a clean directory without making Core depend on template infrastructure or NetCoreApplicationTemplate.

## Related documentation

- [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)
- [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
- [Strict Governance Profile](strict-governance-profile.md)
- [Testing Harness](testing-harness.md)
- [NetCoreApplicationTemplate Host Validation](netcoreapplicationtemplate-host-validation.md)
- [3.0.0 Release Notes](release-notes-300.md)
