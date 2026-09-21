# AsiBackbone 7.0.0 Consumer Verification Guide

Use this guide to verify the `7.0.0` package family after publication. It does
not claim that the packages are author-signed, independently audited,
certified, or reproducibly built by every consumer environment.

## Confirm the package source

Install stable packages from the official NuGet source and verify the package
owner, package ID, and selected version before adoption. Expected package IDs
are:

- `AsiBackbone.Core`
- `AsiBackbone.DependencyInjection`
- `AsiBackbone.Storage.InMemory`
- `AsiBackbone.EntityFrameworkCore`
- `AsiBackbone.AspNetCore`
- `AsiBackbone.Testing`
- `AsiBackbone.Templates`
- `AsiBackbone.Analyzers`
- `AsiBackbone.OpenTelemetry`
- `AsiBackbone.Signing.LocalDevelopment`
- `AsiBackbone.Signing.ManagedKey`

Verify that the selected version is exactly `7.0.0`.

## Confirm the compatibility boundary

For `7.0.0`, verify:

- target framework: `net10.0`;
- package version: `7.0.0`;
- assembly and file versions: `7.0.0.0`;
- repository URL: `https://github.com/AsiBackbone/AsiBackbone`; and
- package IDs and public namespaces remain in the `AsiBackbone.*` family.

`7.0.0` is a major release with intentional binary and behavior breaks. Follow
the [Upgrade from 6.x to 7.0](upgrade-600-to-700.md) guide, rebuild every
dependent assembly, and run the host's governance, acknowledgment, persistence,
DLP, and endpoint tests as applicable.

## Check the behavior changes before adoption

- **Actor-bound acknowledgment:** the actor answering a challenge must have the
  same `ActorId` and `ActorType` as the actor that received it. Confirm actor
  resolution is stable across both request legs and handle
  `acknowledgment.challenge.actor_mismatch`.
- **DLP enum values:** `DlpFailureBehavior` and `DlpIntentRiskLevel` now use
  `Unspecified = 0`, shifting every former numeric value by one. Remap stored,
  serialized, transmitted, or numerically configured values; name-based values
  remain stable.
- **Incomplete DLP policy:** unassigned risk levels and behaviors now raise
  `ArgumentOutOfRangeException` instead of inheriting permissive behavior.

## Verify Source Link repository metadata

After the packages are available on NuGet, run:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 7.0.0
```

The repository commit should resolve to the tagged source revision used for the
published package.

## Verify durable release evidence

The `v7.0.0` GitHub release should expose stable build packages, matching SPDX
JSON SBOMs, `sbom-manifest.json`, `release-evidence-manifest.json`, and a
retained copy of the release notes.

```powershell
gh release download v7.0.0 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.0.0.nupkg'
gh attestation verify ./AsiBackbone.Core.7.0.0.nupkg --repo AsiBackbone/AsiBackbone
gh attestation verify ./AsiBackbone.Core.7.0.0.spdx.json --repo AsiBackbone/AsiBackbone
```

GitHub attestation verification is bound to the downloaded subject digest.

## Verify the NuGet.org distribution separately

NuGet.org repository-signs packages during ingestion, which changes the
`.nupkg` digest from the original attested build package retained on the GitHub
release. Verify a NuGet.org package separately:

```powershell
dotnet nuget verify --all ./AsiBackbone.Core.7.0.0.nupkg
```

Do not treat the GitHub build-package attestation and NuGet.org repository
signature as the same evidence.

## Package-author-signing status

AsiBackbone packages remain intentionally published without maintainer author
signing while the project is independently maintained. The
[NuGet Package Signing Decision Record](nuget-package-signing-decision.md)
defines the accepted residual risk, compensating controls, mandatory review
boundary, and early re-evaluation triggers.

Source Link, SBOMs, provenance statements, GitHub release tags, NuGet.org
repository signatures, retained release hashes, and public source availability
are useful but distinct signals.

## Verify the release record

Compare the published packages with:

- the `v7.0.0` Git tag and GitHub release;
- the [7.0.0 Release Notes](release-notes-700.md);
- the [7.0.0 Release Readiness Record](release-readiness-700.md);
- `CHANGELOG.md`, `CITATION.cff`, and `.zenodo.json`; and
- CI, API compatibility, package validation, documentation, SBOM, provenance,
  dependency, and workflow-security results for the final release commit.

A missing or inconsistent artifact should be investigated rather than silently
treated as equivalent evidence.

## Host responsibilities remain unchanged

Package verification does not replace host-owned authentication, authorization,
identity-claim trust, policy registration, execution enforcement, durable
storage, key custody, replay protection, monitoring, incident response, legal
review, or compliance interpretation.

## Related documentation

- [7.0.0 Release Notes](release-notes-700.md)
- [7.0.0 Release Readiness Record](release-readiness-700.md)
- [Upgrade from 6.x to 7.0](upgrade-600-to-700.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Supply-Chain Provenance and Package SBOMs](supply-chain-provenance.md)
