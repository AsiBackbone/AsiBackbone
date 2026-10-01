# Post-2.0.0 Documentation Currency Review

This record captures the post-release documentation currency review for issue #347.

> [!NOTE]
> This page is a historical record of the documentation currency review performed after the `2.0.0` release. Statements about the "current" stable package family describe the posture at the time of that review, not the current release line. For current stable package guidance, see [API Compatibility and Semantic Versioning](api-compatibility-and-semver.md) and [Releases & Compatibility](../releases/index.md).

The review goal is to confirm that the public documentation still matches the stable `2.0.x` package family after the `2.0.0` release and continues to describe AsiBackbone as practical Accountable Systems Infrastructure: a governance spine for policy-controlled decision flow, not an intelligence engine or completed ASI implementation.

## Review scope

The review covered the public documentation surfaces most likely to shape first impressions, package adoption, and release positioning:

- root `README.md` package-family summary and start-here links;
- `docs/index.md` documentation home and navigation posture;
- `docs/articles/index.md` categorized article map;
- current quickstart and implementation-first adoption pages;
- historical `1.0.0` quickstart positioning;
- template installation guidance;
- release validation and SemVer guidance;
- project-boundary and production-wording guidance;
- package names, namespace wording, and stable-provider boundaries.

## Stable package family at the time of review

At the time of this review, the stable documentation posture was `2.0.x`, with `2.0.0` as the major release boundary for the simplified package and namespace identity:

```text
AsiBackbone.Core
AsiBackbone.DependencyInjection
AsiBackbone.Storage.InMemory
AsiBackbone.EntityFrameworkCore
AsiBackbone.AspNetCore
AsiBackbone.Testing
AsiBackbone.Templates
AsiBackbone.Analyzers
AsiBackbone.OpenTelemetry
AsiBackbone.Signing.LocalDevelopment
AsiBackbone.Signing.ManagedKey
```

The documentation should continue to treat Event Hubs, Purview, Azure-specific SDK adapters, robotics, immutable storage, and additional provider packages as design-only, strategy-only, sample-only, host-owned, or future-provider work unless a later stable release explicitly ships them.

## Currency checks

| Area | Review result |
| --- | --- |
| Package and namespace identity | Current docs use the simplified `AsiBackbone.*` identity and preserve the `CDCavell.AsiBackbone.*` wording as historical migration context. |
| Implementation-first path | Quickstart and adoption pages route readers toward API gating, reference deployment evidence, terminology, endpoint governance, durable audit/outbox persistence, OpenTelemetry, and signing boundaries. |
| Stable package list | Root README, docs home, article index, and SemVer guidance identify the current `2.0.x` package family. |
| Historical release material | `1.0.0`, `1.1.x`, `1.2.0`, and `1.2.1` material remains available as historical context and should not be presented as the current adoption path. |
| Template guidance | Template install guidance should use the current `2.0.0` package artifact name for local validation examples. |
| Signing and tamper-evidence wording | Production wording should describe signing-related surfaces as carried forward into the current `2.0.x` line, while preserving local-development and managed-key adapter limitations. |
| Project boundaries | Documentation continues to state that AsiBackbone does not implement artificial superintelligence, host AI models, control robots, certify compliance, or provide production tamper-evidence by default. |
| Runtime behavior | This review does not introduce runtime behavior changes or public API changes. |

## Updates made during this pass

- Added this review record so the post-`2.0.0` documentation verification is visible in the documentation set.
- Linked the review record from the article table of contents.
- Updated template local-package installation guidance from the previous `1.2.1` artifact example to the current `2.0.0` package artifact example.
- Updated the historical `1.0.0` quickstart's current-family pointer so readers are directed to `2.0.0` release notes and current quickstart material.

## Follow-up expectations

Before merging documentation changes, the pull request should rely on the repository validation workflows to confirm:

- DocFX builds successfully;
- markdown links and navigation entries resolve;
- package metadata validation remains aligned with the current package family;
- no documentation language implies AsiBackbone is an intelligence engine, a robotics control system, a compliance product, or production tamper-evidence by default.

## Related documentation

- [Implementation-First Adoption Path](implementation-first-adoption.md)
- [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [2.0.0 Release Notes](release-notes-200.md)
- [2.0.0 Release Readiness Record](release-readiness-200.md)
- [Release Validation](release-validation.md)
