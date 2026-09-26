# Release Cadence and Readiness

This article defines the release-cadence and release-readiness guidance for the stable AsiBackbone package family.

Release process language should stay grounded in practical governance infrastructure. A stable package line is a compatibility promise and a release-management posture; it is not a claim that every consumer environment has already validated the package over a long adoption window.

## Why this guidance exists

Early stable releases may need fast follow-up patches for package metadata, documentation, release assets, Source Link metadata, SBOM/provenance hardening, or other release-facing details that cannot be changed after packages are published.

That pace is reasonable for a young package family, but governance/security-adjacent consumers also need to see that the project distinguishes routine fixes from additive API work and from major identity or breaking-change events. This document makes that distinction explicit.

## Release streams

| Stream | Use when | Examples | Should not include |
| --- | --- | --- | --- |
| Patch (`x.y.Z`) | The existing public contract remains compatible and the change fixes, clarifies, or hardens the release. | Bug fixes, security fixes, documentation corrections, packaging fixes, NuGet metadata fixes, README/package icon corrections, Source Link metadata fixes, SBOM/provenance workflow fixes, and validation hardening. | New stable public API, new stable package identity, namespace changes, breaking behavior changes. |
| Minor (`x.Y.0`) | The change is backward-compatible but expands the stable package surface or adoption surface. | New optional APIs, new optional providers, compatible options, additional templates, compatible durable-artifact additions, provider improvements. | Package/namespace identity changes, public API breaks, incompatible durable-artifact shape changes. |
| Major (`X.0.0`) | The change intentionally breaks or replaces part of the stable contract and cannot be shipped compatibly. | Package ID changes, namespace changes, removed/renamed public APIs, binary assembly identity changes, dependency-direction breaks, incompatible durable-artifact changes, public default behavior changes that alter consumer outcomes. | Routine package metadata corrections, documentation-only fixes, or compatible additions that can be handled in patch/minor releases. |

Patch releases may happen quickly when fixing package-facing mistakes that are visible to consumers and cannot be overwritten on NuGet. They should still say plainly why a patch is appropriate and confirm that the public API and package identity remain compatible.

Minor releases should be paced enough to let additive surfaces be reviewed, documented, and validated through consumer smoke paths before publication.

Major releases should be rare. They should be reserved for identity, namespace, public API, durable artifact, binary identity, or package-boundary breaks that are strongly justified and documented in advance.

## Current `7.x` stabilization posture

`7.x` is the stable package line maintained on `main`. The current stable release is `7.0.0`.

The `7.0.0` release preserves the package IDs and `net10.0` target while advancing the binary identity to `7.0.0.0`. It renames the `Handshakes` and `CapabilityTokens` namespaces, binds acknowledgment responses to the challenged actor, binds capability grants to the expected subject and operation, and removes permissive zero defaults from the DLP failure-behavior and intent-risk enums.

`7.0.0` is a young major line. Future `7.x` releases should prioritize compatibility, documentation clarity, patch-level release correction, and carefully scoped additive improvements. Additional breaking changes should be avoided unless strongly justified by consumer safety, correctness, maintainability, or a documented architectural boundary that cannot be preserved compatibly.

For cautious consumers, a young major line should be interpreted as canonical but still settling. The project should let that line stabilize through release validation, documentation currency, package metadata correction, consumer smoke testing, and real issue triage before introducing another broad breaking change.

## Prepared and published release wording

Repository `main` can carry a version that is prepared but not yet tagged or published. Evergreen documentation must keep that prepared version distinct from the latest release that NuGet consumers can install.

The publication state is declared in `eng/documentation-release-claims.json`:

```json
"publication": {
  "state": "prepared",
  "latestPublishedVersion": "6.0.0"
}
```

| State | Meaning | Required wording |
| --- | --- | --- |
| `prepared` | `Directory.Build.props` names a version that has not been tagged or published. `latestPublishedVersion` names the latest published stable release and must be lower. | Describe the repository version as the prepared next release and name `latestPublishedVersion` as the latest published stable release. Do not call the prepared version the current release, and do not imply that its APIs are available from NuGet. |
| `released` | The repository version is the release being tagged and published. `latestPublishedVersion` must equal the `Directory.Build.props` version. | Describe the repository version as the current release, and remove prepared, unpublished, and "not yet tagged or published" wording. |

Switching from `prepared` to `released` is an explicit step in the release-preparation pull request, made on the final release-candidate commit before the release tag is created. The switch belongs in that pull request rather than after publication because the tagged commit's README files are packed into the published packages: a tagged commit that still described its own version as prepared would ship that wording to NuGet. The Version Consistency, Stable Release Validation, and package-publishing workflows run the documentation release-claim validator with the release tag, and the validator rejects a stable tag whose commit is still in the `prepared` state. Packages are published only from a tag ref.

Prerelease tags such as `v7.0.0-rc.1` are the exception: they publish prerelease packages only, so the stable version is still unpublished and the tagged commit stays in the `prepared` state.

When the next version begins on `main`, the pull request that advances `Directory.Build.props` switches the state back to `prepared` and sets `latestPublishedVersion` to the release that was just published.

In both states the validator reports every line that still uses the other state's wording, so the switch works as a checklist rather than a search. See [Documentation Release-Claim Validation](../quality/documentation-release-claim-validation.md) for the rules.

## Stabilization window after a major release

After a major release, maintainers should favor a stabilization window before making the line broadly recommended for cautious production adoption.

A stabilization window should focus on:

- package visibility and NuGet metadata correctness;
- package icon and README rendering;
- documentation navigation, release notes, and migration clarity;
- Source Link metadata and repository commit validation;
- package SBOM and provenance artifacts;
- external consumer smoke tests and sample validation;
- issue triage for migration blockers;
- patch-only corrections when the public contract can remain unchanged.

A major line may be described as the recommended line when:

- release-blocking workflows have passed for the release-candidate commit;
- published packages are visible and package metadata has been inspected;
- the documentation site is published and current;
- post-publish Source Link and package metadata checks are completed or explicitly deferred;
- release notes and migration notes are clear enough for a clean consumer project to adopt the line;
- no known migration blocker remains unresolved.

## Pre-release readiness checklist

Before tagging a stable release, the release PR or release-readiness record should confirm the following:

| Area | Confirmation |
| --- | --- |
| Version metadata | `Directory.Build.props`, package filenames, release notes, changelog, citation metadata, Zenodo metadata, and tag expectations align. |
| Package identity | Package IDs, namespaces, stable package list, descriptions, tags, license metadata, project URL, repository URL, and repository commit metadata are current. |
| Package assets | Package icon is regenerated when needed, included in generated packages, and inspected at package-list/detail sizes; packaged README files are present and render acceptably. |
| Documentation links | README links, DocFX navigation, article index, release notes, migration notes, and GitHub Pages links point to current pages. |
| Source Link | Source Link repository commit metadata is generated, and any required post-publish NuGet validation command is documented. |
| SBOM and provenance | Package SBOM files and the SBOM manifest are generated; package and SBOM artifacts are attested where supported; stable releases expose SBOMs, hashes, and release notes as durable public assets. |
| Compatibility | Public API compatibility, stable package boundaries, assembly-version policy, durable schema/version guidance, and provider/package wording are reviewed. |
| Migration | Breaking changes include migration guidance, old/new package IDs, old/new namespaces, representative `PackageReference` and `using` updates, and previous-line support/deprecation posture. |
| Deferred checks | Any intentionally deferred release-critical check records the reason, accepted risk, follow-up issue, and whether release notes need to mention it. |
| Publication wording | The release-preparation pull request switches `eng/documentation-release-claims.json` from `prepared` to `released`, sets `latestPublishedVersion` to the release version, and replaces prepared-release wording so the documentation release-claim validator passes with the release tag. |

## Package identity and namespace changes

Package identity or namespace changes are major-release events because they affect how consumers reference, restore, compile, and document their applications.

A future package identity or namespace change should include at minimum:

1. A proposal or issue explaining why the change cannot be handled compatibly.
2. A migration guide with old package IDs, new package IDs, old namespaces, new namespaces, and representative `PackageReference` / `using` updates.
3. Release notes that identify the breaking boundary near the top of the document.
4. README and documentation updates that name the canonical package line.
5. Compatibility notes for the previous line, including whether it is superseded, deprecated, retained for security fixes only, or retained only for historical traceability.
6. Clean external consumer smoke tests or sample validation using the new package identity.
7. A stabilization note explaining what should settle before another breaking change is considered.

## Wording guidance

Release wording should be confident about what has been validated and humble about what has not.

Prefer:

- "compatible patch release";
- "current canonical package identity line";
- "release-candidate validation passed";
- "no public API changes are intended";
- "post-publish validation completed";
- "known deferred checks are documented".

Avoid:

- implying that a new major line has long consumer adoption history before it does;
- describing patch releases as feature releases;
- calling future provider ideas part of the stable contract before they ship;
- using release notes to defend the project instead of explaining the release boundary.

## Related documentation

- [Governance](https://github.com/AsiBackbone/AsiBackbone/blob/main/GOVERNANCE.md)
- [Release Validation](release-validation.md)
- [7.0.0 Release Readiness Record](release-readiness-700.md)
- [7.0.0 Release Notes](release-notes-700.md)
- [7.0.0 Consumer Verification Guide](consumer-verification-700.md)
- [5.2.0 Release Readiness Record](release-readiness-520.md)
- [4.0.0 Release Readiness Record](release-readiness-400.md)
- [4.0.0 Release Notes](release-notes-400.md)
- [4.0.0 Migration Guide](upgrade-323-to-400.md)
- [4.0.0 Consumer Verification Guide](consumer-verification-400.md)
- [3.2.3 Release Readiness Record](release-readiness-323.md)
- [3.2.3 Release Notes](release-notes-323.md)
- [3.2.3 Consumer Verification Guide](consumer-verification-323.md)
- [3.2.2 Release Readiness Record](release-readiness-322.md)
- [3.2.2 Release Notes](release-notes-322.md)
- [3.2.2 Consumer Verification Guide](consumer-verification-322.md)
- [3.2.1 Release Readiness Record](release-readiness-321.md)
- [3.2.1 Release Notes](release-notes-321.md)
- [3.2.1 Consumer Verification Guide](consumer-verification-321.md)
- [3.2.0 Release Readiness Record](release-readiness-320.md)
- [3.2.0 Release Notes](release-notes-320.md)
- [3.1.0 Release Readiness Record](release-readiness-310.md)
- [3.1.0 Release Notes](release-notes-310.md)
- [3.0.1 Release Readiness Record](release-readiness-301.md)
- [3.0.1 Release Notes](release-notes-301.md)
- [3.0.0 Release Readiness Record](release-readiness-300.md)
- [3.0.0 Release Notes](release-notes-300.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Supply-Chain Provenance and Package SBOMs](supply-chain-provenance.md)
- [Historical Stable API Review](stable-api-review.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
