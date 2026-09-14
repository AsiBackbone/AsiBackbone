# AsiBackbone 5.2.0 Consumer Verification Guide

Use this guide to verify the `5.2.0` package family after publication. It does
not claim that the packages are author-signed, independently audited,
certified, or reproducibly built by every consumer environment.

## Confirm the package source

Install stable packages from the official NuGet source and verify the package
owner, package ID, and selected version before adoption.

Expected package IDs are:

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

Verify that the selected version is exactly `5.2.0`.

## Confirm the compatibility boundary

For `5.2.0`, verify:

- target framework: `net10.0`;
- package version: `5.2.0`;
- assembly version: `5.0.0.0`;
- file version: `5.2.0.0`;
- repository URL: `https://github.com/AsiBackbone/AsiBackbone`; and
- package IDs and public namespaces remain in the `AsiBackbone.*` family.

The release adds a public evaluator-builder API but removes no stable public
members. Rebuild consumers after updating package references and run the host's
normal governance, audit, acknowledgment, capability, persistence, signing,
and endpoint tests as applicable.

Consumers moving from `4.x` must still follow the
[5.0.0 Migration Guide](upgrade-400-to-500.md).

## Check ASIB900 before 6.0

Five partial-argument
`DefaultAsiBackbonePolicyEvaluator<TContext>` constructors are obsolete in
`5.2.0` with diagnostic `ASIB900`.

If your build reports `ASIB900`, migrate to:

```csharp
DefaultAsiBackbonePolicyEvaluator.CreateBuilder<TContext>()
```

or use the all-dependencies constructor when appropriate.

The obsolete constructors still exist in `5.2.0`; their documented removal
target is `6.0`. See
[ASIB900: Obsolete Policy Evaluator Constructors](asib900-policy-evaluator-constructors.md).

## Verify Source Link repository metadata

After the packages are available on NuGet, run:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 5.2.0
```

The repository commit should resolve to the tagged source revision used for the
published package.

## Verify durable release evidence

The `v5.2.0` GitHub release should expose the stable build packages, matching
SPDX JSON SBOMs, `sbom-manifest.json`, `release-evidence-manifest.json`, and a
retained copy of the release notes.

For example:

```powershell
gh release download v5.2.0 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.5.2.0.nupkg'
gh attestation verify ./AsiBackbone.Core.5.2.0.nupkg --repo AsiBackbone/AsiBackbone
gh attestation verify ./AsiBackbone.Core.5.2.0.spdx.json --repo AsiBackbone/AsiBackbone
```

GitHub attestation verification is bound to the downloaded subject digest.

## Verify the NuGet.org distribution separately

NuGet.org repository-signs packages during ingestion. That changes the `.nupkg`
digest from the original attested build package retained on the GitHub release.

Verify a package downloaded from NuGet.org separately with:

```powershell
dotnet nuget verify --all ./AsiBackbone.Core.5.2.0.nupkg
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

- the `v5.2.0` Git tag;
- the GitHub release and attached durable assets;
- the [5.2.0 Release Notes](release-notes-520.md);
- the [5.2.0 Release Readiness Record](release-readiness-520.md);
- `CHANGELOG.md`;
- `CITATION.cff` and `.zenodo.json`; and
- CI, API compatibility, package validation, documentation, SBOM, provenance,
  dependency, and workflow-security results associated with the final release
  commit.

A missing or inconsistent artifact should be investigated rather than silently
treated as equivalent evidence.

## Host responsibilities remain unchanged

Package verification does not replace host-owned authentication, authorization,
identity-claim trust, policy registration, execution enforcement, durable
storage, key custody, replay protection, monitoring, incident response, legal
review, or compliance interpretation.

## Related documentation

- [5.2.0 Release Notes](release-notes-520.md)
- [5.2.0 Release Readiness Record](release-readiness-520.md)
- [ASIB900: Obsolete Policy Evaluator Constructors](asib900-policy-evaluator-constructors.md)
- [5.0.0 Migration Guide](upgrade-400-to-500.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Supply-Chain Provenance and Package SBOMs](supply-chain-provenance.md)
