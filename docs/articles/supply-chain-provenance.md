# Supply-Chain Provenance and Package SBOMs

This article explains the current supply-chain metadata posture for AsiBackbone NuGet package artifacts.

In this software project, **ASI** means **Accountable Systems Infrastructure**. Supply-chain metadata strengthens the release evidence around generated packages, but it does not change the runtime API surface and does not make the packages a compliance product by themselves.

## Current behavior

Package-producing workflows generate Software Bill of Materials (SBOM) artifacts for generated `.nupkg` files and, where GitHub artifact attestation is available for the workflow event, attach provenance attestations to the generated package and SBOM artifacts.

For stable tags, the package-publish workflow also copies the SBOMs, package-to-SBOM manifest, exact GitHub release notes, and a release-evidence checksum manifest to the GitHub release. Release assets are the durable, anonymously downloadable consumer distribution surface. GitHub Actions artifacts remain temporary workflow evidence and are not the archival source.

The current package supply-chain path is:

```text
Restore, build, test, docs
  -> pack NuGet packages
  -> validate package versions and NuGet metadata
  -> generate package SBOMs
  -> attest package and SBOM provenance where supported
  -> upload packages and SBOMs as workflow artifacts
  -> publish packages only after validation succeeds
  -> attach SBOMs, release notes, and checksums to the GitHub release
  -> verify every required release asset is present
```

## SBOM artifacts

The repository uses `scripts/New-NuGetPackageSbom.ps1` to generate SPDX 2.3 JSON documents from produced NuGet package artifacts.

For each `.nupkg`, the script:

- reads package identity and metadata from the embedded `.nuspec` file;
- records the package SHA-256 digest;
- records declared NuGet dependencies from the package metadata;
- emits one `*.spdx.json` SBOM file per package;
- emits `sbom-manifest.json`, which maps package files to SBOM files and hashes.

The generated SBOMs are package-artifact SBOMs, not a broad source repository inventory. They are intended to help downstream consumers inspect the package artifact, package identity, declared dependency relationships, and generated-package hash.

## Workflow and durable release artifacts

The workflows upload SBOM files as GitHub Actions artifacts next to the package artifacts.

| Workflow | Package artifact | SBOM artifact |
| --- | --- | --- |
| `CI` | `asi-backbone-packages` | `asi-backbone-package-sboms` |
| `Stable Release Validation` | `asi-backbone-release-validation-packages` | `asi-backbone-release-validation-package-sboms` |
| `Publish AsiBackbone Packages` | `asi-backbone-packages` | `asi-backbone-package-sboms` |

These artifacts are generated from the same workflow run that builds and validates the packages. Actions artifacts follow repository retention policy and may require GitHub authentication. They support diagnostics and job handoff, but consumers should use the non-expiring files attached to the public GitHub release.

Every stable release contains:

- one `<package-id>.<version>.spdx.json` file for every shipped package;
- `sbom-manifest.json`, including package and SBOM SHA-256 values;
- `asibackbone-<version>-release-notes.md`, copied from the published release body; and
- `release-evidence-manifest.json`, recording the tag, source commit, release-asset hashes, package hashes, and attestation verification command.

The package-publish workflow fails if it cannot load the published release, upload the evidence, or observe every required asset after upload.

CodeQL, dependency review, OWASP Dependency-Check, OpenSSF Scorecard, and workflow-security findings remain on GitHub's code-scanning and workflow-run surfaces, where their repository-relative locations and remediation state are tracked. They are not package build subjects and are not duplicated as release attachments. AsiBackbone does not publish a container image, so there is no release-specific Trivy container SARIF comparable to NetCoreApplicationTemplate's container release evidence.

## Artifact attestations

Package and SBOM provenance attestations are generated for non-pull-request workflow events where GitHub artifact attestation support is available.

The attested subjects are intentionally narrow:

- generated `.nupkg` files;
- generated package SBOM JSON files.

The workflows do not attest broad repository outputs, coverage reports, documentation output, or unrelated artifacts. This keeps the provenance boundary focused on package-release artifacts.

GitHub attestations are verified against a downloaded subject digest; the repository API is not a collection listing of every attestation. After downloading a package from NuGet or an SBOM from the GitHub release, verify it with:

```powershell
gh attestation verify ./AsiBackbone.Core.5.1.0.nupkg --repo AsiBackbone/AsiBackbone
gh attestation verify ./AsiBackbone.Core.5.1.0.spdx.json --repo AsiBackbone/AsiBackbone
```

Repeat the command for each package or SBOM being admitted. A successful provenance verification identifies the workflow and source repository that produced the subject; it is not a NuGet author or repository signature.

## NuGet package signing posture

NuGet package signing is intentionally deferred under the dated [NuGet Package Signing Decision Record](nuget-package-signing-decision.md).

Rationale:

- package signing requires a reviewed certificate or key custody model;
- certificate rotation and expiration policy need to be documented before adoption;
- CI secrets and environment protection should be reviewed before adding signing keys;
- local developer builds should not be forced to depend on production signing infrastructure;
- stable-release-only signing policy should be decided before signed and unsigned packages coexist.

Current packages should therefore be described as having workflow-generated SBOMs and GitHub artifact provenance where supported, not as maintainer-signed NuGet packages.

The decision record requires review by 2027-03-31 or before the first `6.0.0` release candidate, whichever occurs first, and defines mandatory early re-evaluation criteria. When signing is adopted, update `SECURITY.md`, stable release validation guidance, the active release-readiness record, release notes, and consumer verification guidance before publishing public claims that package artifacts are maintainer-signed, repository-signed, or Authenticode-signed.

## Consumer guidance

For a package release or validation run, consumers can review:

1. the generated `.nupkg` package artifact;
2. the matching `*.spdx.json` SBOM file;
3. `sbom-manifest.json` for package-to-SBOM mapping and hashes;
4. GitHub artifact attestations when the workflow event produced them;
5. NuGet package metadata and Source Link metadata after package publication.

For the current package family, the [5.1.0 Consumer Verification Guide](consumer-verification-510.md) provides durable asset links and copy/paste validation commands for package source, package IDs, package version, repository metadata, Source Link metadata, package SBOMs, provenance attestations, and deferred package-signing wording.

For released NuGet packages, Source Link metadata validation remains a separate post-publish check. SBOM/provenance metadata complements that check; it does not replace source review, package validation, vulnerability scanning, or organizational approval.

## Related documentation

- [5.1.0 Consumer Verification Guide](consumer-verification-510.md)
- [Stable Release Validation](release-validation.md)
- [NuGet Package Signing Decision Record](nuget-package-signing-decision.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Production Wording and Stable Signing Boundaries](production-wording-and-alpha-limitations.md)
- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md)
