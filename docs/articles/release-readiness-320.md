# AsiBackbone 3.2.0 Release Readiness Record

Release candidate date: 2026-08-02

## Release intent

`3.2.0` is a backward-compatible minor release for the stable `3.x` package family. It packages the explicit capability-grant validation profile work and post-`3.1.0` dependency/workflow maintenance while preserving the established package, namespace, target-framework, binary-identity, and host-ownership boundaries.

This record is a pre-tag checklist. Do not create `v3.2.0` or publish packages until every required validation is complete on the final release-candidate commit.

## Included scope

- Add `CapabilityGrantValidationOptions.CreateExecutionBoundary(...)` for consequential execution boundaries.
- Require proof verification in the execution-boundary profile.
- Enable bounded-use validation by default with `maxUseCount: 1`.
- Allow an explicit bounded-use opt-out when another trusted boundary owns replay/use enforcement.
- Add `CapabilityGrantValidationOptions.CreateMetadataValidation(...)` for intentional metadata-only validation.
- Preserve the existing configurable `Create(...)` path and no-options validator behavior for `3.x` compatibility.
- Add focused tests and migration guidance for the new validation profiles.
- Update SQLitePCLRaw and repository GitHub Actions dependencies.
- Carry forward the quality-report PowerShell environment-variable fix merged after `3.1.0`.

## Compatibility boundary

- Package IDs remain unchanged.
- Public namespaces remain unchanged.
- The target framework remains `net10.0`.
- `AssemblyVersion` remains `3.0.0.0` for the compatible `3.x` binary line.
- `FileVersion`, package version, informational version, citation metadata, and release metadata advance to `3.2.0`.
- The new capability-grant validation profile APIs are additive.
- Existing callers using `CapabilityGrantValidationOptions.Create(...)` retain their current settings and defaults.
- Existing callers using `ValidateAsync(signedGrant)` without explicit options retain the current `3.x` metadata-oriented behavior.
- NuGet package signing remains deferred while the project is independently maintained.

## Version and metadata checklist

- [ ] `Directory.Build.props` resolves package version `3.2.0`.
- [ ] `AssemblyVersion` remains `3.0.0.0`.
- [ ] `FileVersion` is `3.2.0.0`.
- [ ] `CITATION.cff` reports version `3.2.0` and the release date.
- [ ] `.zenodo.json` reports version `3.2.0` and minor-release scope.
- [ ] Template fallback package references use `3.2.0`.
- [ ] Source Link post-publication validation defaults to `3.2.0`.
- [ ] Lock files are regenerated after the version bump and locked restore succeeds.
- [ ] `CHANGELOG.md` and release notes describe the same change set and compatibility boundary.
- [ ] Evergreen documentation identifies `3.2.0` as the current minor release without rewriting historical release records.

## Required validation before tag

- [ ] Restore succeeds in locked mode using the repository SDK and package configuration.
- [ ] Debug solution build succeeds.
- [ ] Release solution build succeeds.
- [ ] `dotnet format --verify-no-changes` succeeds.
- [ ] All test projects pass.
- [ ] Repository-wide line-coverage gate passes.
- [ ] Package-specific coverage gates pass.
- [ ] Core branch-coverage gate passes.
- [ ] XML-documentation inventory ceiling passes.
- [ ] API baseline and compatibility checks pass.
- [ ] Version consistency validation passes for `3.2.0` and tag `v3.2.0`.
- [ ] Package creation succeeds for the complete publishable package set.
- [ ] Generated package IDs, versions, dependencies, repository metadata, symbols, and README content are correct.
- [ ] Template smoke tests succeed against repository projects and package fallback references.
- [ ] External-consumer and stable-package smoke tests succeed.
- [ ] Capability-validation profile tests pass.
- [ ] DocFX build and documentation release-claim validation succeed.
- [ ] CodeQL and dependency review report no blocking findings.
- [ ] OpenSSF Scorecard, workflow-security, actionlint/Zizmor, and OWASP Dependency-Check results have no unexplained blocking findings.
- [ ] Reviewed OWASP suppressions remain narrowly scoped, documented, and unexpired.
- [ ] SBOM and provenance artifacts are produced where supported.
- [ ] No package-signing claim is made for unsigned packages.

## Release sequence

1. Confirm the prepared `3.2.0` entry is present in `CHANGELOG.md` and consistent with the release notes.
2. Regenerate and commit all NuGet lock files after the central package-version change.
3. Merge the release-preparation pull request after required checks pass.
4. Confirm `main` contains the final `3.2.0` metadata and release documentation.
5. Create the annotated release tag `v3.2.0` from the validated commit.
6. Run the stable release workflow against that tag.
7. Confirm all expected NuGet and symbol packages are published from the official source.
8. Confirm GitHub release assets, SBOMs, and provenance artifacts are attached where supported.
9. Confirm documentation deployment succeeds.
10. Run post-publication Source Link validation:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.0
```

11. Verify that package repository commit metadata resolves to the tagged source commit.
12. Record any release exception explicitly rather than silently weakening the release claim.

## Final scope statement

AsiBackbone remains Accountable Systems Infrastructure for governed .NET decision flow. This minor release makes capability-grant validation intent more explicit at consequential execution boundaries; it does not make AsiBackbone an authentication system, authorization system, host executor, robot controller, compliance certification, complete tamper-evidence platform, production key-management system, or production replay-protection system by default.
