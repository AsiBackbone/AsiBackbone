# AsiBackbone 3.2.2 Release Readiness Record

Release candidate date: 2026-08-22

## Release intent

`3.2.2` is a backward-compatible maintenance patch for the stable `3.2.x`
package family. Its scope is intentionally narrow: approved .NET dependency
updates, SHA-pinned CI/security/release-tooling maintenance, action-pin metadata
alignment, and repository line-ending hygiene accumulated after `3.2.1`.

No runtime source files changed relative to `v3.2.1`. This record is a pre-tag
checklist. Do not create `v3.2.2` or publish packages until required validation
has passed on the final release-candidate commit.

## Included scope

- Advance package and file version metadata to `3.2.2` while keeping
  `AssemblyVersion` `3.0.0.0`.
- Advance `CITATION.cff` and `.zenodo.json` release metadata.
- Advance template fallback package references to `3.2.2`.
- Advance the default Source Link post-publication verification version.
- Record EF Core family `10.0.11`,
  `Microsoft.Extensions.Logging.Abstractions` `10.0.11`, and
  `Microsoft.NET.Test.Sdk` `18.9.0` as the dependency-maintenance state.
- Carry forward current SHA-pinned CI, security, release-validation, and
  provenance workflow updates.
- Preserve the `.config/dotnet-tools.json` line-ending normalization without
  changing configured tool behavior.
- Add `3.2.2` release notes and consumer verification guidance.
- Update current-release documentation and DocFX navigation.

## Compatibility boundary

- Package IDs remain unchanged.
- Public namespaces remain unchanged.
- Public APIs remain unchanged.
- Runtime governance behavior remains unchanged.
- Target framework remains `net10.0`.
- `AssemblyVersion` remains `3.0.0.0`.
- `FileVersion` advances to `3.2.2.0`.
- No stable package boundary is added or removed.
- No durable governance artifact shape is intentionally changed.
- Capability-grant validation behavior introduced in `3.2.0` remains unchanged.
- Repository/documentation metadata from the historical `3.2.1` release remains
  unchanged.
- Authentication, authorization, durable replay protection, signing-key
  custody, persistence, external execution, monitoring, and operational safety
  remain host-owned responsibilities.
- NuGet package signing remains deferred while the project is independently
  maintained.

## Version and metadata checklist

- [ ] `Directory.Build.props` resolves package version `3.2.2`.
- [ ] `AssemblyVersion` remains `3.0.0.0`.
- [ ] `FileVersion` is `3.2.2.0`.
- [ ] `TargetFramework` remains `net10.0`.
- [ ] `RepositoryUrl` remains
  `https://github.com/AsiBackbone/AsiBackbone`.
- [ ] `PackageProjectUrl` remains
  `https://asibackbone.github.io/AsiBackbone/`.
- [ ] `CITATION.cff` reports version `3.2.2` and date `2026-08-22`.
- [ ] `.zenodo.json` reports version `3.2.2`.
- [ ] Template fallback package references use `3.2.2`.
- [ ] Source Link post-publication validation defaults to `3.2.2`.
- [ ] `CHANGELOG.md` contains the `3.2.2` entry dated `2026-08-22`.
- [ ] README and DocFX indexes identify `3.2.2` as the current patch release.
- [ ] DocFX navigation exposes the `3.2.2` release notes, readiness record, and
  consumer verification guide.
- [ ] Historical `3.2.1`, `3.2.0`, and earlier release records remain available
  for traceability.

## Dependency and workflow maintenance checklist

- [ ] EF Core package family resolves to `10.0.11`.
- [ ] `Microsoft.Extensions.Logging.Abstractions` resolves to `10.0.11`.
- [ ] `Microsoft.NET.Test.Sdk` resolves to `18.9.0`.
- [ ] Committed lock files are consistent with the final dependency graph.
- [ ] GitHub Actions remain pinned to immutable commit SHAs.
- [ ] Action version comments agree with pinned SHAs.
- [ ] CodeQL action references resolve to the intended `4.37.7` release.
- [ ] Provenance action references resolve to the intended `4.2.2` release.
- [ ] Zizmor action references resolve to the intended `0.6.2` release.
- [ ] `.config/dotnet-tools.json` does not appear modified after a clean checkout
  solely because of line-ending normalization.

## Required validation before tag

- [ ] Restore succeeds in locked mode.
- [ ] Default Debug solution build succeeds.
- [ ] Release solution build succeeds.
- [ ] `dotnet format --verify-no-changes` succeeds.
- [ ] All test projects pass.
- [ ] Repository-wide line-coverage gate passes.
- [ ] Package-specific coverage gates pass.
- [ ] Core branch-coverage gate passes.
- [ ] XML-documentation inventory validation passes.
- [ ] API baseline validation passes.
- [ ] API compatibility validation passes.
- [ ] Version consistency validation passes for `3.2.2`.
- [ ] Version consistency validation passes for tag `v3.2.2`.
- [ ] Package creation succeeds for the complete publishable package set.
- [ ] Generated package versions are exactly `3.2.2`.
- [ ] Generated dependency metadata is correct.
- [ ] Generated repository/project URL and repository-commit metadata are
  correct.
- [ ] Symbol packages and packaged README/icon assets are correct.
- [ ] Template smoke tests succeed against repository projects.
- [ ] Template smoke tests succeed against `3.2.2` fallback packages.
- [ ] External-consumer smoke tests succeed.
- [ ] Stable-package smoke tests succeed.
- [ ] Existing capability-validation profile tests continue to pass unchanged.
- [ ] DocFX build succeeds.
- [ ] Documentation release-claim validation succeeds.
- [ ] CodeQL reports no blocking findings.
- [ ] Dependency review reports no blocking findings.
- [ ] OpenSSF Scorecard has no unexplained blocking findings.
- [ ] actionlint/Zizmor checks succeed where configured.
- [ ] OWASP Dependency-Check has no unexplained blocking findings.
- [ ] Package SBOM artifacts are generated for the expected package set.
- [ ] Provenance artifacts are produced where supported.
- [ ] No package-signing claim is made for unsigned packages.

## Package verification expectations

Generated and published `3.2.2` packages should expose:

```text
Repository type:
git

Repository URL:
https://github.com/AsiBackbone/AsiBackbone

Project URL:
https://asibackbone.github.io/AsiBackbone/
```

The package repository commit value should identify the final source revision
used for the release.

## Package-signing posture

NuGet package signing remains intentionally deferred. Source Link, repository
commit metadata, SBOMs, provenance artifacts, release tags, and public source
are complementary traceability signals rather than substitutes for a NuGet
package signature.

## Release sequence

1. Apply and review the `3.2.2` release-preparation changes.
2. Regenerate or verify lock files as required by the final dependency graph.
3. Run the complete release-validation path on `release/3.2.2`.
4. Open the release pull request against `main`.
5. Resolve blocking CI, security, package, documentation, or metadata findings
   without weakening release criteria.
6. Merge only after required checks pass.
7. Confirm `main` contains the final `3.2.2` metadata and release documentation.
8. Create annotated tag `v3.2.2` from the validated release source commit.
9. Run the stable package release workflow.
10. Confirm expected NuGet and symbol packages are published as `3.2.2`.
11. Confirm GitHub release assets, SBOMs, and provenance artifacts are present
    where expected.
12. Confirm documentation deployment succeeds.
13. Run post-publication Source Link validation:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.2
```

14. Record any deferred or post-publication findings explicitly rather than
    weakening the published release claims.

## Related documentation

- [3.2.2 Release Notes](release-notes-322.md)
- [3.2.2 Consumer Verification Guide](consumer-verification-322.md)
- [Release Validation](release-validation.md)
- [Release Cadence and Readiness](release-cadence-and-readiness.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Supply-Chain Provenance and Package SBOMs](supply-chain-provenance.md)
