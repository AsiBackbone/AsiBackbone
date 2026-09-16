# 1.2.0 Release Readiness Record

This document is the release-candidate control sheet for the AsiBackbone `1.2.0` NuGet package-family release.

This record verifies the package family as governance infrastructure for accountable decision flow. It does not treat AsiBackbone as a legal/compliance guarantee, or production tamper-evident ledger provider.

## Stable package family prepared for 1.2.0

The `1.2.0` release covers the implemented packages below.

| Package | Stable role | Release boundary |
| --- | --- | --- |
| `AsiBackbone.Core` | Framework-neutral governance primitives, decisions, constraints, acknowledgments, audit residue, lifecycle events, durable outbox contracts, provider-neutral emission contracts, DLP/classification failure policy primitives, signing-ready metadata, canonical hashing/signing seams, and verification-policy primitives. | Core remains independent of ASP.NET Core, EF Core, cloud-provider SDKs, OpenTelemetry SDKs, robotics, AI model, and host-template dependencies. |
| `AsiBackbone.DependencyInjection` | Explicit `AddAsiBackbone(...)` builder facade for coordinating host-selected provider registrations. | Does not register hidden defaults or make Core own infrastructure. |
| `AsiBackbone.Storage.InMemory` | Non-durable in-memory storage helpers for tests, samples, local validation, lifecycle events, and outbox proof paths. | Not durable storage, not production audit storage, and not a compliance archive. |
| `AsiBackbone.EntityFrameworkCore` | EF Core model configuration and host-owned persistence helpers. | Host owns `DbContext`, provider, migrations, deployment, schema lifecycle, retention, access controls, backup, and recovery. |
| `AsiBackbone.AspNetCore` | ASP.NET Core host adapters for actor context, request correlation, HTTP result mapping, acknowledgment challenge flows, endpoint governance, development diagnostics, and hosted outbox drain integration. | Does not own host authentication, authorization, routing, policy enforcement, persistence, UI, exporter configuration, key management, or execution behavior. |
| `AsiBackbone.Testing` | Test-only harness helpers for deterministic endpoint governance and package wiring. | Test and validation surface only; not runtime enforcement or production control. |
| `AsiBackbone.Templates` | `dotnet new` templates for governed ASP.NET Core host scaffolding. | Developer-experience scaffold only; not a runtime dependency. |
| `AsiBackbone.Analyzers` | Roslyn analyzer safety rails for governance persistence, continuation flows, and production-signing configuration mistakes. | Build-time guidance only; not runtime enforcement, compliance proof, or security control by itself. |
| `AsiBackbone.OpenTelemetry` | OpenTelemetry-friendly governance emission provider for projecting provider-neutral envelopes into .NET diagnostics. | Does not configure exporters and does not depend on Azure Monitor, Event Hubs, Purview, SIEM, AI, robotics, or cloud-provider SDK packages. |
| `AsiBackbone.Signing.LocalDevelopment` | Local-development RSA signing and verification for tests, samples, and wiring proof paths. | Not production key custody, managed-key signing, immutability, non-repudiation, or tamper-evidence. |
| `AsiBackbone.Signing.ManagedKey` | Provider-neutral managed-key signing adapter. | Host supplies the actual managed-key client, credentials, key operations, verification path, monitoring, and operational policy. |

Future Event Hubs, Purview, Azure-specific SDK adapters, robotics/physical execution, immutable storage, external anchoring, or additional provider packages remain outside the `1.2.0` stable contract unless separately reviewed and released as stable.

## Pre-tag hard gate for 1.2.0

Before creating `v1.2.0`, the release-candidate commit should satisfy these checks:

- [x] `Directory.Build.props` resolves package version `1.2.0` with no preview suffix.
- [x] `AssemblyVersion` remains `1.0.0.0` for compatible `1.x` binary identity.
- [x] `FileVersion` is `1.2.0.0`.
- [x] `CITATION.cff` and `.zenodo.json` report version `1.2.0`.
- [x] `CHANGELOG.md` includes the `1.2.0` entry.
- [x] `1.2.0 Release Notes` match the current package family.
- [x] `README.md` describes the `1.2.0` stable package family and bounded implementation claims.
- [x] API compatibility / SemVer documentation describes `1.2.0` as a compatible minor release.
- [ ] `Validate-VersionConsistency.ps1` passes for the release candidate and for tag `v1.2.0` before publishing from the tag.
- [ ] `Validate-NuGetPackageMetadata.ps1` passes against generated `.nupkg` artifacts.
- [ ] `CI` passes on the release-candidate commit.
- [ ] `Stable Release Validation` passes on the release-candidate commit.
- [ ] `External Consumer Smoke Test` passes against package-shaped artifacts.
- [ ] `Publish Documentation` passes and the DocFX site reflects the `1.2.0` package boundary.
- [ ] No open release-blocking issues remain for the `1.2.0` milestone other than post-merge tag/publish verification.
- [ ] Any intentionally deferred release-critical check is documented in this record, the release notes, or a follow-up issue before tagging.

## Local release-candidate validation

Before relying only on hosted workflows, a maintainer may run the following commands from a clean checkout of the release-candidate commit.

```bash
dotnet restore AsiBackbone.slnx
dotnet build AsiBackbone.slnx --configuration Release
dotnet test AsiBackbone.slnx --configuration Release --no-build --no-restore
dotnet format AsiBackbone.slnx --verify-no-changes --verbosity minimal
dotnet tool restore
dotnet tool run docfx -- docs/docfx.json
```

Pack local release artifacts for inspection:

```bash
mkdir -p artifacts/packages
find ./src -name '*.csproj' -type f -not -path '*/templates/*' | sort | while read project; do
  dotnet pack "$project" --configuration Release --output artifacts/packages /p:ContinuousIntegrationBuild=true
done
```

Validate release identity and generated NuGet metadata:

```powershell
./scripts/Validate-VersionConsistency.ps1 -ExpectedVersion 1.2.0 -PackageDirectory artifacts/packages
./scripts/Validate-VersionConsistency.ps1 -ExpectedVersion 1.2.0 -TagName v1.2.0 -PackageDirectory artifacts/packages
./scripts/Validate-NuGetPackageMetadata.ps1 -ExpectedVersion 1.2.0 -PackageDirectory artifacts/packages
```

Run package-consumer smoke checks when the host environment can execute them:

```bash
bash ./eng/smoke-tests/external-consumer-smoke.sh
bash ./eng/smoke-tests/stable-package-integration-smoke.sh
bash ./eng/smoke-tests/template-package-smoke.sh
```

## Generated `.nupkg` metadata inspection

A release candidate should inspect actual packed `.nupkg` files, not only project files.

`Validate-NuGetPackageMetadata.ps1` validates:

- package ID casing;
- package version;
- package descriptions;
- package tags;
- MIT license metadata;
- project URL and repository URL metadata;
- README metadata;
- packaged README presence;
- package-specific README wording anchors.

Expected `1.2.0` package artifacts:

```text
AsiBackbone.Core.1.2.0.nupkg
AsiBackbone.DependencyInjection.1.2.0.nupkg
AsiBackbone.Storage.InMemory.1.2.0.nupkg
AsiBackbone.EntityFrameworkCore.1.2.0.nupkg
AsiBackbone.AspNetCore.1.2.0.nupkg
AsiBackbone.Testing.1.2.0.nupkg
AsiBackbone.Templates.1.2.0.nupkg
AsiBackbone.Analyzers.1.2.0.nupkg
AsiBackbone.OpenTelemetry.1.2.0.nupkg
AsiBackbone.Signing.LocalDevelopment.1.2.0.nupkg
AsiBackbone.Signing.ManagedKey.1.2.0.nupkg
```

## Documentation checklist

Confirm that:

- Root `README.md` describes the project as stable Accountable Systems Infrastructure and does not describe the package family as alpha or `1.1.x` current.
- Documentation home and article index describe `1.2.0` as the current stable package family.
- Package README files describe their package-specific boundaries.
- `Storage.InMemory` is clearly non-durable and local-validation focused.
- EF Core docs preserve host-owned persistence language.
- ASP.NET Core docs preserve host-adapter language and do not imply automatic enforcement.
- Testing docs describe test-harness guidance, not runtime enforcement.
- Template docs describe developer-experience scaffolding, not a runtime dependency.
- Analyzer docs describe build-time guidance, not runtime enforcement.
- OpenTelemetry docs describe provider projection, not exporter ownership.
- Signing docs avoid unsupported claims of production tamper-evidence, immutability, legal non-repudiation, or compliance certification.
- Release notes list the stable package family and accepted deferrals.
- `API Compatibility and SemVer` documents the stable package contract and `AssemblyVersion` strategy.
- `Schema Versioning` documents stable artifact schema and durable-artifact rationale.
- `Release Validation` documents the workflow gates and generated package metadata validation.

## Public wording checklist

Use bounded language such as:

- `Accountable Systems Infrastructure`;
- `governance spine`;
- `host-owned persistence`;
- `durable outbox baseline`;
- `provider-neutral governance emission`;
- `OpenTelemetry projection`;
- `signing-ready fields`;
- `provider signing boundary`;
- `verification policy`;
- `non-durable in-memory storage`;
- `test harness`;
- `template scaffold`;
- `host-owned execution`.

Avoid unsupported claims such as:

- `tamper-proof`;
- `tamper-evident`, unless implemented by a documented signing/storage path;
- `immutable`, unless backed by a concrete storage/anchoring design;
- `non-repudiation`;
- `compliance guarantee`;
- `legal certification`;
- production audit guarantees not backed by implementation;
- language implying artificial superintelligence implementation, AI model hosting, or robot/physical-system control.

## Tagging and publish sequence

Recommended sequence for the `1.2.0` stable release:

1. Merge the release-preparation PR into `main` after required checks pass.
2. Confirm release-blocking workflows pass on `main`.
3. Create the `v1.2.0` release tag from the exact validated commit.
4. Let tag-triggered package validation run.
5. Publish only after tag validation succeeds.
6. Confirm NuGet packages, GitHub release notes, documentation site, and Zenodo metadata are aligned.

## Related documentation

- [1.2.0 Release Notes](release-notes-120.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Schema Versioning](schema-versioning.md)
