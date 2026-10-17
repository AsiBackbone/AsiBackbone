# AsiBackbone 7.2.0 Release Notes

Release date: 2026-10-17

`7.2.0` is a backward-compatible minor release for the stable `7.x` package
family. It completes the public parameter-name transition from the historical
term `residue` to decision-receipt terminology, refreshes supported dependencies,
and carries documentation, automation, and test maintenance completed since
`7.1.1`.

Package IDs, public namespaces, the `net10.0` target, persisted schemas,
canonical wire values, runtime behavior, and binary assembly identity are
unchanged. `AssemblyVersion` remains `7.0.0.0`; package and file versions advance
to `7.2.0` and `7.2.0.0`.

## Public terminology alignment

The following public parameter names now use decision-receipt terminology:

| API | Previous name | 7.2.0 name |
| --- | --- | --- |
| `IDecisionReceiptSink.WriteAsync` | `residue` | `receipt` |
| `InMemoryAuditLedger.WriteAsync` | `residue` | `receipt` |
| `GovernanceTestDecisionReceiptSink.WriteAsync` | `residue` | `receipt` |
| `HostAccountabilityLifecycleEvent.ExecutionStarted` | `residue` | `decisionReceipt` |
| `HostAccountabilityLifecycleEvent.FromExecutionReceipt` | `residue`, `receipt` | `decisionReceipt`, `executionReceipt` |

These are source-level names only. Existing compiled consumers, positional
callers, interface implementations, persisted data, and runtime behavior remain
compatible. Source that passes one of these parameters by name must use the new
name when rebuilding against `7.2.0`.

## Dependency maintenance

- Updated MessagePack to `3.1.12`.
- Updated Microsoft CodeAnalysis packages to `5.9.0`.
- Updated Microsoft Entity Framework Core packages to `10.0.13`.
- Updated Microsoft Extensions packages to `10.0.13`.
- Updated Microsoft.NET.Test.Sdk to `18.11.0`.
- Refreshed the repository lock graphs for the supported dependency set.

The release also includes the xUnit `4.0.2` and Visual Studio runner `4.0.1`
test-stack update, Aspire `13.6.1` maintenance, and the previously merged
MessagePack security maintenance.

## Documentation and automation

- Added a ReferenceApplication integration walkthrough and connected it to the
  documentation ownership contract and site navigation.
- Added XML documentation for the remaining 99 public API gaps while keeping
  the CS1591 baseline at zero.
- Updated pinned GitHub Actions used by repository automation.

## Upgrade actions

1. Update adopted `AsiBackbone.*` packages to `7.2.0` together.
2. Replace the old parameter names above in any named-argument calls.
3. Restore, rebuild with warnings enabled, and run the consumer's tests.

No data migration or runtime configuration change is required. Consumers
moving from `6.x` must still complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

## Release verification

The release candidate is validated through locked restore, Debug and Release
builds, the full test suite, formatting, public API baselines, package metadata
and contents, template and external-consumer smoke tests, documentation release
claims, and DocFX. Stable publication occurs only from the protected `v7.2.0`
tag after the release-preparation pull request is merged to `main`.

See also:

- [7.2.0 Release Readiness Record](release-readiness-720.md)
- [7.2.0 Consumer Verification Guide](consumer-verification-720.md)
- [7.1.1 Release Notes](release-notes-711.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Upgrade from 6.x to 7.0](upgrade-600-to-700.md)
