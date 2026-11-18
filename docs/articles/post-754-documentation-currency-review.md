# Post-7.5.4 Documentation Currency Review

This record captures the documentation currency review for [issue #945](https://github.com/AsiBackbone/AsiBackbone/issues/945), completed after the first pass in pull request #946.

> [!NOTE]
> This page is a dated record of a review performed on 2026-11-18 against the published `7.5.4` release. It describes what was reviewed and decided at that time. For the current release and its evidence, see [Releases & Compatibility](../releases/index.md); for compatibility rules, see [API Compatibility and Semantic Versioning](api-compatibility-and-semver.md).

## Baseline

- Published, non-draft, non-prerelease releases: AsiBackbone `7.5.4`, NetCoreApplicationTemplate `2.11.10`, Reference Application `1.0.2`, and Learning `1.6.0`.
- Source reviewed: `main` after #946, with the committed public API baselines in `eng/api-baseline/` (each headed `# Baseline release: 7.5.4`).
- Rule applied: deeper pages describe verified current behavior without naming a release; version references stay only where they carry compatibility, migration, introduction, reproducibility, release-evidence, or historical meaning. Current release discovery stays on the documentation homepage, the articles index, Releases & Compatibility, Major Version History, and the current section of API Compatibility and Semantic Versioning.

## Inventory and classification

All 245 Markdown files under `docs/` were classified before any change. Equivalent surfaces outside `docs/` that state documentation facts (`README.md`, package READMEs, and `eng/api-baseline/README.md`) were reviewed with them.

| Classification | Pages | Treatment |
| --- | --- | --- |
| Current guidance (articles, scenarios, samples, concepts) | 124 | Reviewed in full for currency, behavior, and terminology |
| Quality reports | 8 | Reviewed against CI configuration and issue state |
| Navigation and entry points (`docs/index.md`, `docs/articles/index.md`, `docs/releases/index.md`, `docs/maintainers/index.md`) | 4 | Central release discovery; exact current-release statements retained and enforced by the release-claim validator |
| Dated reviews (`package-discovery-metadata-review.md`, `policy-evaluator-allocation-review.md`) | 2 | Dated evidence; unchanged |
| Release notes | 35 | Historical; unchanged |
| Release readiness records | 34 | Historical; unchanged |
| Consumer verification guides | 24 | Per-release evidence; unchanged |
| Upgrade guides | 5 | Valid for the version span they name; unchanged |
| Quickstarts | 2 | Pinned installation examples; unchanged |
| Historical reviews, records, and archive pages | 7 | Historical; unchanged |

## Method

A version-text search alone was not treated as sufficient. The review combined four passes with source verification:

1. **Version claims:** every version token on a current page that appeared near a currency, stability, or future word, and every mention of an earlier major line, was read in context and classified.
2. **Work-status claims:** every list or sentence that presents issues as tracked, open, planned, or pending was checked against the issue's actual state on GitHub.
3. **API names:** every type-like identifier on a current page was compared with the `7.5.4` public API baselines and the source tree.
4. **Terminology:** current pages were checked for the vocabulary retired by `eng/docs/current-terminology.json` and for the legacy "ASI Backbone" spelling.

Each behavior claim that was corrected was checked against the implementation, tests, CI configuration, or changelog named below.

## Findings and corrections

| Page | Finding | Correction | Verified against |
| --- | --- | --- | --- |
| `capability-grant-hardening.md` | Stated that `CapabilityGrantValidator.ValidateAsync(signedGrant)` without options "preserves the existing 3.x behavior" and validates metadata. Since `5.0.0` that call denies. | Describes the fail-closed result and the `capability.validation-options-required` code; the migration section now treats the permissive path as closed history. | `CapabilityGrantValidator.cs`, `CapabilityGrantValidationProfileTests`, `CHANGELOG.md` `5.0.0` |
| `schema-versioning.md` | Said the "`1.0.0` package" has no durable capability-grant record and that a future one should carry a schema discriminator. | Documents `CapabilityGrant.SchemaVersion`, the `CapabilityGrantId` and retained `CapabilityTokenId` references, and the EF Core use store table. | `CapabilityGrant.cs`, `EfCoreCapabilityGrantUseStore`, `CapabilityGrantUseEntityConfiguration` |
| `quality/index.md` | Listed seven issues as "tracked" hardening work; all closed between June and July 2026. | Relabeled as completed work kept for traceability. The 75% line and 90% Core branch gates were confirmed. | GitHub issue states; `.github/workflows/ci.yml` |
| `api-baseline-and-boundary-checks.md`, `release-validation.md`, `eng/api-baseline/README.md` | Described the protected surface as the "stable `5.x` package line" or the "reviewed `7.0.0`" API. | Version-neutral; points to each baseline file's release header. | `eng/api-baseline/*.txt` headers |
| `observability-and-governance-emission-architecture.md` | Framed the whole current boundary as "`3.1.0`". | Describes the current package family; `3.1.0` kept as the release that established the boundary. | Packable projects under `src/` |
| `signing-ready-receipts-and-key-handling.md` | Attributed the primitives to `3.0.0`, then `4.0.0`, then `3.0.0` again. They came from the `1.1.0` milestone. | Corrected introduction and status; links the signature-input versioning that began in `6.0`. | `release-notes-110.md`; `cryptographic-security-posture.md` |
| `release-validation.md` | Named `7.5.2` as the patch preceding `7.5.4`; `7.5.3` shipped between them. | Routes readers to Releases & Compatibility. | `CHANGELOG.md` |
| `event-hubs-governance-emission-provider-design.md`, `purview-governance-lineage-enrichment-strategy.md`, `progressive-adoption.md`, `release-cadence-and-readiness.md`, `release-validation.md` | Deep pages named `7.5.4` as current and would go stale at the next release. | Version-neutral wording with a link to Releases & Compatibility. | Release-claim validator scope |
| `signed-audit-and-outbox-records.md`, `cryptographic-security-hardening-roadmap.md`, `decision-receipt-observability-schema.md`, `dlp-classification-failure-policy.md`, `governance-emission-contract.md`, `opentelemetry-governance-emission-provider-design.md`, `templates.md`, `developer-checklist.md`, `policy-evaluator-pipeline.md` | Introduction versions read as the current boundary. | Introduction versions labeled as history; current status stated without a version. | Public API baselines; package list |
| `public-api-naming-600.md` | "Planned `6.x` deprecations" never happened: no `6.x` minor release shipped, and `7.0.0` renamed the names directly. | Outcome note added; the `6.0` plan is kept as the record. | `CHANGELOG.md`; `eng/api-baseline/*.txt` |
| `host-mutation-accountability.md` | Said a closed NetCoreApplicationTemplate issue "owns" the NCAT contract. | NCAT owns it; the issue is cited as where it was introduced. | Issue state |
| `branch-retention-policy.md` | "Current state" claimed the repository holds the two active branches; the live repository has only `main`. | Restated as the state when the policy was adopted. The policy question is listed as follow-up. | `git ls-remote --heads` |
| Terminology: `aspnetcore-endpoint-governance.md`, `core-governance-flow-diagrams.md`, `strict-governance-profile.md`, `threat-model-contributors.md`, `robotics-operational-gateway.md`, `purview-…`, `event-hubs-…`, `observability-…`, `progressive-adoption.md`, `signing-ready-…`, `quality/index.md`, `core-branch-coverage.md` | Current prose used "capability token", which `current-terminology.json` retired in favor of "capability grant". | Prose updated. Retained identifiers, failure codes, issue titles, and history sections unchanged. | `eng/docs/current-terminology.json` |
| `ef-core-host-ownership-and-migrations.md` | Used the legacy "ASI Backbone" spelling 17 times. | Uses "AsiBackbone". | Landing-page standard naming |
| `schema-versioning.md`, `project-boundaries.md` | Earlier blanket rename left "decision receipt, decision receipts". | Duplicates removed. | — |
| `documentation-ownership-inventory.md` | Continuity rule referred to "the current 5.x consumer path". | Version-neutral. | — |

## Regression coverage

`scripts/Validate-DocumentationReleaseClaims.ps1` now treats "package line" like "package family" and "release line" in its major-line claim patterns, so prose such as "the stable `5.x` package line" fails on any page the validator scans. The `stale-current` fixture in `eng/test-fixtures/documentation-release-claims/` and `scripts/Test-DocumentationReleaseClaims.ps1` cover the new phrasing.

## Reviewed and retained

| Reference | Purpose |
| --- | --- |
| Exact current release on the homepage, articles index, Releases & Compatibility, Major Version History, and the current section of API Compatibility and Semantic Versioning | Central release discovery, enforced by the release-claim validator |
| `7.x` line descriptors, `AssemblyVersion 7.0.0.0`, the `8.0` removal plans for `ASIB901`–`ASIB903`, and the target-framework decision | Compatibility and support boundaries |
| "Introduced in" and "since" versions | Feature-introduction history |
| Schema versions, canonical JSON version, and wire values | Fixed protocol and schema identities |
| The NCAT `v2.11.0` contract-vector pin | Reproducible integration evidence |
| Review triggers before the first `8.0.0` release candidate | Decision-record review cadence |
| "Handshake" in articles | Retained for the multi-step protocol and persisted identifiers, as `current-terminology.json` allows |

## Published counterparts

On 2026-11-18 the deployed cryptographic posture page already showed the #946 corrections, matching `main`, so that page had no deployment drift. Search-engine caches were not used as evidence. The pages changed in this pass must be checked after the documentation workflow publishes them.

## Follow-up

- **Branch retention policy versus practice.** `eng/repository-controls/branch-retention-policy.json` lists `issue_work` as a permanent active branch, but it no longer exists and pull requests now use per-issue branches such as `issue_945_work`, which match no disposable pattern (`issue-*` uses a hyphen). The maintainer should decide whether to update the policy or the branch practice; the audit would report such a branch as `UNCLASSIFIED`.
- **Terminology gate scope.** `current-terminology.json` scans only summary surfaces. Extending it to articles, with allowances for retained identifiers, would keep this pass's terminology corrections from regressing.
- **Retained anchors.** The `Capability-token validation` heading in `cryptographic-security-posture.md` and the roadmap's phase title keep their wording so existing anchors and issue titles still resolve.
- **Learning-owned titles.** Links to Learning's "Acknowledgment and Audit Residue" tutorial keep Learning's page title; any rename belongs in Learning.
