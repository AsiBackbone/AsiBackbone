# AsiBackbone 7.5.2 Release Notes

Release date: 2026-10-25

`7.5.2` is a backward-compatible dependency maintenance patch for the stable
`7.x` package family. Package and file versions advance to `7.5.2` and
`7.5.2.0`; binary assembly identity remains `7.0.0.0`.

## Dependency maintenance

- Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.InMemory,
  Microsoft.EntityFrameworkCore.Relational, Microsoft.EntityFrameworkCore.Sqlite,
  and Microsoft.EntityFrameworkCore.SqlServer advance from `10.0.13` to `10.0.14`.
- Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging.Abstractions,
  Microsoft.Extensions.Options, and System.Security.Cryptography.Pkcs advance
  from `10.0.13` to `10.0.14`.
- Aspire.Hosting.AppHost advances from `13.6.2` to `13.6.4` for the optional
  Aspire sample. FsCheck advances from `3.5.0` to `3.6.0` for test tooling.
- Affected lock files capture the updated dependency graph. Template fallback
  references use the `7.5.2` package family.

These updates were merged in [#910](https://github.com/AsiBackbone/AsiBackbone/pull/910)
and [#911](https://github.com/AsiBackbone/AsiBackbone/pull/911).

## Compatibility

No AsiBackbone source implementation, public signature, durable schema,
canonical wire value, package ID, public namespace, target framework,
documented default, or runtime decision logic changes in this patch.
Consumer-visible Microsoft dependency minimums advance to `10.0.14` where
declared by the adopted package. Review central version overrides and restore
the consumer graph; dependency upgrades can affect behavior in the upstream
libraries even when AsiBackbone's own implementation is unchanged.

Aspire hosting and FsCheck remain sample/test dependencies; they are not added
to the stable runtime packages. The `7.5.0` trace-correlation APIs and the
`7.5.1` signing guidance and host-enforcement evidence remain applicable.

## Upgrade actions

1. Update adopted `AsiBackbone.*` packages to `7.5.2` together.
2. Review any centrally pinned Microsoft dependency versions against the new
   `10.0.14` minimums, then restore and inspect the resolved dependency graph.
3. Rebuild with warnings enabled and run the consumer's integration tests,
   especially EF Core persistence and provider paths used by the host.

Consumers moving from `6.x` must still complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

## Release verification

Stable publication occurs only from the protected `v7.5.2` tag after the
release-preparation pull request is merged to `main` and required hosted
checks pass on the release candidate. The date above is the planned release
date; publication and hosted evidence must be verified separately.

See also:

- [7.5.2 Release Readiness Record](release-readiness-752.md)
- [7.5.2 Consumer Verification Guide](consumer-verification-752.md)
- [7.5.1 Release Notes](release-notes-751.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
