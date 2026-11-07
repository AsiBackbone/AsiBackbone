# AsiBackbone 7.5.3 Release Notes

Release date: 2026-11-06

`7.5.3` is a backward-compatible maintenance patch for the stable `7.x`
package family. It improves package discovery, evaluation guidance,
terminology and documentation validation, visual identity, and repository
tooling. Package and file versions advance to `7.5.3` and `7.5.3.0`; binary
assembly identity remains `7.0.0.0`.

## Package discovery and terminology

- `AsiBackbone.Core` adds the reviewed `policy-as-code` and
  `capability-grant` NuGet tags while retaining `capability-token` for
  compatibility and search continuity. Its package description now names
  policy evaluation, decision receipts, and capability grants.
- The package metadata validator pins the reviewed discovery vocabulary, so
  future package work cannot silently drop those terms.
- Current-facing README and package documentation now use the canonical 7.0
  capability-grant and acknowledgment vocabulary. Validation and fixtures
  distinguish current product language from retained historical, compatibility,
  schema, and generated-documentation terms.

These changes improve how consumers discover and understand the existing
package surface. They do not add a public API or runtime capability.

## Evaluation and project guidance

- The README adds a five-minute evaluator path based on the published
  `AsiBackbone.Core` package. One request satisfies a host-owned region
  constraint and reaches host code; a second request is denied before the host
  action executes.
- Release validation pins the evaluator to the package-family version and runs
  it against locally packed packages as part of the external-consumer smoke
  path.
- Project-selection guidance now gives AsiBackbone, Learning,
  NetCoreApplicationTemplate, and ReferenceApplication distinct roles and
  routes feedback to the relevant organization discussion categories.
- ReferenceApplication guidance now reflects its stable `1.x` line.

## Documentation, identity, and tooling

- Release-document continuity validation now rejects stale current-release
  labels, local paths, and published destinations while retaining explicitly
  historical release links.
- Citation and Zenodo metadata use the canonical `AsiBackbone` product name,
  with version validation enforcing exact agreement.
- Repository, documentation, package, favicon, and social-preview assets now
  use the governance-spine glyph from the AsiBackbone glyph family.
- Documentation search and navigation follow the shared organization contract,
  the README includes the OpenSSF Scorecard badge, and ReportGenerator plus
  pinned GitHub Actions receive maintenance updates.

## Compatibility

No stable public signature, source implementation, durable schema, canonical
wire value, package ID, public namespace, target framework, documented default,
or runtime decision behavior changes in this patch. Declared runtime dependency
minimums are unchanged from `7.5.2`.

The new evaluator is sample and validation content. Package descriptions, tags,
visual assets, documentation, and repository workflows do not change generated
or adopted application behavior.

## Upgrade actions

1. Update adopted `AsiBackbone.*` packages to `7.5.3` together.
2. Restore and inspect the resolved dependency graph, then rebuild with warnings
   enabled and run the consumer's integration tests.
3. If package discovery or governance terminology is surfaced in internal
   catalogs, refresh that metadata from the `7.5.3` packages.

Consumers moving from `6.x` must still complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

## Release verification

Stable publication occurs only from the protected `v7.5.3` tag after the
release-preparation pull request is merged to `main` and required hosted checks
pass on the release candidate. The date above is the planned release date;
publication and hosted evidence must be verified separately.

See also:

- [7.5.3 Release Readiness Record](release-readiness-753.md)
- [7.5.3 Consumer Verification Guide](consumer-verification-753.md)
- [7.5.2 Release Notes](release-notes-752.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
