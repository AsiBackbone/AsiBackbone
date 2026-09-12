# AsiBackbone 5.1.0 Release Readiness Record

Release candidate date: 2026-09-11

## Release intent

`5.1.0` is a backward-compatible stabilization release for the stable `5.x`
package family. It packages the public API baseline, repository security,
security-advisory distribution, branch-retention, documentation-governance, and
project-stewardship work completed after `5.0.0` while preserving the runtime
contract.

This record is a pre-tag checklist. Do not create `v5.1.0` or publish packages
until every required validation is complete on the final release-candidate
commit.

## Included scope

- Add committed `v5.0.0` public API baselines and release-blocking baseline and
  package-boundary validation for every managed stable package.
- Add auditable repository security-control definitions and dry-run-first
  management tooling.
- Add security-advisory distribution and CVE-request audit/apply tooling with
  fail-closed response handling and non-mutating preview behavior.
- Add branch-retention policy, maintenance tooling, fixtures, and tests.
- Add `SUPPORT.md` and `MAINTAINERS.md` and align repository ownership and
  contribution routing.
- Reorganize documentation navigation and strengthen cross-repository link and
  current-release-claim validation.
- Carry forward workflow, stable-smoke, citation, and documentation annotation
  corrections merged after `5.0.0`.

## Compatibility boundary

- Package IDs remain unchanged.
- Public namespaces remain unchanged.
- The target framework remains `net10.0`.
- `AssemblyVersion` remains `5.0.0.0` for the compatible `5.x` binary line.
- `FileVersion`, package version, informational version, citation metadata, and
  release metadata advance to `5.1.0`.
- No stable public API additions, removals, or signature changes are included.
- No runtime behavior or durable artifact shape changes are included.
- NuGet package signing remains deferred while the project is independently
  maintained.

## Version and metadata checklist

- [ ] `Directory.Build.props` resolves package version `5.1.0`.
- [ ] `AssemblyVersion` remains `5.0.0.0`.
- [ ] `FileVersion` is `5.1.0.0`.
- [ ] `CITATION.cff` reports version `5.1.0` and release date `2026-09-11`.
- [ ] `.zenodo.json` reports version `5.1.0` and stabilization-release scope.
- [ ] Template fallback package references use `5.1.0`.
- [ ] Source Link post-publication validation resolves the central version.
- [ ] Lock files are regenerated after the version bump and locked restore
  succeeds.
- [ ] `CHANGELOG.md` and release notes describe the same change set and
  compatibility boundary.
- [ ] Evergreen documentation identifies `5.1.0` as the current release without
  rewriting historical release records.

## Required validation before tag

- [ ] Version consistency passes for `5.1.0` and tag `v5.1.0`.
- [ ] Debug solution-build coverage validation passes.
- [ ] Locked restore succeeds using the repository SDK and package configuration.
- [ ] Debug and Release solution builds succeed.
- [ ] `dotnet format --verify-no-changes` succeeds.
- [ ] All test projects pass.
- [ ] Public API baseline and package-boundary validation pass.
- [ ] Documentation continuity and cross-repository link validation pass.
- [ ] DocFX site generation succeeds.
- [ ] Package creation succeeds for the complete publishable package set.
- [ ] Generated package IDs, versions, dependencies, repository metadata,
  symbols, and README content are correct.
- [ ] Package SBOM generation succeeds.
- [ ] Template, external-consumer, and stable-package smoke tests succeed.
- [ ] CodeQL and dependency review report no blocking findings.
- [ ] OpenSSF Scorecard, workflow-security, actionlint/Zizmor, and OWASP
  Dependency-Check results have no unexplained blocking findings.
- [ ] No package-signing claim is made for unsigned packages.

## Local release-candidate evidence

The release-preparation change should record the following checks before it is
merged:

| Check | Expected result |
| --- | --- |
| Version consistency for `5.1.0` and `v5.1.0` | Passed |
| Locked restore | Passed |
| Debug solution build | Passed |
| Release solution build | Passed |
| Formatting verification | Passed |
| Test suite | Passed |
| Public API and package-boundary validation | Passed |
| DocFX and documentation validation | Passed |
| Package metadata and SBOM generation | Passed |
| Template and consumer smoke tests | Passed |

GitHub-hosted security, dependency, provenance, and repository-control evidence
remains authoritative for checks that cannot be completed solely from a local
checkout.

## Release sequence

1. Confirm the `5.1.0` changelog entry and release notes are consistent.
2. Regenerate and commit NuGet lock files after the central version change.
3. Merge the release-preparation pull request after required checks pass.
4. Confirm `main` contains the final `5.1.0` metadata and release documentation.
5. Create the annotated release tag `v5.1.0` from the validated commit.
6. Run the stable release workflow against that tag.
7. Confirm all expected NuGet and symbol packages are published from the official
   source.
8. Confirm all package SBOMs, `sbom-manifest.json`,
   `release-evidence-manifest.json`, and retained release notes are attached to
   the public GitHub release, and verify package/SBOM attestations by subject
   digest.
9. Confirm documentation deployment succeeds.
10. Run post-publication Source Link validation:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 5.1.0
```

11. Verify package repository commit metadata resolves to the tagged source
    commit.
12. Create the version-specific Zenodo record, then update citation metadata in a
    follow-up change if a new version DOI is intentionally recorded.
13. Record any release exception explicitly rather than silently weakening the
    release claim.

## Final scope statement

AsiBackbone remains Accountable Systems Infrastructure for governed .NET
decision flow. This release improves evidence and operational stewardship around
the package family; it does not make AsiBackbone an authentication system,
authorization system, host executor, compliance certification, complete
tamper-evidence platform, production key-management system, or production
replay-protection system by default.

## Related documentation

- [5.1.0 Release Notes](release-notes-510.md)
- [5.1.0 Consumer Verification Guide](consumer-verification-510.md)
- [5.0.0 Migration Guide](upgrade-400-to-500.md)
- [Release Validation](release-validation.md)
- [API Baseline and Boundary Checks](api-baseline-and-boundary-checks.md)
- [Repository Host Security Controls](repository-host-security-controls.md)
- [Branch Retention Policy](branch-retention-policy.md)
