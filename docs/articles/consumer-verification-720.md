# AsiBackbone 7.2.0 Consumer Verification Guide

This guide records the consumer checks for the `7.2.0` package family. Perform
the artifact-verification steps against the published `v7.2.0` GitHub Release
and NuGet packages after publication.

## Confirm the compatibility boundary

- Package IDs remain `AsiBackbone.*`.
- All packages continue to target `net10.0`.
- Managed assemblies retain `AssemblyVersion` `7.0.0.0`.
- Package and file versions are `7.2.0` and `7.2.0.0`.
- Persisted schemas, canonical wire values, public namespaces, runtime behavior,
  and documented defaults are unchanged.
- Existing compiled consumers, positional callers, and interface implementations
  remain compatible.

## Update named-argument callers

When rebuilding against `7.2.0`, update source that names the affected
parameters:

| API | Replace | With |
| --- | --- | --- |
| `IDecisionReceiptSink.WriteAsync` | `residue:` | `receipt:` |
| `InMemoryAuditLedger.WriteAsync` | `residue:` | `receipt:` |
| `GovernanceTestDecisionReceiptSink.WriteAsync` | `residue:` | `receipt:` |
| `HostAccountabilityLifecycleEvent.ExecutionStarted` | `residue:` | `decisionReceipt:` |
| `HostAccountabilityLifecycleEvent.FromExecutionReceipt` | `residue:`, old `receipt:` | `decisionReceipt:`, `executionReceipt:` |

No data migration, interface reimplementation, or runtime configuration change
is required.

## Verify the published packages

Download a package and its release evidence from the `v7.2.0` GitHub Release,
then verify its provenance:

```powershell
gh release download v7.2.0 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.2.0.nupkg'
gh attestation verify ./AsiBackbone.Core.7.2.0.nupkg --repo AsiBackbone/AsiBackbone
```

Inspect the package metadata and Source Link repository commit, and compare the
package SHA-256 value with the release evidence manifest. Repeat for each
package adopted by the application.

## Build a clean consumer

Create a clean `net10.0` project, add only the required `AsiBackbone.*` packages
at `7.2.0`, restore, build with warnings enabled, and exercise the integration
boundary used by the application. Run the consumer's own tests and confirm that
the parameter-name updates are the only required source changes.

Consumers upgrading from `6.x` must first complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md); the analyzer and deprecation
checks for the `7.1` line are in the
[7.1.0 Consumer Verification Guide](consumer-verification-710.md).

See also:

- [7.2.0 Release Notes](release-notes-720.md)
- [7.2.0 Release Readiness Record](release-readiness-720.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
