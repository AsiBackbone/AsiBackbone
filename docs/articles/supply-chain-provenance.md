# Supply-Chain Provenance and Package SBOMs

This article explains the current supply-chain metadata posture for AsiBackbone NuGet package artifacts.

Supply-chain metadata strengthens the release evidence around generated packages, but it does not change the runtime API surface and does not make the packages a compliance product by themselves.

## Current behavior

Package-producing workflows generate Software Bill of Materials (SBOM) artifacts for generated `.nupkg` files and, where GitHub artifact attestation is available for the workflow event, attach provenance attestations to the generated package and SBOM artifacts.

For stable tags, the package-publish workflow also copies the SBOMs, package-to-SBOM manifest, exact GitHub release notes, and a release-evidence checksum manifest to the GitHub release. Release assets are the durable, anonymously downloadable consumer distribution surface. GitHub Actions artifacts remain temporary workflow evidence and are not the archival source.

For tag-push releases, the protected package-publish job also signs every exact `.nupkg` build package with a [Sigstore keyless signature](#sigstore-keyless-signatures-for-release-build-packages) and attaches one `<package-file>.sigstore.json` bundle per package to the GitHub release. These are blob signatures over the GitHub release build packages. They are not NuGet author signatures and do not change the [NuGet Package Signing Decision Record](nuget-package-signing-decision.md). Releases up to and including `7.5.3` predate this step and carry no Sigstore bundles.

The current package supply-chain path is:

```text
Restore, build, test, docs
  -> pack NuGet packages
  -> validate package versions and NuGet metadata
  -> generate package SBOMs
  -> attest package and SBOM provenance where supported
  -> upload packages and SBOMs as workflow artifacts
  -> in the protected publish job: sign each validated build package with Sigstore keyless signing
  -> verify every bundle against the exact signer identity, including tamper and untrusted-identity cases
  -> publish packages only after validation and signing succeed
  -> attach attested build packages, Sigstore bundles, SBOMs, release notes, and checksums to the GitHub release
  -> verify every required release asset and package attestation
  -> download the packages and bundles anonymously from the release and verify them again
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

- one attested `<package-id>.<version>.nupkg` build package for every shipped package;
- one `<package-id>.<version>.nupkg.sigstore.json` Sigstore bundle for every shipped build package (releases after `7.5.3`);
- one `<package-id>.<version>.spdx.json` file for every shipped package;
- `sbom-manifest.json`, including package and SBOM SHA-256 values;
- `asibackbone-<version>-release-notes.md`, copied from the published release body; and
- `release-evidence-manifest.json`, recording the tag, source commit, release-asset hashes, package hashes, Sigstore bundle files and hashes, the expected Sigstore signer identity and issuer, and the attestation and Sigstore verification commands.

The package-publish workflow fails if it cannot load the published release, upload the evidence, or observe every required asset after upload. It also fails when a package has no Sigstore bundle, a bundle has no package, a package or bundle digest differs from the recorded value, or a bundle does not verify for the exact signer identity.

CodeQL, dependency review, OWASP Dependency-Check, OpenSSF Scorecard, and workflow-security findings remain on GitHub's code-scanning and workflow-run surfaces, where their repository-relative locations and remediation state are tracked. They are not package build subjects and are not duplicated as release attachments. AsiBackbone does not publish a container image, so there is no release-specific Trivy container SARIF comparable to NetCoreApplicationTemplate's container release evidence.

## Artifact attestations

Package and SBOM provenance attestations are generated for non-pull-request workflow events where GitHub artifact attestation support is available.

The attested subjects are intentionally narrow:

- generated `.nupkg` files;
- generated package SBOM JSON files.

The workflows do not attest broad repository outputs, coverage reports, documentation output, or unrelated artifacts. This keeps the provenance boundary focused on package-release artifacts.

GitHub attestations are verified against a downloaded subject digest; the repository API is not a collection listing of every attestation. After downloading a build package and SBOM from the GitHub release, verify them with:

```powershell
gh release download v7.0.0 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.0.0.nupkg'
gh attestation verify ./AsiBackbone.Core.7.0.0.nupkg --repo AsiBackbone/AsiBackbone
gh attestation verify ./AsiBackbone.Core.7.0.0.spdx.json --repo AsiBackbone/AsiBackbone
```

Repeat the command for each package or SBOM being admitted. A successful provenance verification identifies the workflow and source repository that produced the subject; it is not a NuGet author or repository signature.

NuGet.org adds a repository signature during package ingestion, changing the
digest of the `.nupkg` it serves. The GitHub build attestation therefore applies
to the exact build package retained on the GitHub release. Verify a package
downloaded from NuGet.org separately with
`dotnet nuget verify --all <package-path>`.

## Sigstore keyless signatures for release build packages

### What is signed and by whom

On a tag push, the `Publish packages` job of `.github/workflows/publish-packages.yml` runs in the protected `package-publish` environment. Before it publishes to NuGet.org, it:

1. downloads the validated `.nupkg` build packages and confirms each digest against `sbom-manifest.json`;
2. installs Cosign through the SHA-pinned `sigstore/cosign-installer` action;
3. runs `cosign sign-blob --bundle <package-file>.sigstore.json` for every package, using the job's GitHub Actions OIDC token to obtain a short-lived Fulcio certificate and recording the signature in the Rekor transparency log;
4. verifies every bundle against the exact expected signer identity and confirms that a modified package, an untrusted workflow identity, and a bundle issued for another package are all rejected.

No long-lived signing key, signing secret, or `COSIGN_EXPERIMENTAL` setting is used. Only that job holds `id-token: write`; the release-evidence job verifies bundles but cannot sign.

The trusted signer is defined by these certificate values:

| Certificate value | Expected value |
| --- | --- |
| OIDC issuer | `https://token.actions.githubusercontent.com` |
| Certificate identity (SAN) | `https://github.com/AsiBackbone/AsiBackbone/.github/workflows/publish-packages.yml@refs/tags/<tag>` |
| Workflow repository | `AsiBackbone/AsiBackbone` |
| Workflow ref | `refs/tags/<tag>` |
| Workflow SHA | the release source commit recorded as `sourceCommit` in `release-evidence-manifest.json` |
| Workflow trigger | `push` |

Do not accept any other identity. A bundle that verifies only with a broad `--certificate-identity-regexp` or for another workflow, branch, or repository is not evidence of an AsiBackbone release.

### Verify a downloaded release package

The commands below need no GitHub credentials, so they work for anyone after the temporary workflow artifacts have expired. Replace the tag and package as needed.

```powershell
$tag = 'v<version>'
$version = $tag.Substring(1)
$package = "AsiBackbone.Core.$version.nupkg"
$base = "https://github.com/AsiBackbone/AsiBackbone/releases/download/$tag"

Invoke-WebRequest "$base/$package" -OutFile $package
Invoke-WebRequest "$base/$package.sigstore.json" -OutFile "$package.sigstore.json"
Invoke-WebRequest "$base/release-evidence-manifest.json" -OutFile release-evidence-manifest.json
$sourceCommit = (Get-Content release-evidence-manifest.json -Raw | ConvertFrom-Json).sourceCommit

cosign verify-blob $package `
  --bundle "$package.sigstore.json" `
  --certificate-identity "https://github.com/AsiBackbone/AsiBackbone/.github/workflows/publish-packages.yml@refs/tags/$tag" `
  --certificate-oidc-issuer https://token.actions.githubusercontent.com `
  --certificate-github-workflow-repository AsiBackbone/AsiBackbone `
  --certificate-github-workflow-ref "refs/tags/$tag" `
  --certificate-github-workflow-sha $sourceCommit `
  --certificate-github-workflow-trigger push
```

Confirm that `$sourceCommit` is the commit the release tag points to (for example with `git rev-parse "$tag^{commit}"` in a clone) rather than trusting the manifest value alone. A successful check prints `Verified OK` and exits with code `0`.

To confirm that your verifier is not accepting everything, verify a modified copy of the package with the same command and expect a non-zero exit code:

```powershell
Copy-Item $package tampered.nupkg
Add-Content tampered.nupkg 'x'
cosign verify-blob tampered.nupkg --bundle "$package.sigstore.json" `
  --certificate-identity "https://github.com/AsiBackbone/AsiBackbone/.github/workflows/publish-packages.yml@refs/tags/$tag" `
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

### External services, trust roots, and failure behavior

- Verification requires Cosign 3.x or later, which reads the Sigstore protobuf bundle format (`application/vnd.dev.sigstore.bundle.v0.3+json`).
- Cosign validates the bundle against the Sigstore public-good trusted root (Fulcio certificate authority, Rekor transparency log, and timestamp keys). It retrieves that trusted root through TUF from `tuf-repo-cdn.sigstore.dev` and caches it locally. The bundle already contains the certificate, signature, and transparency-log inclusion proof, so verification does not need to contact Fulcio or Rekor.
- For offline or air-gapped verification, obtain the Sigstore `trusted_root.json` through a separately trusted channel and pass it with `--trusted-root <path>`.
- Cosign exits non-zero, and the release must be rejected, when the package bytes differ from the signed digest, the certificate identity or any pinned workflow value differs, the issuer differs, the bundle is malformed or belongs to another package, or the trusted root cannot be obtained or does not validate the bundle.
- The package-publish workflow applies the same checks before NuGet publication and again after downloading the published release assets anonymously. A failure in either check fails the release workflow.

### Boundaries

- The bundle binds to the GitHub release build package. NuGet.org repository-signs packages during ingestion, so the `.nupkg` served by NuGet.org has a different digest and will not verify against the bundle. Verify a NuGet.org download with `dotnet nuget verify --all <package-path>`, or compare its contents with the GitHub release build package.
- A Sigstore bundle identifies the workflow run that signed a byte sequence. It is not NuGet author signing, NuGet repository signing, or Authenticode signing, and it does not attest any runtime governance decision, decision receipt, capability grant, or signing-provider outcome.
- Release-artifact signing is separate from the runtime signing APIs. `AsiBackbone.Signing.ManagedKey`, its key-version and revocation semantics, and provider-neutral runtime signing are unchanged. A runtime Sigstore adapter would need its own threat model and design review.
- Public transparency-log entries contain the workflow identity, repository, tag, commit, and package digests. They contain no governance decision records, actor identities, or receipt metadata.
- OpenSSF Scorecard may detect the `*.sigstore.json` release assets in its Signed-Releases check. Scorecard results are reviewed after publication; they are not a release acceptance condition.

## Distinct package trust signals

| Signal | What it binds | How to verify | What it does not mean |
| --- | --- | --- | --- |
| Sigstore keyless blob signature | Exact GitHub release `.nupkg` bytes to the `publish-packages.yml` workflow run for a release tag | `cosign verify-blob` with the pinned identity above | Not NuGet author signing; does not cover NuGet.org-served bytes |
| GitHub build-provenance attestation | Package and SBOM digests to the workflow run that built them | `gh attestation verify <file> --repo AsiBackbone/AsiBackbone` | Not a package signature |
| NuGet.org repository signature | Package bytes served by NuGet.org to NuGet.org | `dotnet nuget verify --all <package-path>` | Not an AsiBackbone maintainer or author signature |
| Native NuGet author signing | Package bytes to an AsiBackbone-controlled certificate | Not available; deferred | See the [NuGet Package Signing Decision Record](nuget-package-signing-decision.md) |

## NuGet package signing posture

NuGet package signing is intentionally deferred under the dated [NuGet Package Signing Decision Record](nuget-package-signing-decision.md).

Rationale:

- package signing requires a reviewed certificate or key custody model;
- certificate rotation and expiration policy need to be documented before adoption;
- CI secrets and environment protection should be reviewed before adding signing keys;
- local developer builds should not be forced to depend on production signing infrastructure;
- stable-release-only signing policy should be decided before signed and unsigned packages coexist.

Current packages should therefore be described as having workflow-generated SBOMs, GitHub artifact provenance where supported, and, for releases after `7.5.3`, Sigstore keyless signatures over the GitHub release build packages, not as maintainer-signed or author-signed NuGet packages.

The decision record requires review by 2027-03-31 or before the first `8.0.0` release candidate, whichever occurs first, and defines mandatory early re-evaluation criteria. When signing is adopted, update `SECURITY.md`, stable release validation guidance, the active release-readiness record, release notes, and consumer verification guidance before publishing public claims that package artifacts are maintainer-signed, repository-signed, or Authenticode-signed.

## Consumer guidance

For a package release or validation run, consumers can review:

1. the generated `.nupkg` package artifact;
2. the matching `*.spdx.json` SBOM file;
3. `sbom-manifest.json` for package-to-SBOM mapping and hashes;
4. GitHub artifact attestations when the workflow event produced them;
5. the `<package-file>.sigstore.json` bundle for each GitHub release build package, verified against the exact signer identity;
6. NuGet package metadata and Source Link metadata after package publication.

The [7.0.0 Consumer Verification Guide](consumer-verification-700.md) provides durable asset links and copy/paste validation commands for package source, package IDs, package version, repository metadata, Source Link metadata, package SBOMs, provenance attestations, behavior changes, and deferred package-signing wording.

For released NuGet packages, Source Link metadata validation remains a separate post-publish check. SBOM/provenance metadata complements that check; it does not replace source review, package validation, vulnerability scanning, or organizational approval.

## Related documentation

- [7.0.0 Consumer Verification Guide](consumer-verification-700.md)
- [Stable Release Validation](release-validation.md)
- [NuGet Package Signing Decision Record](nuget-package-signing-decision.md)
- [Central Transitive Pinning Decision Record](central-transitive-pinning-decision.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Production Wording and Stable Signing Boundaries](production-wording-and-alpha-limitations.md)
- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md)
