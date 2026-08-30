# Release Cadence and Readiness

This article defines the release-cadence and release-readiness guidance for the stable AsiBackbone package family.

In this software project, **ASI** means **Accountable Systems Infrastructure**. Release process language should stay grounded in practical governance infrastructure. A stable package line is a compatibility promise and a release-management posture; it is not a claim that every consumer environment has already validated the package over a long adoption window.

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

## Current `3.x` stabilization posture

`3.x` is the current stable package line, and `3.2.3` is the current patch release.

The historical `3.0.0` release established the major-line binary identity while preserving the existing `AsiBackbone.*` package IDs and namespaces.

Future `3.x` releases should prioritize compatibility, documentation clarity, patch-level release correction, and carefully scoped additive improvements. Additional breaking changes should be avoided unless strongly justified by consumer safety, correctness, maintainability, or a documented architectural boundary that cannot be preserved compatibly.

For cautious consumers, a young major line should be interpreted as canonical but still settling. The project should let that line stabilize through release validation, documentation currency, package metadata correction, consumer smoke testing, and real issue triage before introducing another broad breaking change.

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
| SBOM and provenance | Package SBOM files and the SBOM manifest are generated; package and SBOM artifacts are uploaded and attested where the workflow event supports attestation. |
| Compatibility | Public API compatibility, stable package boundaries, assembly-version policy, durable schema/version guidance, and provider/package wording are reviewed. |
| Migration | Breaking changes include migration guidance, old/new package IDs, old/new namespaces, representative `PackageReference` and `using` updates, and previous-line support/deprecation posture. |
| Deferred checks | Any intentionally deferred release-critical check records the reason, accepted risk, follow-up issue, and whether release notes need to mention it. |

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

- [Governance](../../GOVERNANCE.md)
- [Release Validation](release-validation.md)
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
