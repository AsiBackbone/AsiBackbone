# AsiBackbone 7.5.3 Consumer Verification Guide

This guide describes checks for the `7.5.3` package family. Perform artifact
verification against the published `v7.5.3` GitHub Release and NuGet packages
after publication; this guide alone is not evidence of publication.

## Confirm compatibility and package metadata

- Package IDs remain `AsiBackbone.*`, and the target remains `net10.0`.
- Managed assemblies retain `AssemblyVersion` `7.0.0.0`.
- Package and file versions are `7.5.3` and `7.5.3.0`.
- Public APIs, durable schemas, canonical wire values, documented defaults,
  declared runtime dependency minimums, and runtime decision logic are
  unchanged from `7.5.2`.
- `AsiBackbone.Core` adds the `policy-as-code` and `capability-grant` discovery
  tags and updates its description. These are NuGet metadata changes, not new
  runtime capabilities.

## Verify the published artifacts

```powershell
gh release download v7.5.3 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.5.3.nupkg'
gh attestation verify ./AsiBackbone.Core.7.5.3.nupkg --repo AsiBackbone/AsiBackbone
```

Inspect NuGet metadata, declared dependencies, Source Link repository commit,
package icon, README, and the reviewed discovery tags. Compare SHA-256 hashes
with the release-evidence manifest and repeat for each adopted package.

## Build and exercise a clean consumer

Create a clean `net10.0` project, install only the required `AsiBackbone.*`
packages at `7.5.3`, restore, build with warnings enabled, and run the host's
tests. Exercise persistence and provider paths where adopted.

For a minimal policy-decision path, run the five-minute evaluator from the
repository or reproduce its constraint, allowed request, denied request, and
decision-before-side-effect assertions in a clean consumer. The evaluator is a
sample; it does not replace the host's integration and threat-model validation.

Retain decision-before-side-effect tests and production-signing configuration
checks described in the
[7.5.1 Consumer Verification Guide](consumer-verification-751.md).
Consumers moving from `6.x` must first complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

See also:

- [7.5.3 Release Notes](release-notes-753.md)
- [7.5.3 Release Readiness Record](release-readiness-753.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
