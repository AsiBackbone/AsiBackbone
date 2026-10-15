# AsiBackbone 7.1.1 Release Notes

Release date: 2026-10-15

`7.1.1` is a backward-compatible patch release for the stable `7.x` package
family. It republishes the `7.1.0` package surface with refreshed package
README content and carries the documentation, validation, and test maintenance
completed since `7.1.0`. Public APIs, runtime behavior, package dependencies,
schemas, canonical wire values, and documented defaults are unchanged. Package
IDs and the `net10.0` target are unchanged. `AssemblyVersion` remains
`7.0.0.0`; package and file versions advance to `7.1.1` and `7.1.1.0`.

The analyzer rule, deprecations, and migration guidance introduced in the `7.1`
line are described in the [7.1.0 Release Notes](release-notes-710.md) and remain
current.

## Package content

- Every packaged component README now carries NuGet version and download
  badges, so the README shown on each package's NuGet page links to that
  package.
- The `AsiBackbone.Templates` scaffold references the `7.1.1` packages.

No library source changes since `7.1.0`. The packages are rebuilt from the same
source with the new version metadata.

## Dependency security maintenance

The transitive `System.Security.Cryptography.Pkcs` dependency that
`Microsoft.Data.SqlClient` brought into the SQL Server provider tests is pinned
to the patched 10.0.13 release for CVE-2026-70162
([GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7)) and
CVE-2026-84008
([GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx)), so
repository builds pass NuGet audit again.

No shipped package and no `AsiBackbone.Templates` scaffold depends on
`Microsoft.Data.SqlClient`, so package consumers are not exposed through
AsiBackbone and need no action. Applications that use `Microsoft.Data.SqlClient`
directly should follow their own dependency guidance for these advisories.

## Documentation and validation

- Aligned the documentation homepage, navigation, and footer with the
  organization landing-page standard, and restored product-fit guidance to the
  README and Get Started navigation.
- Every workflow DocFX build now runs with `--warningsAsErrors`, guarded by
  `scripts/Validate-DocFxStrictBuild.ps1`.
- Added full-site link validation with Lychee and corrected the stale source
  link it found.
- Every published page now has search and social-preview descriptions, and the
  metadata script no longer inserts a literal `\n` when line endings differ.
- Publish Documentation no longer drops deployed mutation reports after a
  transient fetch failure.
- Routed support questions to the
  [AsiBackbone organization Discussions](https://github.com/orgs/AsiBackbone/discussions)
  and added the top-level concept DOI to the citation metadata.

## Test maintenance

Branch tests now cover every class that was below 80% branch coverage in the
published report. Overall branch coverage rises from 87.6% to 92.3% and line
coverage from 97.4% to 98.4%. No library code was changed to achieve this.

## Upgrade actions

None required. Consumers on `7.1.0` may move to `7.1.1` at their convenience;
it contains no code or behavior change. Consumers moving from `6.x` must first
complete the [6.x to 7.0 upgrade guide](upgrade-600-to-700.md), and consumers
moving from `7.0.x` should review the `7.1` analyzer and deprecation guidance in
the [7.1.0 Release Notes](release-notes-710.md).

## Release verification

The release candidate is validated through locked restore, Debug and Release
builds, the full test suite, formatting, public API baselines, package metadata
and contents, template and external-consumer smoke tests, documentation release
claims, and DocFX. Stable publication occurs only from the protected `v7.1.1`
tag after the release-preparation pull request is merged to `main`.

See also:

- [7.1.1 Release Readiness Record](release-readiness-711.md)
- [7.1.1 Consumer Verification Guide](consumer-verification-711.md)
- [7.1.0 Release Notes](release-notes-710.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Upgrade from 6.x to 7.0](upgrade-600-to-700.md)
