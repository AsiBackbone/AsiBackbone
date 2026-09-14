# AsiBackbone 5.2.0 Release Readiness Record

Release candidate date: 2026-09-14

## Release intent

`5.2.0` is a backward-compatible minor release for the stable `5.x` package
family. It publishes the additive policy-evaluator builder and begins the
`ASIB900` deprecation window while preserving the existing package identity,
namespace, target-framework, and binary-assembly boundary.

This record is a pre-tag checklist. Do not create `v5.2.0` or publish packages
until every required validation is complete on the final release-candidate
commit.

## Compatibility boundary

- Package IDs remain unchanged.
- Public namespaces remain unchanged.
- The target framework remains `net10.0`.
- `AssemblyVersion` remains `5.0.0.0`.
- Package version advances to `5.2.0` and `FileVersion` to `5.2.0.0`.
- The evaluator builder is additive.
- The five `ASIB900` constructors remain present and are planned for removal in
  `6.0`.
- The all-dependencies evaluator constructor remains supported for DI.
- NuGet package author signing remains deferred under the current decision
  record.

## Version and metadata checklist

- [ ] `Directory.Build.props` resolves package version `5.2.0`.
- [ ] `AssemblyVersion` remains `5.0.0.0`.
- [ ] `FileVersion` is `5.2.0.0`.
- [ ] `CITATION.cff` reports `5.2.0` and release date `2026-09-14`.
- [ ] `.zenodo.json` reports `5.2.0` and the minor-release scope.
- [ ] Template fallback package references use `5.2.0`.
- [ ] NuGet lock files have been regenerated after the central version bump.
- [ ] Package validation compares `5.2.0` against the published `5.1.0`
  baseline.
- [ ] `CHANGELOG.md` and release notes describe the same change set and
  compatibility boundary.
- [ ] Evergreen documentation identifies `5.2.0` as the current release without
  rewriting historical release records.

## Required validation before tag

- [ ] Version consistency passes for `5.2.0` and `v5.2.0`.
- [ ] Locked restore succeeds using the repository SDK and package configuration.
- [ ] Debug and Release solution builds succeed with warnings treated as errors.
- [ ] `dotnet format --verify-no-changes` succeeds.
- [ ] All tests pass.
- [ ] Public API baseline and package-validation checks pass against `5.1.0`.
- [ ] XML-documentation inventory and enforcement checks pass.
- [ ] Documentation continuity, links, and release-claim validation pass.
- [ ] DocFX site generation succeeds with no release-blocking warnings.
- [ ] Package creation succeeds for the complete publishable package set.
- [ ] Package IDs, versions, dependencies, repository metadata, symbols, and
  README content are correct.
- [ ] Package SBOM generation succeeds.
- [ ] Template, external-consumer, and stable-package smoke tests succeed.
- [ ] CodeQL and dependency review report no blocking findings.
- [ ] Required actionlint/Zizmor, workflow-security, OWASP Dependency-Check, and
  related repository checks have no unexplained blocking findings.
- [ ] No package-author-signing claim is made for unsigned project packages.

## Deprecation-window check

Before publication, confirm that:

- `ASIB900` links to the published migration guide;
- the replacement builder covers the intended partial-constructor use cases;
- first-party examples use the builder where appropriate;
- obsolete overloads remain tested during the `5.x` window; and
- no `6.0` constructor removal is included in this release.

## Release sequence

1. Confirm the `5.2.0` changelog entry and release notes are consistent.
2. Regenerate NuGet lock files after the central version change.
3. Merge the release-preparation pull request after required checks pass.
4. Confirm `main` contains the final `5.2.0` metadata and documentation.
5. Create the annotated tag `v5.2.0` from the validated commit.
6. Run the stable release workflow against that tag.
7. Confirm all expected NuGet and symbol packages are published.
8. Confirm build packages, SBOMs, manifests, and release notes are attached to
   the GitHub release.
9. Verify package/SBOM attestations by subject digest.
10. Confirm documentation deployment succeeds.
11. Run post-publication Source Link validation:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 5.2.0
```

12. Verify package repository commit metadata resolves to the tagged source
    commit.
13. Record any release exception explicitly rather than weakening a gate.

## Related documentation

- [5.2.0 Release Notes](release-notes-520.md)
- [5.2.0 Consumer Verification Guide](consumer-verification-520.md)
- [ASIB900: Obsolete Policy Evaluator Constructors](asib900-policy-evaluator-constructors.md)
- [5.0.0 Migration Guide](upgrade-400-to-500.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [NuGet Package Signing Decision Record](nuget-package-signing-decision.md)
