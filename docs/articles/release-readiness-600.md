# AsiBackbone 6.0.0 Release Readiness Record

Release candidate date: 2026-09-19

## Release intent

`6.0.0` is a major release for the AsiBackbone package family. It publishes the
planned 6.0 public API names, removes the members whose `5.x` deprecation
windows completed, and ships verification, signature-input, and use-store
corrections that change security-relevant behavior.

This record is a pre-tag checklist. Do not create `v6.0.0` or publish packages
until every required validation is complete on the final release-candidate
commit.

## Compatibility boundary

- Package IDs remain unchanged.
- Public namespaces remain unchanged.
- The target framework remains `net10.0`.
- `AssemblyVersion` advances to `6.0.0.0`.
- Package version advances to `6.0.0` and `FileVersion` to `6.0.0.0`.
- Public type and member renames, removed obsolete members, and the
  `GovernancePolicyAttribute` rename are intentional source and binary breaks
  without compatibility aliases.
- Verification default actions, the signature-input wire format, and the
  in-memory use-store retention horizon are intentional behavior breaks.
- JSON keys, schema versions, canonical artifact tags, canonical payload bytes,
  diagnostic IDs, and EF table and column names are unchanged.
- NuGet package author signing remains deferred under the current decision
  record.

## Version and metadata checklist

- [x] `Directory.Build.props` resolves package version `6.0.0`.
- [x] `AssemblyVersion` is `6.0.0.0`.
- [x] `FileVersion` is `6.0.0.0`.
- [x] `CITATION.cff` reports `6.0.0` and the final release date.
- [x] `.zenodo.json` reports `6.0.0` and the major-release scope.
- [x] Template fallback package references use `6.0.0`.
- [x] NuGet lock files reference `6.0.0` for in-repository projects and restore
  in locked mode.
- [ ] Package validation compares `6.0.0` against the configured
  `AsiBackbonePackageValidationBaselineVersion`, with every intentional break
  recorded as an exact suppression. The baseline remains `5.1.0`, the latest
  published package family; `5.2.0` was not published and therefore cannot be a
  package-validation baseline. API introduced during the unreleased 5.2
  development stage, such as the policy-evaluator builder, is covered by the
  reviewed public API baseline and documented in the migration guide rather
  than detected by NuGet package validation.
- [ ] Public API baselines are regenerated and reviewed.
- [ ] `CHANGELOG.md` and release notes describe the same change set and
  compatibility boundary.
- [ ] Evergreen documentation identifies `6.0.0` as the current release without
  rewriting historical release records.

## Required validation before tag

- [ ] Version consistency passes for `6.0.0` and `v6.0.0`.
- [ ] Locked restore succeeds using the repository SDK and package configuration.
- [ ] Debug and Release solution builds succeed with warnings treated as errors.
- [ ] `dotnet format --verify-no-changes` succeeds.
- [ ] All tests pass, including the signature-input binding and use-store replay
  tests.
- [ ] Public API baseline and package-validation checks pass.
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

## Major-release checks

Before publication, confirm that:

- the [Upgrade from 5.x to 6.0](upgrade-500-to-600.md) guide covers every
  removed member, renamed type and member, changed default, and wire-format
  change in this release;
- no obsolete member or compatibility alias remains from the completed `5.x`
  deprecation windows;
- first-party samples, templates, and benchmarks use the 6.0 names;
- the signature-input change is documented for host managed-key clients and
  verification services; and
- Learning repository samples and articles that target `6.0.0` are tracked for
  API alignment.

## Release sequence

1. Confirm the `6.0.0` changelog entry and release notes are consistent.
2. Merge the release-preparation pull request after required checks pass.
3. Confirm `main` contains the final `6.0.0` metadata and documentation.
4. Create the annotated tag `v6.0.0` from the validated commit.
5. Run the stable release workflow against that tag.
6. Confirm all expected NuGet and symbol packages are published.
7. Confirm build packages, SBOMs, manifests, and release notes are attached to
   the GitHub release.
8. Verify package/SBOM attestations by subject digest.
9. Confirm documentation deployment succeeds.
10. Run post-publication Source Link validation:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 6.0.0
```

11. Verify package repository commit metadata resolves to the tagged source
    commit.
12. Advance `AsiBackbonePackageValidationBaselineVersion` to `6.0.0` for the
    next release line.
13. Record any release exception explicitly rather than weakening a gate.

## Related documentation

- [6.0.0 Release Notes](release-notes-600.md)
- [6.0.0 Consumer Verification Guide](consumer-verification-600.md)
- [Upgrade from 5.x to 6.0](upgrade-500-to-600.md)
- [6.0 Public API Naming Convention](public-api-naming-600.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [NuGet Package Signing Decision Record](nuget-package-signing-decision.md)
