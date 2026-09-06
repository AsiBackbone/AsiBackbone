# Security Policy

Thank you for taking the time to report security concerns responsibly.

AsiBackbone is a .NET governance and policy-control package family for accountable software decision flow. It is intended to help host applications structure policy evaluation, acknowledgment, audit residue, capability scoping, provider emission, and signing-ready metadata around consequential actions.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a governance spine, not an intelligence engine.

## Supported versions

Security review and vulnerability handling focus on the current stable `4.x` release line.

| Version line | Support posture |
| --- | --- |
| `4.x` | Supported stable line. Please prefer the latest available `4.x` patch when validating or reporting a concern. |
| `3.x` | Previous stable line. Supported only for migration-sensitive reports that also affect or inform the current `4.x` package family. |
| `2.x`, `1.x` | Historical stable lines. Supported only for migration-sensitive reports that also affect or inform the current `4.x` package family. |
| `0.x`, alpha, beta, preview, or historical package lines | Not supported except when a maintainer explicitly asks for comparison or reproduction details. |
| Unreleased `main` branch changes | Reviewed on a best-effort basis before release, but not treated as a supported production release line. |

A report that affects supported `4.x` packages may still result in documentation, sample, analyzer, package, or release-process changes depending on where the actual risk lives.

## How to report a vulnerability or sensitive concern

Please do **not** place exploit details, secrets, proof-of-concept payloads, private keys, customer data, or sensitive operational information in a public issue, discussion, pull request, or comment.

Preferred reporting path:

1. Use [GitHub private vulnerability reporting](https://github.com/AsiBackbone/AsiBackbone/security/advisories/new) for this repository.
2. Include a concise title, affected package or documentation area, affected version, reproduction steps, expected behavior, actual behavior, and any safe proof material.
3. Describe the practical impact in host-application terms: for example, policy bypass, acknowledgment bypass, capability-token misuse, unsafe sample guidance, signing or verification confusion, audit-residue integrity concern, data exposure, or denial-of-service risk.
4. Keep public disclosure deferred until the maintainer has had reasonable time to triage and respond.

If the private vulnerability reporting link is unavailable to you, open a minimal public issue that only states that you have a sensitive security report to share. Do not include technical details in the public issue.

For non-sensitive hardening suggestions, documentation wording concerns, or defense-in-depth improvements, a normal GitHub issue is appropriate.

## Expected response posture

This project is maintained as an open-source package family and does not promise a formal SLA.

The expected best-effort posture is:

1. A maintainer reviews the report and determines whether it is a vulnerability, documentation issue, sample issue, hardening opportunity, duplicate, or out-of-scope concern.
2. The maintainer may ask for clarification, version details, logs with sensitive data removed, or a reduced reproduction.
3. Confirmed concerns are handled through a fix, documentation correction, package update, advisory, or issue/PR trail as appropriate to the risk.
4. Public wording will avoid overstating the package's security guarantees, legal effect, compliance status, or production tamper-evidence posture.

Please avoid sending repeated public comments while a sensitive report is being reviewed.

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

AsiBackbone NuGet packages are intentionally published without Authenticode or repository signing.

This is a deliberate governance decision while the project is independently maintained, balancing operational complexity against practical value. At this stage, the project prioritizes transparent source code, deterministic build and release practices, GitHub releases, Source Link, SBOM generation, and package provenance over maintaining signing certificates and supporting release-signing infrastructure.

Consumers should validate package identity through the official NuGet package source, package version, repository metadata, Source Link information, GitHub release tags, and available integrity metadata provided by NuGet tooling.

Package signing remains under consideration for the long-term roadmap and will be reconsidered as the project's community, governance, and operational requirements evolve. Until package signing is formally adopted and documented, AsiBackbone should not be described as providing signed release artifacts, repository-signed packages, or Authenticode-signed packages.

### Current trust model

Current package trust is established through:

- Official NuGet package publication
- Public GitHub source repository
- GitHub release tags
- Source Link integration
- Software Bill of Materials (SBOM)
- Package provenance and repository metadata

These mechanisms provide transparency and traceability while package signing remains deferred.

## Sensitive data guidance for reports

When reporting a concern:

- redact secrets, tokens, private keys, connection strings, user identifiers, customer data, and regulated data;
- use synthetic examples when possible;
- share only the minimum details necessary to reproduce or understand the issue;
- clearly mark whether the report includes sensitive operational information.

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

## Safe public language

It is accurate to say that AsiBackbone provides governance infrastructure and host-owned integration seams for accountable decision flow.

It is not accurate to say that AsiBackbone is a complete security platform, compliance certification system, tamper-proof ledger, legal evidence system, or artificial superintelligence implementation.
