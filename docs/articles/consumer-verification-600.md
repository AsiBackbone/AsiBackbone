# AsiBackbone 6.0.0 Consumer Verification Guide

Use this guide to verify the `6.0.0` package family after publication. It does
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

Verify that the selected version is exactly `6.0.0`.

## Confirm the compatibility boundary

For `6.0.0`, verify:

- target framework: `net10.0`;
- package version: `6.0.0`;
- assembly version: `6.0.0.0`;
- file version: `6.0.0.0`;
- repository URL: `https://github.com/AsiBackbone/AsiBackbone`; and
- package IDs and public namespaces remain in the `AsiBackbone.*` family.

`6.0.0` is a major release with intentional source, binary, and behavior
breaks. Follow the [Upgrade from 5.x to 6.0](upgrade-500-to-600.md) guide,
rebuild every dependent assembly, and run the host's normal governance, audit,
acknowledgment, capability, persistence, signing, and endpoint tests as
applicable.

## Check the behavior changes before adoption

Confirm the host is ready for these changes before deploying `6.0.0`:

- **Verification defaults:** pin mismatches, canonicalization mismatches, and
  missing signatures deny by default. Update alerting for the new failure codes
  `signature.provider-not-trusted` and `signature.policy-context-not-trusted`.
- **Signature input:** host `IManagedKeySigningClient` implementations must sign
  `ManagedKeySignRequest.SignatureInput`, and host
  `IGovernanceSignatureVerificationService` implementations must verify
  `SignatureVerificationRequest.SignatureInput`. Update signer and verifier
  together.
- **Historical artifacts:** artifacts signed by `5.x` providers verify only
  through a context created with
  `VerificationPolicyContext.WithLegacySignatureInputAllowed()`, and such
  signatures cannot satisfy a policy pin.
- **Persisted categories:** hosts that persist `SignatureVerificationCategory`
  as an integer must accept `UntrustedSigningContext = 12`.
- **In-memory use store:** set `EvictionGracePeriod` to at least the largest
  `AllowedClockSkew` used with the store.

## Verify Source Link repository metadata

After the packages are available on NuGet, run:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 6.0.0
```

The repository commit should resolve to the tagged source revision used for the
published package.

## Verify durable release evidence

The `v6.0.0` GitHub release should expose the stable build packages, matching
SPDX JSON SBOMs, `sbom-manifest.json`, `release-evidence-manifest.json`, and a
retained copy of the release notes.

For example:

```powershell
gh release download v6.0.0 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.6.0.0.nupkg'
gh attestation verify ./AsiBackbone.Core.6.0.0.nupkg --repo AsiBackbone/AsiBackbone
gh attestation verify ./AsiBackbone.Core.6.0.0.spdx.json --repo AsiBackbone/AsiBackbone
```

GitHub attestation verification is bound to the downloaded subject digest.

## Verify the NuGet.org distribution separately

NuGet.org repository-signs packages during ingestion. That changes the `.nupkg`
digest from the original attested build package retained on the GitHub release.

Verify a package downloaded from NuGet.org separately with:

```powershell
dotnet nuget verify --all ./AsiBackbone.Core.6.0.0.nupkg
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

- the `v6.0.0` Git tag;
- the GitHub release and attached durable assets;
- the [6.0.0 Release Notes](release-notes-600.md);
- the [6.0.0 Release Readiness Record](release-readiness-600.md);
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

- [6.0.0 Release Notes](release-notes-600.md)
- [6.0.0 Release Readiness Record](release-readiness-600.md)
- [Upgrade from 5.x to 6.0](upgrade-500-to-600.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Supply-Chain Provenance and Package SBOMs](supply-chain-provenance.md)
