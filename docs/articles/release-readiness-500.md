# AsiBackbone 5.0.0 Release Readiness Record

Release candidate date: 2026-09-07

## Release intent

`5.0.0` starts the stable `5.x` package family. It is a major release because it
resolves twelve security findings, and every behavior change in it makes a
governance path fail closed where it previously returned a permissive result.
Several of those changes require host action: persisted enum values move off
zero, capability validation requires explicit options, partial audit chain
verification requires an anchor, and local-development signing is refused in
production.

This is a pre-tag checklist. Package publication and the GitHub release remain
separate post-merge actions.

## Security disclosure coordination

`5.0.0` is the fixed version for twelve draft security advisories, including
[`GHSA-q63q-xcvp-chv8`](https://github.com/AsiBackbone/AsiBackbone/security/advisories/GHSA-q63q-xcvp-chv8) (critical) and [`GHSA-p6pw-5gr3-xrr7`](https://github.com/AsiBackbone/AsiBackbone/security/advisories/GHSA-p6pw-5gr3-xrr7) (high). Because
`4.0.0` is published and affected, disclosure and release must be sequenced
together.

- [ ] Each advisory records `5.0.0` as the fixed version.
- [ ] Each advisory records the affected range as `<= 4.0.0`.
- [ ] Advisories are published no earlier than package availability.
- [ ] The host mitigation for `4.0.0` and earlier is stated in each advisory.
- [ ] The repository does not publicly describe an unfixed defect before the
      fixed packages are available.

## Version and compatibility

- [ ] Package version is `5.0.0`.
- [ ] `AssemblyVersion` is `5.0.0.0`.
- [ ] `FileVersion` is `5.0.0.0`.
- [ ] Target framework remains `net10.0`.
- [ ] Package IDs and public namespaces remain unchanged.
- [ ] Citation and Zenodo metadata report `5.0.0` and `2026-09-07`.
- [ ] Template fallback references and applicable lock ranges use `5.0.0`.
- [ ] Source Link validation resolves `5.0.0` from shared metadata.
- [ ] The 4.0.0-to-5.0.0 migration guide documents every breaking change.

## Runtime boundary

- [ ] Verification recomputes the canonical payload hash before calling a
      provider and denies a payload that does not hash to the signed value.
- [ ] Capability proof validation rebuilds the payload from the grant it
      evaluates and asserts artifact type and identifier.
- [ ] Partial audit chain verification requires an anchoring previous link hash.
- [ ] Chain truncation is detectable against an expected tip.
- [ ] Zero is a rejected `Unspecified` sentinel in the four renumbered enums.
- [ ] Capability validation requires explicit options and an audience
      expectation wherever proof or bounded use is required.
- [ ] Provider signing responses are compared against the request.
- [ ] Local-development signing is refused in production without an explicit
      opt-in, and `ASIB003` reports unguarded registration.
- [ ] Issuer-bound capability use limits cannot be widened by a relying party.
- [ ] Grants recording `StableArtifactsV1` canonicalize unchanged.

## Required validation before tag

- [ ] Locked restore.
- [ ] Debug and Release builds.
- [ ] `dotnet format --verify-no-changes`.
- [ ] Full Microsoft Testing Platform suite.
- [ ] Repository, package, and Core coverage gates.
- [ ] XML documentation inventory.
- [ ] API baseline and compatibility review.
- [ ] Version consistency for `5.0.0` and `v5.0.0`.
- [ ] Package creation and NuGet metadata validation.
- [ ] Template fallback smoke test against `5.0.0`.
- [ ] External-consumer and stable-package smoke tests.
- [ ] DocFX and documentation-link validation.
- [ ] CodeQL, dependency review, OpenSSF, actionlint/Zizmor, and OWASP checks.
- [ ] SBOM and provenance artifacts where supported.
- [ ] No package-signing claim for unsigned packages.

## Local verification performed on the release candidate

The following were run against the candidate source on 2026-09-07. They are
recorded as development evidence and do not replace the release workflow runs
listed above.

| Check | Result |
| --- | --- |
| Release build, full solution | Succeeded, 0 warnings, 0 errors |
| Microsoft Testing Platform suite | 1717 passed, 0 failed |
| `dotnet format --verify-no-changes` | No changes required |
| Version consistency for `5.0.0` and tag `v5.0.0` | Passed |
| Core branch coverage gate (90%) | 91.43% |
| Package coverage baselines | Passed |
| XML documentation inventory | Generated |
| Documentation continuity and link validation | Passed |
| Web API template project build | Succeeded |

Each of the five tamper tests covering the critical finding was confirmed to
fail with the content-binding checks disabled and pass with them enabled, so the
tests detect the defect rather than only exercising the new code paths.

## Release sequence

1. Validate `release/5.0.0`.
2. Open the release pull request to `main`.
3. Merge only after required checks pass.
4. Tag the validated source as `v5.0.0`.
5. Publish packages and release artifacts.
6. Publish the twelve security advisories with `5.0.0` as the fixed version.
7. Confirm documentation deployment.
8. Run post-publication Source Link validation.

## Related documentation

- [5.0.0 Release Notes](release-notes-500.md)
- [5.0.0 Migration Guide](upgrade-400-to-500.md)
- [5.0.0 Consumer Verification Guide](consumer-verification-500.md)
- [Release Validation](release-validation.md)
- [Release Cadence and Readiness](release-cadence-and-readiness.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
