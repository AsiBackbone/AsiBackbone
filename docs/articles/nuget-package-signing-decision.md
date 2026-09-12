# NuGet Package Signing Decision Record

| Field | Decision |
| --- | --- |
| Status | Accepted, with mandatory review |
| Decision date | 2026-09-12 |
| Decision owner | [@cdcavell](https://github.com/cdcavell), Core Maintainer |
| Scope | NuGet package signing for the public `AsiBackbone.*` package family |
| Current decision | Defer NuGet package signing |
| Next mandatory review | 2027-03-31 or before the first `6.0.0` release candidate, whichever occurs first |

## Context

AsiBackbone publishes governance and policy-control packages whose consumers may
need strong artifact identity and supply-chain evidence. NuGet repository
signing or author signing can add a certificate-backed verification signal, but
it also introduces certificate acquisition, protected key custody, rotation,
expiration, revocation, incident response, and release-continuity obligations.

The project is independently maintained. Adopting package signing without a
reviewed operational owner and recoverable key-management process could make
release availability depend on one certificate or one maintainer workstation.
That risk does not justify presenting an incomplete signing process as a strong
assurance boundary.

This decision concerns signing the distributed NuGet packages. It is separate
from the framework's signing-provider APIs and from signatures a consuming host
may apply to its own governance records.

## Decision

NuGet package signing remains deferred for the current `5.x` line. Unsigned
packages must not be described as maintainer-signed, repository-signed,
Authenticode-signed, tamper-proof, or legally non-repudiable.

The deferral is time-bounded. The maintainer must review it by 2027-03-31 or
before the first `6.0.0` release candidate, whichever comes first, even if none
of the event-driven criteria below has occurred.

## Accepted residual risk

Consumers do not receive a NuGet package signature rooted in a certificate
identity controlled by the project. A compromise of the publication identity,
release workflow, or package source therefore cannot be mitigated by requiring
an independent project package signature alone.

Consumers that require signed dependencies must treat the current packages as
not satisfying that policy and either apply their own approved trust process or
defer adoption.

## Compensating controls

Until signing is adopted, the release process maintains distinct trust signals:

- publication through the official NuGet package source;
- protected, reviewable release workflows and environment boundaries;
- immutable Git tags and public source history;
- Source Link repository URL and commit metadata;
- one SPDX SBOM per package plus package and SBOM SHA-256 mappings;
- durable SBOM and release-evidence assets on each GitHub release;
- GitHub build-provenance attestations for package and SBOM subjects; and
- consumer-retained package hashes and independent vulnerability review.

None of these controls is described as equivalent to NuGet package signing.

## Mandatory early re-evaluation criteria

Re-evaluate this decision before the scheduled date when any of the following
occurs:

1. A second active maintainer can share release and certificate-rotation duties.
2. A consumer, distribution channel, regulator, or organizational dependency
   policy requires signed NuGet packages.
3. The project enters a regulated production adoption where certificate-backed
   package identity is part of the accepted control set.
4. A suitable signing service becomes available with protected non-exportable
   keys, auditable access, rotation, revocation, and recovery procedures.
5. NuGet or GitHub changes its package-signing, trusted-publishing, or
   provenance capabilities in a way that materially changes operational cost or
   assurance.
6. A publication-identity, workflow, package-source, provenance, or release
   evidence incident exposes a weakness that package signing would materially
   mitigate.
7. The package family changes publisher identity, ownership, or release
   infrastructure.

## Requirements for adopting signing

A proposal to adopt signing must define and validate:

- author signing, repository signing, or both, including the assurance each is
  intended to provide;
- certificate issuer, identity, acquisition, renewal, and revocation;
- protected key custody and least-privilege release access;
- rotation, expiry monitoring, backup, recovery, and maintainer succession;
- stable-release versus preview and local-development signing behavior;
- verification instructions and failure policy for consumers;
- transition behavior for previously unsigned packages; and
- coordinated updates to `SECURITY.md`, release workflows, release validation,
  release notes, and consumer verification guidance.

## Review record

At each review, update the decision date, status, rationale, next review date,
and any triggered criteria. If deferral continues, record why the residual risk
and compensating controls remain acceptable rather than carrying the prior
decision forward implicitly.
