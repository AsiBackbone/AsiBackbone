# AsiBackbone 7.5.6 Consumer Verification Guide

This guide describes checks for the `7.5.6` package family. Perform artifact
verification against the published `v7.5.6` GitHub Release and NuGet packages
after publication; this guide alone is not evidence of publication.

## Confirm compatibility and package metadata

- Package IDs remain `AsiBackbone.*`, and the target remains `net10.0`.
- Managed assemblies retain `AssemblyVersion` `7.0.0.0`.
- Package and file versions are `7.5.6` and `7.5.6.0`.
- Public APIs, durable schemas, canonical wire values, documented defaults,
  shipped runtime dependency declarations, and runtime decision logic are
  unchanged from `7.5.5`.
- The packed `AsiBackbone.Analyzers` assembly references
  Microsoft.CodeAnalysis 5.9.0.0, matching the compiler in .NET SDK 10.0.404.

## Verify the published artifacts

Download an adopted package, its Sigstore bundle, and the release evidence:

```powershell
gh release download v7.5.6 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.5.6.nupkg*'
gh release download v7.5.6 --repo AsiBackbone/AsiBackbone --pattern 'release-evidence-manifest.json'
$sourceCommit = (Get-Content release-evidence-manifest.json -Raw | ConvertFrom-Json).sourceCommit

cosign verify-blob AsiBackbone.Core.7.5.6.nupkg `
  --bundle AsiBackbone.Core.7.5.6.nupkg.sigstore.json `
  --certificate-identity 'https://github.com/AsiBackbone/AsiBackbone/.github/workflows/publish-packages.yml@refs/tags/v7.5.6' `
  --certificate-oidc-issuer https://token.actions.githubusercontent.com `
  --certificate-github-workflow-repository AsiBackbone/AsiBackbone `
  --certificate-github-workflow-ref refs/tags/v7.5.6 `
  --certificate-github-workflow-sha $sourceCommit `
  --certificate-github-workflow-trigger push

gh attestation verify ./AsiBackbone.Core.7.5.6.nupkg --repo AsiBackbone/AsiBackbone
```

Confirm independently that `v7.5.6` points to `$sourceCommit`. Compare SHA-256
hashes with `release-evidence-manifest.json` and repeat for every adopted
package. A bundle covers the exact GitHub release build package. NuGet.org adds
its own repository signature, so verify a package downloaded from NuGet.org
with `dotnet nuget verify --all <package-path>`.

## Build and exercise a clean consumer

Create a clean `net10.0` project, install only the required `AsiBackbone.*`
packages at `7.5.6`, restore, build with warnings enabled, and run the host's
tests. Exercise persistence, signing, provider, and analyzer paths where
adopted. When adopting `AsiBackbone.Analyzers`, deliberately discard a
governance decision and confirm `ASIB001` is reported before correcting the
consumer.

Retain decision-before-side-effect tests and production-signing configuration
checks described in the
[7.5.5 Consumer Verification Guide](consumer-verification-755.md).
Consumers moving from `6.x` must first complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

See also:

- [7.5.6 Release Notes](release-notes-756.md)
- [7.5.6 Release Readiness Record](release-readiness-756.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
