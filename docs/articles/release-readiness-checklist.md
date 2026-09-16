# 1.1.0 Release Readiness Record

This document is retained as the historical release-candidate control sheet for the AsiBackbone `1.1.0` NuGet package release. It is not a live indication that `1.1.0` is still pending.

For future stable releases, use this record as a reusable checklist shape together with [Stable Release Validation](release-validation.md), then copy or adapt the checks for the new release version.

This record verifies the package family as governance infrastructure for accountable decision flow. It does not treat AsiBackbone as a legal/compliance guarantee, or production tamper-evident ledger provider.

## Stable package family recorded for 1.1.0

The `1.1.0` release covers the implemented packages below.

| Package | Stable role | Release boundary |
| --- | --- | --- |
| `AsiBackbone.Core` | Framework-neutral governance primitives, decisions, constraints, acknowledgments, audit residue, lifecycle events, durable outbox contracts, provider-neutral emission contracts, DLP/classification failure policy primitives, signing-ready metadata, canonical hashing/signing seams, and verification-policy primitives. | Core remains independent of ASP.NET Core, EF Core, cloud-provider SDKs, OpenTelemetry SDKs, robotics, AI model, and host-template dependencies. |
| `AsiBackbone.Storage.InMemory` | Non-durable in-memory storage helpers for tests, samples, local validation, lifecycle events, and outbox proof paths. | Not durable storage, not production audit storage, and not a compliance archive. |
| `AsiBackbone.EntityFrameworkCore` | EF Core model configuration and host-owned persistence helpers. | Host owns `DbContext`, provider, migrations, deployment, schema lifecycle, retention, access controls, backup, and recovery. |
| `AsiBackbone.AspNetCore` | ASP.NET Core host adapters for actor context, request correlation, HTTP result mapping, acknowledgment challenge flows, endpoint governance, and hosted outbox drain integration. | Does not own host authentication, authorization, routing, policy enforcement, persistence, UI, exporter configuration, key management, or execution behavior. |
| `AsiBackbone.Analyzers` | Roslyn analyzer safety rails for governance persistence and continuation flows. | Build-time guidance only; not runtime enforcement, compliance proof, or security control by itself. |
| `AsiBackbone.OpenTelemetry` | OpenTelemetry-friendly governance emission provider for projecting provider-neutral envelopes into .NET diagnostics. | Does not configure exporters and does not depend on Azure Monitor, Event Hubs, Purview, SIEM, AI, robotics, or cloud-provider SDK packages. |
| `AsiBackbone.Signing.LocalDevelopment` | Local-development RSA signing and verification for tests, samples, and wiring proof paths. | Not production key custody, managed-key signing, immutability, non-repudiation, or tamper-evidence. |
| `AsiBackbone.Signing.ManagedKey` | Provider-neutral managed-key signing adapter. | Host supplies the actual managed-key client, credentials, key operations, verification path, monitoring, and operational policy. |

Future Event Hubs, Purview, Azure-specific SDK adapters, robotics/physical execution, immutable storage, external anchoring, or additional provider packages remain outside the `1.1.0` stable contract unless separately reviewed and released as stable.

## Historical pre-tag hard gate

At the time of the `1.1.0` release candidate, the pre-tag gate expected the exact release-candidate commit to satisfy these checks before creating `v1.1.0`:

- `Directory.Build.props` resolves package version `1.1.0` with no preview suffix.
- `AssemblyVersion` remains `1.0.0.0` for compatible `1.x` binary identity.
- `FileVersion` is `1.1.0.0`.
- `CITATION.cff` and `.zenodo.json` report version `1.1.0`.
- `CHANGELOG.md` includes the `1.1.0` entry.
- `1.1.0 Release Notes` match the current package family.
- `README.md` describes the `1.1.0` stable package family and bounded implementation claims.
- `Validate-VersionConsistency.ps1` passes for the release candidate and for tag `v1.1.0` before publishing from the tag.
- `Validate-NuGetPackageMetadata.ps1` passes against generated `.nupkg` artifacts.
- `CI` passes on the release-candidate commit.
- `Stable Release Validation` passes on the release-candidate commit.
- `External Consumer Smoke Test` passes against package-shaped artifacts.
- `Publish Documentation` passes and the DocFX site reflects the `1.1.0` package boundary.
- No open release-blocking issues remain for the `1.1.0` milestone.
- Any intentionally deferred release-critical check is documented in this record, the release notes, or a follow-up issue before tagging.

For future releases, copy this section into a release-specific checklist and replace `1.1.0` / `v1.1.0` with the target version.

## Reusable local release-candidate validation

Before relying only on hosted workflows, a maintainer may run the following commands from a clean checkout of a release-candidate commit.

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
find ./src -name '*.csproj' -type f | sort | while read project; do
  dotnet pack "$project" --configuration Release --output artifacts/packages /p:ContinuousIntegrationBuild=true
done
```

Validate release identity and generated NuGet metadata:

```powershell
./scripts/Validate-VersionConsistency.ps1 -ExpectedVersion 1.1.0 -PackageDirectory artifacts/packages
./scripts/Validate-VersionConsistency.ps1 -ExpectedVersion 1.1.0 -TagName v1.1.0 -PackageDirectory artifacts/packages
./scripts/Validate-NuGetPackageMetadata.ps1 -ExpectedVersion 1.1.0 -PackageDirectory artifacts/packages
```

For future release candidates, replace `1.1.0` and `v1.1.0` with the new release version.

Run package-consumer smoke checks when the host environment can execute them:

```bash
bash ./eng/smoke-tests/external-consumer-smoke.sh
bash ./eng/smoke-tests/stable-package-integration-smoke.sh
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

Expected `1.1.0` package artifacts:

```text
AsiBackbone.Core.1.1.0.nupkg
AsiBackbone.Storage.InMemory.1.1.0.nupkg
AsiBackbone.EntityFrameworkCore.1.1.0.nupkg
AsiBackbone.AspNetCore.1.1.0.nupkg
AsiBackbone.Analyzers.1.1.0.nupkg
AsiBackbone.OpenTelemetry.1.1.0.nupkg
AsiBackbone.Signing.LocalDevelopment.1.1.0.nupkg
AsiBackbone.Signing.ManagedKey.1.1.0.nupkg
```

## Documentation checklist shape

For a future release-candidate checklist, confirm that:

- Root `README.md` describes the project as stable Accountable Systems Infrastructure and does not describe the package family as early alpha.
- Package README files describe their package-specific boundaries.
- `Storage.InMemory` is clearly non-durable and local-validation focused.
- EF Core docs preserve host-owned persistence language.
- ASP.NET Core docs preserve host-adapter language and do not imply automatic enforcement.
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

## Publish workflow gate

The `Publish AsiBackbone Packages` workflow must block package publication unless the validation-and-pack job succeeds.

Before using the workflow to publish to NuGet.org for a future release, confirm that the validation-and-pack job performs:

1. version metadata validation;
2. restore;
3. build;
4. formatting verification;
5. tests;
6. .NET tool restore;
7. DocFX build;
8. package creation;
9. generated package version validation;
10. generated NuGet metadata validation;
11. artifact upload.

The publish job should only run after those checks complete successfully.

## Tagging and publish sequence

Recommended sequence for future stable releases:

1. Merge the release-preparation PR into `main`.
2. Confirm release-blocking workflows pass on `main`.
3. Create the release tag from the exact validated commit.
4. Let tag-triggered package validation run.
5. Publish only after tag validation succeeds.
6. Confirm NuGet packages, GitHub release notes, documentation site, and Zenodo metadata are aligned.

## Related documentation

- [1.1.0 Release Notes](release-notes-110.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Schema Versioning](schema-versioning.md)
