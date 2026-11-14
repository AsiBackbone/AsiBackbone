# AsiBackbone 7.5.4 Release Notes

Release date: 2026-11-14

`7.5.4` is a backward-compatible supply-chain and validation maintenance patch
for the stable `7.x` package family. It adds Sigstore keyless signatures for
GitHub release build packages, advances Aspire sample hosting, refreshes GitHub
Actions, and strengthens package compatibility validation. Package and file
versions advance to `7.5.4` and `7.5.4.0`; binary assembly identity remains
`7.0.0.0`.

## Release-artifact signatures

- The protected package-publish job signs each exact `.nupkg` build package
  with Sigstore keyless signing through GitHub Actions OIDC before NuGet
  publication.
- Each GitHub release includes one `<package-file>.sigstore.json` bundle per
  build package. Verification pins the repository, release tag, source commit,
  workflow path, trigger, and GitHub Actions OIDC issuer.
- Release evidence schema version 2 records every package and bundle digest
  plus the expected signer identity. Validation rejects modified packages,
  wrong workflow identities, and bundles exchanged between packages.
- These signatures cover GitHub release build packages. They are distinct from
  GitHub build provenance and NuGet.org repository signatures, and they are not
  NuGet author signing or runtime governance-record signing.

## Dependency, workflow, and compatibility maintenance

- `Aspire.Hosting.AppHost` advances from `13.6.4` to `13.6.5`, with both
  platform-specific AppHost lock files refreshed. This affects sample-host
  tooling, not dependencies declared by shipped `AsiBackbone.*` packages.
- Pinned GitHub Actions used for CI, release validation, documentation,
  dependency scanning, Scorecard analysis, and quality reports are refreshed.
- Package validation now compares against the published `7.5.3` packages. The
  obsolete suppressions for intentional 5.x-to-7.x breaks are removed, so the
  complete stable 7.x package surface receives direct binary-compatibility
  checks alongside the existing DocFX public API baseline.

## Compatibility

No stable public signature, source implementation, durable schema, canonical
wire value, package ID, public namespace, target framework, documented default,
declared runtime dependency minimum, or runtime decision behavior changes in
this patch. Managed assemblies retain `AssemblyVersion` `7.0.0.0`.

The Sigstore work changes release artifact production and verification only.
It does not change `AsiBackbone.Signing.ManagedKey`, runtime signing behavior,
or the decision to defer native NuGet author signing.

## Upgrade actions

1. Update adopted `AsiBackbone.*` packages to `7.5.4` together.
2. Restore and inspect the resolved dependency graph, rebuild with warnings
   enabled, and run the consumer's integration tests.
3. When admitting GitHub release build packages, verify their Sigstore bundles
   against the exact release tag, source commit, and protected workflow
   identity described in the consumer verification guide.

Consumers moving from `6.x` must still complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

## Release verification

Stable publication occurs only from the protected `v7.5.4` tag after the
release-preparation pull request is merged to `main` and required hosted checks
pass on the release candidate. The date above is the planned release date;
publication and hosted evidence must be verified separately.

See also:

- [7.5.4 Release Readiness Record](release-readiness-754.md)
- [7.5.4 Consumer Verification Guide](consumer-verification-754.md)
- [7.5.3 Release Notes](release-notes-753.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
