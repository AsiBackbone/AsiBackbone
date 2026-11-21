# AsiBackbone 7.5.5 Release Notes

Release date: 2026-11-21

`7.5.5` is a backward-compatible .NET 10 dependency, documentation, and
release-reliability maintenance patch for the stable `7.x` package family. It
services framework dependencies, corrects assembly informational versions,
hardens release runners, and strengthens documentation validation. Package and
file versions advance to `7.5.5` and `7.5.5.0`; binary assembly identity remains
`7.0.0.0`.

## .NET 10 dependency servicing

- Entity Framework Core packages advance from 10.0.14 to 10.0.15.
- Microsoft.Extensions.DependencyInjection,
  Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options, and
  System.Security.Cryptography.Pkcs advance from 10.0.14 to 10.0.15.
- Dependabot ignores framework-coupled .NET 11 major updates so the supported
  .NET 10 line continues receiving compatible servicing updates.
- Microsoft.CodeAnalysis.Analyzers and Microsoft.CodeAnalysis.CSharp remain on
  5.9.0 so the shipped analyzer stays compatible with the compiler included in
  the .NET 10 SDK. Microsoft.NET.Test.Sdk, xunit.v3, ReportGenerator, and
  dotnet-stryker also receive maintenance updates.

## Build and release reliability

- Managed package assemblies now use the SDK-default informational version
  format, `<version>+<commit>`. Source Link continues to record repository
  provenance, and repository tests guard the built value.
- Package-publish and release-validation jobs now start with Harden Runner in
  egress audit mode, recording release-job network, process, and file activity
  without changing publication behavior.
- Zenodo archive DOI validation now uses a dedicated bounded-retry step instead
  of Lychee. Pull requests and pushes warn on transient Zenodo failures;
  scheduled and manual validation still fail if a DOI stops resolving.

## Documentation maintenance

- Current documentation was reviewed against the implementation, public API
  baselines, and the published package family. Stale stable-line claims,
  outdated capability-validation wording, closed quality work, and historical
  terminology were corrected.
- Release-claim validation now distinguishes prepared and published versions
  and rejects stale claims about the current stable package line.
- The repository Code of Conduct now matches the AsiBackbone organization
  standard; the .NET Foundation alignment statement remains in governance
  documentation.

## Compatibility

No stable public signature, durable schema, canonical wire value, package ID,
public namespace, target framework, documented default, or runtime decision
behavior changes in this patch. Managed assemblies retain `AssemblyVersion`
`7.0.0.0`, and the package family remains on `net10.0`.

The dependency updates may raise transitive minimums for consumers that adopt
the affected packages. Restore the package family together, inspect the
resolved graph, and run host integration tests before deployment.

## Upgrade actions

1. Update adopted `AsiBackbone.*` packages to `7.5.5` together.
2. Restore and inspect the resolved dependency graph, rebuild with warnings
   enabled, and run the consumer's integration tests.
3. When admitting GitHub release build packages, verify their Sigstore bundles
   against the exact release tag, source commit, and protected workflow
   identity described in the consumer verification guide.

Consumers moving from `6.x` must still complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

## Release verification

Stable publication occurs only from the protected `v7.5.5` tag after the
release-preparation pull request is merged to `main` and required hosted checks
pass on the release candidate. The date above is the planned release date;
publication and hosted evidence must be verified separately.

See also:

- [7.5.5 Release Readiness Record](release-readiness-755.md)
- [7.5.5 Consumer Verification Guide](consumer-verification-755.md)
- [7.5.4 Release Notes](release-notes-754.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
