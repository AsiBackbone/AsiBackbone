# AsiBackbone 5.1.0 Consumer Verification Guide

Use this guide to verify the `5.1.0` package family after publication. It does
not claim that the packages are NuGet-signed, independently audited, certified,
or reproducibly built by every consumer environment.

## Confirm the package source

Install release packages from the official NuGet source and confirm the package
owner and package ID before adoption.

Expected package IDs:

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

Verify that the selected version is exactly `5.1.0` and that no unexpected
package source overrides the configured NuGet source order.

## Confirm the compatibility boundary

For `5.1.0`, verify:

- target framework: `net10.0`;
- package version: `5.1.0`;
- assembly version: `5.0.0.0`;
- file version: `5.1.0.0`;
- repository URL: `https://github.com/AsiBackbone/AsiBackbone`; and
- public package IDs and namespaces remain in the `AsiBackbone.*` family.

No stable public API or runtime behavior change is included between `5.0.0` and
`5.1.0`. Rebuild consumers after updating package references and run the host's
normal policy, audit, acknowledgment, capability, outbox, signing, actor-context,
execution-accountability, and endpoint-governance tests as applicable.

Consumers moving from `4.x` must still follow the
[5.0.0 Migration Guide](upgrade-400-to-500.md), including its persisted-enum and
explicit-validation-options guidance.

## Verify the public API baseline

The committed `5.x` baseline was generated from the published `v5.0.0` source.
Release validation compares each managed stable package against that surface and
checks package dependency boundaries. Consumers needing a detailed surface audit
can inspect `eng/api-baseline/` and the
[API Baseline and Boundary Checks](api-baseline-and-boundary-checks.md) record.

The baseline is a compatibility guard, not a substitute for application-level
integration testing or a claim that every behavior is appropriate for every
host.

## Verify Source Link repository metadata

After the packages are available on NuGet, run:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 5.1.0
```

The validation confirms that each package exposes the expected repository type,
public repository URL, and non-empty repository commit value. The repository
commit should resolve to the source revision used for the published package.

Source Link metadata improves traceability but is not equivalent to package
signing or an independent supply-chain attestation.

## Inspect package contents

For higher-assurance adoption, download each `.nupkg` and `.snupkg`, retain
cryptographic hashes in the consumer's release record, and inspect:

- the `.nuspec` package ID and version;
- dependency versions and target-framework assets;
- embedded or linked README content;
- repository URL and commit metadata;
- symbols and source-document mappings;
- SBOM and provenance artifacts published with the release, where available; and
- the absence of unexpected executable tooling or package payloads.

## Download durable release evidence

The `v5.1.0` release exposes durable evidence generated for the shipped package subjects. Start with:

- [`sbom-manifest.json`](https://github.com/AsiBackbone/AsiBackbone/releases/download/v5.1.0/sbom-manifest.json), which maps every package to its SPDX SBOM and package/SBOM SHA-256 values;
- [`release-evidence-manifest.json`](https://github.com/AsiBackbone/AsiBackbone/releases/download/v5.1.0/release-evidence-manifest.json), which binds the durable assets to the release tag and source commit; and
- the [complete `v5.1.0` release asset list](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v5.1.0), which includes all eleven SPDX JSON files and the retained release notes.

These public release assets do not require GitHub Actions API authentication and do not expire with workflow-artifact retention.

After downloading a NuGet package and its corresponding SBOM, verify their workflow provenance:

```powershell
gh attestation verify ./AsiBackbone.Core.5.1.0.nupkg --repo AsiBackbone/AsiBackbone
gh attestation verify ./AsiBackbone.Core.5.1.0.spdx.json --repo AsiBackbone/AsiBackbone
```

Use the same command for every package or SBOM admitted by the consumer. GitHub resolves attestations from the subject digest; a repository-level attestations collection response is not the verification mechanism.

## Distinguish repository controls from package guarantees

The `5.1.0` release documents and automates repository rulesets, secret scanning,
push protection, security-advisory distribution, and branch retention. These are
maintainer and repository-host controls. They improve the traceability of the
release process but do not change the runtime package contract and should not be
interpreted as a consumer-environment security guarantee.

## Package-signing status

Current AsiBackbone NuGet packages are intentionally published without NuGet
package signing while the project is independently maintained.

The [NuGet Package Signing Decision Record](nuget-package-signing-decision.md)
records the 2026-09-12 decision, accepted residual risk, compensating controls,
mandatory 2027-03-31/`6.0.0` review boundary, and earlier re-evaluation triggers.

Do not interpret Source Link, SBOMs, provenance statements, GitHub release tags,
or public source availability as a signed-package guarantee. Consumers that
require signed packages should enforce that requirement in their own dependency
policy.

## Verify the release record

Compare the published packages with:

- the `v5.1.0` Git tag;
- the GitHub release and attached assets;
- the [5.1.0 Release Notes](release-notes-510.md);
- the [5.1.0 Release Readiness Record](release-readiness-510.md);
- `CHANGELOG.md`;
- `CITATION.cff` and `.zenodo.json`; and
- CI, public API, CodeQL, dependency-review, OpenSSF, workflow-security, OWASP,
  package-validation, documentation, SBOM, and provenance results associated
  with the final release commit.

A missing or inconsistent artifact should be investigated rather than silently
treated as equivalent evidence.

## Host responsibilities remain unchanged

Package verification does not replace host-owned authentication, authorization,
identity-claim trust, policy registration, execution enforcement, durable
storage, key custody, replay protection, monitoring, incident response, legal
review, or compliance interpretation.

AsiBackbone provides governance-oriented software primitives. The consuming
application remains responsible for deciding whether the package, its evidence,
and its operational controls meet the host's risk requirements.

## Related documentation

- [5.1.0 Release Notes](release-notes-510.md)
- [5.1.0 Release Readiness Record](release-readiness-510.md)
- [5.0.0 Migration Guide](upgrade-400-to-500.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
