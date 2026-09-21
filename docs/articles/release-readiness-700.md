# AsiBackbone 7.0.0 Release Readiness Record

Release candidate date: 2026-09-20

## Release intent

`7.0.0` is a major release for the AsiBackbone package family. It binds
liability-handshake acknowledgment responses to the actor that received the
challenge and moves the DLP failure-behavior and intent-risk enums away from
permissive zero defaults.

This record is a pre-tag checklist. Do not create `v7.0.0` or publish packages
until every required validation is complete on the final release-candidate
commit.

## Compatibility boundary

- Package IDs, public namespaces, and the `net10.0` target remain unchanged.
- No public type, member, namespace, or package is renamed or removed.
- Package version advances to `7.0.0`; `AssemblyVersion` and `FileVersion`
  advance to `7.0.0.0`.
- Acknowledgment responses must match the challenged actor's ID and type.
- `DlpFailureBehavior` and `DlpIntentRiskLevel` gain `Unspecified = 0`; every
  existing numeric value shifts by one and incomplete policy is rejected.
- NuGet package author signing remains deferred under the current decision
  record.

## Version and metadata checklist

- [x] `Directory.Build.props` resolves package version `7.0.0`.
- [x] `AssemblyVersion` and `FileVersion` are `7.0.0.0`.
- [x] `CITATION.cff` and `.zenodo.json` report `7.0.0`, the release date, and
  the 7.0 major-release scope.
- [x] Template fallback package references use `7.0.0`.
- [x] NuGet lock files reference `7.0.0` for in-repository projects.
- [x] Package validation compares `7.0.0` with the `5.1.0` baseline so the full
  two-major compatibility surface remains checked. Reviewed 6.0 breaks retain
  their exact suppressions, and intentional 7.0 enum-value changes have exact
  `CP0011` suppressions.
- [x] Public API baselines record the 7.0 enum values and new members.
- [x] `CHANGELOG.md`, release notes, and the upgrade guide describe the same
  change set and compatibility boundary.
- [x] Evergreen documentation identifies `7.0.0` as the current release while
  preserving prior records as historical evidence.

## Required validation before tag

- [x] Version consistency passes for `7.0.0` and `v7.0.0`.
- [x] Locked restore succeeds using the repository SDK and package configuration.
- [x] Debug and Release solution builds succeed with warnings treated as errors.
- [x] `dotnet format --verify-no-changes` succeeds.
- [x] All tests pass, including actor-binding and DLP-default regression tests.
- [x] Public API baseline and package-validation checks pass.
- [x] XML-documentation inventory and enforcement checks pass.
- [x] Documentation continuity, links, and release-claim validation pass.
- [x] DocFX site generation succeeds with no release-blocking warnings.
- [x] Package creation succeeds for the complete publishable package set.
- [x] Package IDs, versions, dependencies, repository metadata, symbols, and
  README content are correct.
- [x] Package SBOM generation succeeds.
- [x] Template, external-consumer, and stable-package smoke tests succeed.
- [ ] CodeQL and dependency review report no blocking findings.
- [ ] Required actionlint/Zizmor, workflow-security, OWASP Dependency-Check,
  and related repository checks have no unexplained blocking findings.
- [x] No package-author-signing claim is made for unsigned project packages.

Unchecked items are publication gates. They remain unchecked until the final
release-candidate commit has the corresponding local or CI evidence.

## Major-release checks

Before publication, confirm that:

- the [Upgrade from 6.x to 7.0](upgrade-600-to-700.md) guide covers actor
  binding, every shifted enum value, persisted numeric data, configuration, and
  consumer rebuild requirements;
- first-party samples, templates, tests, and documentation use explicit DLP
  enum values where required;
- actor mismatch failures disclose one stable reason code without revealing
  whether the ID or actor type differed; and
- consumers have a documented verification path for the new major boundary.

## Release sequence

1. Complete every required validation above on the final candidate commit.
2. Merge the release-preparation pull request after required checks pass.
3. Confirm `main` contains the final `7.0.0` metadata and documentation.
4. Create the annotated tag `v7.0.0` from the validated commit.
5. Run the stable release workflow against that tag.
6. Confirm all expected NuGet and symbol packages are published.
7. Confirm build packages, SBOMs, manifests, and release notes are attached to
   the GitHub release.
8. Verify package/SBOM attestations by subject digest.
9. Confirm documentation deployment succeeds.
10. Run post-publication Source Link validation:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 7.0.0
```

11. Verify package repository commit metadata resolves to the tagged source
    commit.
12. Advance the package-validation baseline only after `7.0.0` is published.
13. Record any release exception explicitly rather than weakening a gate.

## Related documentation

- [7.0.0 Release Notes](release-notes-700.md)
- [7.0.0 Consumer Verification Guide](consumer-verification-700.md)
- [Upgrade from 6.x to 7.0](upgrade-600-to-700.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [NuGet Package Signing Decision Record](nuget-package-signing-decision.md)
