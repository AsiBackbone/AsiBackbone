# Security Policy

Thank you for taking the time to report security concerns responsibly.

AsiBackbone is a .NET governance and policy-control package family for accountable software decision flow. It is intended to help host applications structure policy evaluation, acknowledgment, audit residue, capability scoping, provider emission, and signing-ready metadata around consequential actions.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a governance spine, not an intelligence engine.

## Supported versions

Security review and vulnerability handling focus on the current stable `5.x` release line.

| Version line | Support posture |
| --- | --- |
| `5.x` | Supported stable line. Please prefer the latest available `5.x` patch when validating or reporting a concern. |
| `3.x` | Previous stable line. Supported only for migration-sensitive reports that also affect or inform the current `5.x` package family. |
| `2.x`, `1.x` | Historical stable lines. Supported only for migration-sensitive reports that also affect or inform the current `5.x` package family. |
| `0.x`, alpha, beta, preview, or historical package lines | Not supported except when a maintainer explicitly asks for comparison or reproduction details. |
| Unreleased `main` branch changes | Reviewed on a best-effort basis before release, but not treated as a supported production release line. |

A report that affects supported `5.x` packages may still result in documentation, sample, analyzer, package, or release-process changes depending on where the actual risk lives.

## Reporting a Vulnerability or Sensitive Concern

Please do **not** place exploit details, secrets, proof-of-concept payloads, private keys, tokens, personal data, or sensitive operational information in a public Issue, pull request, Discussion, commit message, screenshot, or comment.

Preferred reporting path:

1. Open this repository's **Security** tab and select **Report a vulnerability** to use GitHub private vulnerability reporting when it is available.
2. Include a concise title and identify the affected repository area, version, branch, or commit when known.
3. Provide reproduction steps, expected behavior, actual behavior, and the practical security impact.
4. Use synthetic data and redact secrets or identifying information.
5. Allow reasonable time for review before public disclosure.

If private vulnerability reporting is unavailable, open a minimal public Issue stating only that you have a sensitive security report to share. Do not include technical details or sensitive material in that Issue.

For non-sensitive hardening suggestions, documentation corrections, or defense-in-depth improvements, a normal GitHub Issue or pull request is appropriate.

## Expected Response Posture

This is a community-maintained open-source project and does not promise a formal security-response SLA or fixed acknowledgment or remediation timelines.

The expected best-effort process is:

1. A maintainer reviews the report and determines whether it is a vulnerability, documentation issue, sample or template issue, workflow or dependency concern, hardening opportunity, duplicate, or out-of-scope report.
2. The maintainer may request clarification, affected-version or commit information, sanitized logs, or a reduced reproduction.
3. Confirmed concerns are addressed through code, documentation, dependency, workflow, repository-configuration, release, or advisory changes appropriate to the risk.
4. Public communication distinguishes confirmed behavior from suspected risk and avoids overstating security, compliance, legal, or operational guarantees.

Please avoid repeated public disclosure while a sensitive report is being reviewed.

## Sensitive Data Guidance for Reports

When reporting a concern:

- redact passwords, secrets, tokens, private keys, certificates, connection strings, user identifiers, personal information, customer data, and regulated data;
- use synthetic examples whenever possible;
- share only the minimum information required to reproduce or understand the concern;
- clearly identify any material that remains sensitive.

## Safe Public Language Expectations

Public communication should distinguish implemented controls from intended architecture, repository behavior from downstream or consuming-system behavior, and verified evidence from assumptions.

Do not describe a repository, package, template, sample, workflow, or generated application as vulnerability-free, automatically compliant, legally sufficient, tamper-proof, or production-ready solely because a documented control exists or automated checks pass.

## Advisory distribution and downstream notification

Publishing a repository security advisory is the public disclosure step, but it is not by itself proof that downstream package-consumer signals are active. GitHub reviews published repository advisories for inclusion in the global GitHub Advisory Database, and [GitHub documents that this review can take up to 72 hours](https://docs.github.com/en/code-security/concepts/vulnerability-reporting-and-management/repository-security-advisories).

For a security release, the maintainer should:

1. Publish fixed packages before publishing the corresponding repository advisories.
2. Confirm each advisory identifies the supported ecosystem/package, affected range, and fixed version accurately.
3. Run `./scripts/Manage-SecurityAdvisoryDistribution.ps1` after publication and again after the documented review window.
4. Treat a missing global entry inside the review window as pending curation rather than as proof of failed submission.
5. If a published advisory remains absent after the review window, review the advisory metadata and use the script's explicit `-RequestMissingCves` path when a GitHub-issued CVE is appropriate. Use `-WhatIf` before requesting CVEs.
6. Continue checking until each published advisory resolves from the global advisory endpoint. Dependabot, NuGet vulnerability metadata, and `dotnet list package --vulnerable` should not be described as carrying the advisory until that downstream state is observed.
7. Use NuGet package deprecation independently when affected package versions need a direct package-registry warning; deprecation does not replace advisory distribution and advisory distribution does not replace deprecation.

The CVE-request path requires an authenticated repository administrator or security manager, or a token with "Repository security advisories" write permission. The script uses the existing `gh` authentication context and does not store a publishing or advisory-management credential in the repository.

## Repository-host security controls

Repository-host protections are treated as part of the project's supply-chain
boundary rather than as an implicit GitHub administrator default. The canonical
desired state is documented in
[Repository Host Security Controls](docs/articles/repository-host-security-controls.md)
and represented by `eng/repository-controls/main-branch-ruleset.json`.

The maintained posture requires secret scanning and secret-scanning push
protection, an explicit ruleset for `main`, the existing release-blocking status
checks, pull-request-only changes, squash-only merge into `main`, linear history,
and protection against branch deletion and force pushes. The sole emergency
bypass actor is repository-specific and may bypass the ruleset only through a
pull request so the reason and resulting change remain reviewable and auditable.

Audit the live GitHub settings with:

```powershell
./scripts/Manage-RepositorySecurityControls.ps1
```

The script also exposes an explicit `-Apply` path for an authenticated repository
administrator. Use `-WhatIf` first. Repository-host controls are not changed by a
normal build or package release workflow.

## Project security boundaries

AsiBackbone provides governance-oriented building blocks and host integration seams. It does **not** provide end-to-end production security, compliance, or legal assurance by itself.

The package family does not:

- implement an intelligence engine;
- host, train, run, or orchestrate AI models;
- control physical systems by itself;
- replace authentication, authorization, legal review, compliance review, operational security, DLP review, or organizational accountability;
- certify compliance with any law, regulation, audit framework, or security standard;
- provide production tamper-evidence, immutability, legal non-repudiation, external anchoring, or blockchain-backed assurance by default;
- own the consuming application's persistence, execution behavior, deployment model, observability backend, exporter configuration, retention policy, key custody, verification path, incident response, or production hardening.

Host applications remain responsible for deciding how AsiBackbone decisions are enforced before side effects occur.

## Signing and key-handling boundaries

`AsiBackbone.Signing.LocalDevelopment` is for tests, samples, local validation, and wiring proof paths only. It is **not** a production key-custody or production signing control.

`AsiBackbone.Signing.ManagedKey` provides an adapter boundary. The host supplies the actual managed-key client, credentials, key operations, verification path, monitoring, operational policy, and incident response.

Signing-ready metadata, signed records, verification results, hash chains, and externally anchored evidence are distinct states. Do not assume that a record is tamper-evident, immutable, legally non-repudiable, externally anchored, or compliance-certified unless the host has deployed and verified the full operational trust design that proves that claim.

## Package signing status

AsiBackbone packages are intentionally published without author signing. NuGet.org
adds its own repository signature to the package bytes it serves; that repository
signature is distinct from an AsiBackbone maintainer or author signature.

This is a deliberate governance decision while the project is independently maintained, balancing operational complexity against practical value. At this stage, the project prioritizes transparent source code, deterministic build and release practices, GitHub releases, Source Link, SBOM generation, and package provenance over maintaining signing certificates and supporting release-signing infrastructure.

Consumers should validate package identity through the official NuGet package source, package version, repository metadata, Source Link information, GitHub release tags, and available integrity metadata provided by NuGet tooling.

Package signing remains deferred under the dated [NuGet Package Signing Decision Record](docs/articles/nuget-package-signing-decision.md). That record requires review by 2027-03-31 or before the first `6.0.0` release candidate, whichever occurs first, and defines earlier re-evaluation triggers for maintainer capacity, consumer or regulatory requirements, suitable signing infrastructure, and supply-chain incidents. Until package signing is formally adopted and documented, AsiBackbone should not be described as providing signed release artifacts, repository-signed packages, or Authenticode-signed packages.

### Current trust model

Current package trust is established through:

- Official NuGet package publication
- Public GitHub source repository
- GitHub release tags
- Source Link integration
- Durable release-attached Software Bills of Materials (SBOMs)
- Package and SBOM provenance attestations
- Release evidence and package SHA-256 mappings

These mechanisms provide transparency and traceability while package signing remains deferred.

Download the attested build package from the matching GitHub release and verify
its workflow provenance by subject digest:

```powershell
gh release download v5.1.0 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.5.1.0.nupkg'
gh attestation verify ./AsiBackbone.Core.5.1.0.nupkg --repo AsiBackbone/AsiBackbone
```

NuGet.org repository-signs packages during ingestion, so a package downloaded
from NuGet.org has a different digest from the attested build package. Verify
that distribution separately with `dotnet nuget verify --all <package-path>`.

## Scope examples

Examples that are generally in scope:

- a package bug that allows governance decisions, acknowledgments, capability checks, or audit persistence to be bypassed contrary to documented behavior;
- unsafe sample guidance that could encourage production use of local-development signing controls;
- documentation wording that could reasonably cause consumers to believe AsiBackbone certifies compliance, provides production tamper-evidence by default, or offers legal protection by itself;
- a denial-of-service or data-exposure issue in package code, sample code, or supported host integration paths.

Examples that are generally out of scope:

- requests for legal, compliance, or certification guarantees;
- vulnerabilities caused solely by a consuming host application's custom execution logic, deployment configuration, key management, database security, cloud policy, or network controls;
- reports against unsupported historical package lines unless the same behavior affects the supported stable `3.x` line;
- claims that AsiBackbone should prevent all misuse of AI, agents, robotics, or host-side tools without a specific package-level or documentation-level vulnerability.

## Related Project Policies

- [Support Policy](SUPPORT.md)
- [Maintainers](MAINTAINERS.md)
- [Governance](GOVERNANCE.md)
- [Contributing](CONTRIBUTING.md)
