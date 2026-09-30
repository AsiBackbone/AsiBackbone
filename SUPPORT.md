# Support Policy

AsiBackbone is a .NET package family for governance-oriented decision flow. Support is focused on the package source, public APIs, templates, samples, documentation, package-consumer behavior, and release artifacts maintained in this repository.

## Support Channels

Use [GitHub Discussions](https://github.com/AsiBackbone/AsiBackbone/discussions) for:

- Setup, adoption, and usage questions.
- Design or integration guidance that is not a package defect.
- Community feedback and open-ended ideas that are not yet focused feature requests.

Use [GitHub Issues](https://github.com/AsiBackbone/AsiBackbone/issues) for:

- Reproducible defects in released packages, templates, samples, or repository tooling.
- Documentation gaps or incorrect examples.
- Packaging, installation, compatibility, or Source Link problems.
- Security-adjacent hardening work that is safe to discuss publicly.
- Focused feature requests that fit the documented package and host-ownership boundaries.

Use the private vulnerability reporting process described in [SECURITY.md](SECURITY.md) for suspected vulnerabilities or reports containing sensitive details.

## Support Expectations

Support is provided on a best-effort basis by the project maintainers. The triage targets in [GOVERNANCE.md](GOVERNANCE.md) guide project operations but are not a service-level agreement.

Users can expect:

- Public triage when a report contains enough information to evaluate.
- Security reports and release-blocking defects to receive higher priority than general feature requests.
- Reproducible package or template defects to be prioritized over host-specific customization requests.
- Documentation corrections to be considered when they clarify supported behavior or project boundaries.
- Compatible patch releases when a fix is appropriate for the current stable line.

Users should not expect:

- Guaranteed response or resolution times.
- Private consulting, production incident response, legal or compliance advice, or environment-specific debugging.
- Support for every historical release line, prerelease build, or unreleased `main` branch revision.
- Support for host-owned infrastructure, policy content, deployment configuration, key custody, persistence, or execution behavior unless the problem reproduces in AsiBackbone itself.
- A guarantee that AsiBackbone makes a consuming system secure, compliant, tamper-proof, or legally sufficient.

## Version Support Lifecycle

| Version line | Support expectation |
|:---|:---|
| `7.x` | Current stable line. Supported for reproducible defects, security fixes, compatible package corrections, and documentation fixes. Consumers should use the latest available `7.x` patch. |
| `6.x` | Previous stable line. Fixes for High or Critical vulnerabilities through 2027-03-26, six months after the `7.0.0` release; a security advisory with mitigation guidance is published when a fix cannot be backported compatibly. Other reports are best effort. |
| `5.x` | Historical stable line. Best effort for migration-sensitive reports that also affect or inform the current stable line. |
| Earlier stable lines | Best effort for migration-sensitive reports that also affect or inform the current stable line. Historical releases do not receive routine backports. |
| Alpha, beta, preview, and other prerelease builds | Evaluation only unless a maintainer explicitly requests testing or reproduction details. |
| Unreleased `main` branch | Development line only. Behavior may change before release. |

The security-specific lifecycle and private reporting process are authoritative in [SECURITY.md](SECURITY.md).

## Deprecation and Breaking-Change Commitments

The authoritative policy is [Deprecation and major-release policy](docs/articles/api-compatibility-and-semver.md#deprecation-and-major-release-policy). In summary:

- Stable public APIs are removed or renamed only in a major release, after an `[Obsolete]` warning with an `ASIB9xx` diagnostic ID and a shipped replacement.
- A deprecation stays available for at least 90 days and at least one subsequent minor release before removal.
- At most one major release is published in any rolling six-month period, and each is announced in GitHub Discussions at least 30 days before tagging. The next major release after `7.0.0` will not be tagged before 2027-03-26.
- After a new major release, the preceding line receives High and Critical vulnerability fixes for six months.
- A High or Critical vulnerability that cannot be fixed compatibly may override these timelines; the release documents the justification and provides migration and mitigation guidance.

## Issue Triage

Issues are generally reviewed for:

1. Reproducibility and affected package version.
2. Security, compatibility, or release impact.
3. The affected boundary: Core, dependency injection, ASP.NET Core, EF Core, storage, signing, telemetry, analyzers, templates, samples, documentation, or release tooling.
4. Whether the behavior belongs to AsiBackbone or to a consuming host application.
5. Whether the report includes enough detail to validate safely.

Maintainers may close reports that are duplicated, stale, unreproducible, outside the project boundary, or specific to unsupported host customization. The detailed triage workflow is defined in [GOVERNANCE.md](GOVERNANCE.md).

## Pull Request Support

Pull requests should follow [CONTRIBUTING.md](CONTRIBUTING.md). Changes to public APIs, package boundaries, persisted artifacts, security posture, release behavior, or governance may require proposal review or additional evidence even when CI passes.

