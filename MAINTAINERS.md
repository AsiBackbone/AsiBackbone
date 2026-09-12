# Maintainers

AsiBackbone currently operates under the bootstrap solo-maintainer model defined in [GOVERNANCE.md](GOVERNANCE.md). This file records operational ownership; `GOVERNANCE.md` remains authoritative for project roles, decisions, voting, and succession.

## Current Maintainer

| Maintainer | Role |
|:---|:---|
| [@cdcavell](https://github.com/cdcavell) | Core Maintainer; architecture, API, release, security, CI/CD, documentation, and community owner |

## Maintainer Responsibilities

The maintainer is responsible for:

- Reviewing and merging pull requests and keeping the stable package boundary coherent.
- Maintaining public API compatibility, package metadata, templates, samples, and consumer documentation.
- Reviewing workflow, dependency, security, signing, provenance, and release-process changes.
- Coordinating private vulnerability reports, advisories, security fixes, and downstream advisory distribution.
- Maintaining release-blocking validation, branch protections, repository rulesets, and CODEOWNERS coverage.
- Approving package publication through protected GitHub environments.
- Managing NuGet package publication, GitHub Releases, documentation publication, and Zenodo archival sequencing.
- Keeping governance, support, contribution, security, release, and maintainer documentation current.

## Release Cadence

AsiBackbone does not promise a fixed release calendar. Releases follow Semantic Versioning and the readiness process documented in [Release Cadence and Readiness](docs/articles/release-cadence-and-readiness.md) and [GOVERNANCE.md](GOVERNANCE.md).

Expected release behavior:

- Patch releases may address security defects, compatible bug fixes, packaging corrections, documentation-critical fixes, or release-validation hardening.
- Minor releases may add backward-compatible APIs, providers, templates, or other compatible improvements after the required review.
- Major releases require strong justification, explicit migration guidance, and the significant-decision process.
- Release timing depends on issue readiness, CI health, package validation, documentation readiness, and maintainer availability.

## Publishing Ownership

Official release artifacts are produced only through maintainer-controlled workflows. Publishing ownership includes:

- The stable `AsiBackbone.*` NuGet package family.
- GitHub Releases, annotated release tags, release notes, and release evidence.
- Package SBOMs and build-provenance attestations produced by repository workflows.
- The AsiBackbone documentation site.
- Zenodo archival metadata and DOI-bearing releases.
- Post-publication package, Source Link, advisory, and documentation verification.

External contributors are not expected to publish packages or manage release credentials. NuGet package signing is currently deferred as documented in [SECURITY.md](SECURITY.md).

## Branch Protection Expectations

The `main` branch is the stable integration branch. Its expected controls are documented in [Repository Host Security Controls](docs/articles/repository-host-security-controls.md) and represented by `eng/repository-controls/main-branch-ruleset.json`.

Under the current bootstrap solo-maintainer model:

- Changes reach `main` through pull requests.
- Release-blocking status checks must pass before ordinary merge.
- CODEOWNERS records ownership and review routing.
- Required independent approval, Code Owner approval, and last-push approval remain disabled while only one active Core Maintainer exists.
- Review-thread resolution is required, and force pushes and branch deletion are blocked.
- Squash merge and linear history preserve a reviewable stable branch.
- Emergency bypass is repository-specific, pull-request-only, and documented rather than a routine direct-push path.

## Adding or Removing Maintainers

Maintainer changes follow [GOVERNANCE.md](GOVERNANCE.md). Before expanding maintainership, review repository permissions, independent approval requirements, the emergency bypass actor, protected environments, package publishing access, advisory access, and CODEOWNERS coverage.

Update this file, `GOVERNANCE.md`, `.github/CODEOWNERS`, and the repository-host security controls together whenever maintainer ownership changes.

