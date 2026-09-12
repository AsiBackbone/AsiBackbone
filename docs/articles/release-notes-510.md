# AsiBackbone 5.1.0 Release Notes

Release date: 2026-09-11

## Summary

`5.1.0` is a backward-compatible stabilization release for the stable `5.x`
AsiBackbone package family. It makes the package compatibility promise and the
repository's release, security, documentation, and stewardship controls easier
to verify and operate without changing runtime APIs or behavior.

Package IDs, public namespaces, and the `net10.0` target remain unchanged.
`AssemblyVersion` remains `5.0.0.0`; package and file versions advance to
`5.1.0` and `5.1.0.0` respectively. Consumers can update within the `5.x` line
without a migration step, then rebuild and run their normal host validation.

## Added

### Public API compatibility baselines

Every managed package in the stable `5.x` family now has a committed public API
baseline generated from `v5.0.0`. Release validation regenerates the DocFX
managed-reference surface, compares it with those baselines, and checks stable
package dependency boundaries. This turns the documented same-major
compatibility promise into an automated release gate.

`AsiBackbone.Templates` remains protected by package and template smoke tests
because it is a content-only package without a managed consumer assembly.

### Repository and security operations

The repository now includes:

- an auditable main-branch ruleset definition and dry-run-first management tool;
- documented secret-scanning and push-protection expectations;
- security-advisory distribution and CVE-request audit/apply tooling;
- a branch-retention policy with deterministic fixtures and tests; and
- public support-routing and maintainer-ownership policies.

These controls make repository operations reproducible and reviewable. They do
not replace GitHub-hosted enforcement, maintainer judgment, or consumer security
review.

## Changed

- Release validation now blocks on public API baseline and package-boundary
  checks.
- Documentation navigation separates current product guidance, release records,
  maintainer evidence, and historical material more clearly.
- Documentation-link and release-claim validation cover cross-repository links
  and stale current-version wording.
- Stable-package and external-consumer smoke tests use stricter package and
  cancellation-token handling.
- GitHub Pages workflows emit scanner-friendly paths and use the current pinned
  deployment action.
- Citation metadata retains the Zenodo concept DOI and removes stale
  version-specific DOI wording from evergreen metadata.

## Fixed

- Security-advisory distribution tooling now handles missing advisory endpoints
  consistently across PowerShell environments.
- `-WhatIf` correctly previews CVE-request actions without issuing mutations.
- Documentation and quality-report workflow findings now point to
  repository-relative source paths.

## Compatibility notes

- No stable public API additions, removals, or signature changes are included.
- No runtime behavior or durable artifact shape changes are included.
- Package IDs and public namespaces remain unchanged.
- The target framework remains `net10.0`.
- `AssemblyVersion` remains `5.0.0.0`; `FileVersion` advances to `5.1.0.0`.
- The `5.0.0` migration guide remains the required guidance for consumers moving
  from `4.x`; consumers already on `5.0.0` need no additional migration.

## Durable release evidence

The `v5.1.0` GitHub release retains all eleven attested build packages, all
eleven package SBOMs, `sbom-manifest.json`, `release-evidence-manifest.json`,
and a Markdown copy of these release notes as durable public assets. Consumers
can begin with the
[release asset list](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v5.1.0)
and use the commands in the
[5.1.0 Consumer Verification Guide](consumer-verification-510.md) to verify
package and SBOM provenance by subject digest.

For example:

```powershell
gh release download v5.1.0 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.5.1.0.nupkg'
gh attestation verify ./AsiBackbone.Core.5.1.0.nupkg --repo AsiBackbone/AsiBackbone
```

NuGet.org adds a repository signature during ingestion, which changes the
downloaded package digest. Use `dotnet nuget verify --all <package-path>` for
that NuGet-served distribution; use the command above for the exact attested
build package retained on the GitHub release.

GitHub Actions artifacts remain useful workflow evidence but are not the
archival distribution channel because their retention expires.

## Package signing posture

NuGet package signing remains intentionally deferred while AsiBackbone is
independently maintained. Consumers should use the official NuGet source, public
repository, release tag, Source Link repository metadata, SBOMs, provenance
artifacts where available, and their own retained package hashes as distinct
trust signals.

The dated [NuGet Package Signing Decision Record](nuget-package-signing-decision.md)
requires review by 2027-03-31 or before the first `6.0.0` release candidate,
whichever occurs first, and defines earlier event-driven review criteria.

## Validation

The release candidate should pass:

- locked restore and Debug/Release solution builds;
- formatting, analyzer, unit, integration, and property-based tests;
- public API baseline and stable package-boundary checks;
- package creation, metadata validation, SBOM generation, and smoke tests;
- version-consistency validation for `5.1.0` and `v5.1.0`;
- DocFX generation and documentation continuity/release-claim checks; and
- repository security, dependency, workflow, and supply-chain checks in CI.

After publication, validate Source Link repository metadata with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 5.1.0
```

See the [5.1.0 Consumer Verification Guide](consumer-verification-510.md) and
[5.1.0 Release Readiness Record](release-readiness-510.md) for the complete
consumer and maintainer checklists.
