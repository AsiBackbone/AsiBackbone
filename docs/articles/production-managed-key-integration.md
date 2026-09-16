# Production Managed-Key Integration Guide

Issue: #512.

This guide documents the production runtime signing path for AsiBackbone governance receipt when a host wants managed-key signing without making AsiBackbone responsible for key custody or cloud/provider-specific key-management behavior.

AsiBackbone is a policy decision pipeline for accountable decision flow. It is not a key-management platform, HSM appliance, cloud KMS wrapper, compliance certification service, immutable ledger, or legal non-repudiation product by itself.

> [!IMPORTANT]
> Production runtime signing remains **provider-neutral**. AsiBackbone documents how a host can connect Azure Key Vault, AWS KMS, GCP Cloud KMS, HSM, or enterprise key-management clients behind the existing managed-key boundary. AsiBackbone does not ship or maintain first-party production signing providers, production-style signing sample hosts, credentials, key storage, key rotation, legal non-repudiation guarantees, or provider-specific security guarantees.

## Decision

The approved production signing-provider story is **Option 1: provider-neutral only**.

AsiBackbone provides signing and verification abstractions, canonical hashing/signing seams, safe metadata conventions, the `AsiBackbone.Signing.ManagedKey` adapter boundary, and documentation for host responsibilities.

AsiBackbone does **not** provide first-party production provider packages for Azure Key Vault, AWS KMS, GCP Cloud KMS, HSM appliances, certificate stores, or enterprise key-management systems. It also does not ship a production-style sample host that could be mistaken for a supported provider implementation.

This keeps the package family focused on governance flow and avoids expanding the project into consumer key-management maintenance.

## Three different signing/provenance concerns

Do not collapse these concerns into one claim:

| Concern | What it means | AsiBackbone posture |
| --- | --- | --- |
| NuGet package signing | Whether published `.nupkg` files are signed release artifacts. | Deferred unless a reviewed package-signing process is adopted. See the consumer verification guide. |
| GitHub provenance and SBOM | Source Link, repository metadata, package SBOMs, and workflow provenance where available. | Useful supply-chain evidence, but not package signing and not runtime audit signing. |
| Runtime decision-receipt signing | A host signs decision receipts, audit ledger records, or outbox records through configured signing infrastructure. | Supported through provider-neutral abstractions and the managed-key adapter boundary; production key custody remains host-owned. |

A package can have Source Link and SBOM provenance without being maintainer-signed. A governance record can be signed at runtime without proving legal non-repudiation. A signed governance record still requires verification, durable storage controls, key-retention policy, monitoring, and incident response before a host should make stronger integrity claims.

## Runtime boundary

The runtime dependency direction stays narrow:

```text
AsiBackbone.Core
        ^
        |
AsiBackbone.Signing.ManagedKey
        ^
        |
Host-owned managed-key client
        ^
        |
Azure Key Vault, AWS KMS, GCP Cloud KMS, HSM, or enterprise KMS
```

`AsiBackbone.Signing.ManagedKey` owns the adapter service, provider-neutral options, DI registration helpers, safe metadata handling, retry diagnostics, and fail-closed defaults.

The consuming host owns the concrete client and operating environment.

## What AsiBackbone provides

AsiBackbone provides:

- `IGovernanceSigningService` and `IGovernanceSignatureVerificationService` abstractions in Core;
- canonical payload/hash contracts and signing-ready metadata fields;
- `IManagedKeySigningClient` as the host-owned client boundary;
- `ManagedKeySigningOptions` for provider descriptor, key ID, key version, hash/signature algorithm descriptors, retry settings, and failure behavior;
- fail-closed production-oriented registration through `AddAsiBackboneManagedKeySigning(...)`;
- local-validation registration for tests, samples, diagnostics, and intentionally unsigned failure metadata;
- safe metadata filtering so provider metadata cannot return secrets or overwrite service-owned diagnostic keys.

## What the host provides

The consuming host provides:

- the Azure Key Vault, AWS KMS, GCP Cloud KMS, HSM, certificate-store, or enterprise key-management client;
- authentication and authorization configuration, such as managed identity, workload identity, service account, certificate, or enterprise credential flow;
- key custody, key creation, key protection level, key version selection, and key rotation policy;
- provider-specific algorithm mapping;
- timeout, retry, circuit-breaker, and failover policy;
- monitoring and alerting for signing failures, latency, disabled keys, revoked keys, provider outages, and verification failures;
- a verification service or verification process when signed records must be trusted later;
- durable audit/outbox storage, retention, backup, immutability/append-only controls, and anchoring strategy when required;
- legal, compliance, and evidentiary interpretation for the deployed environment.

## Minimal host-owned adapter shape

A production host implements `IManagedKeySigningClient` around its selected provider. The client signs the precomputed AsiBackbone signing hash and returns provider-neutral metadata. It must not return private key material, tokens, secrets, or raw credential material.

```csharp
services.AddSingleton<IManagedKeySigningClient, HostOwnedManagedKeySigningClient>();

services.AddAsiBackboneManagedKeySigning(options =>
{
    options.ProviderName = "host-managed-key";
    options.KeyId = "provider-specific-key-reference";
    options.KeyVersion = "provider-specific-key-version";
    options.HashAlgorithm = "SHA-256";
    options.SignatureAlgorithm = "RSASSA-PSS-SHA256-MANAGED-KEY";
    options.RequireKeyVersion = true;
    options.ReturnUnsignedOnFailure = false;
    options.MaxRetryAttempts = 2;
});
```

The concrete `HostOwnedManagedKeySigningClient` might call Azure Key Vault, AWS KMS, GCP Cloud KMS, an HSM appliance, or an organization-owned signing service. That implementation is intentionally outside the AsiBackbone package boundary.

## Provider-specific checklist

Before connecting a concrete provider, document these host-owned choices:

- Which provider signs the artifact hash?
- Which key ID and key version are active?
- Which algorithm descriptor does AsiBackbone record, and which concrete provider algorithm does it map to?
- Does the provider sign a digest or require a raw payload?
- Which identity can sign?
- Which identity can verify or read the public key?
- How are signing and verification permissions separated?
- How are disabled, retired, missing, or revoked key versions detected?
- What happens when the provider times out or is unavailable?
- Are signing failures denied, deferred, escalated, dead-lettered, or allowed to continue unsigned under an explicit host policy?
- How are provider operation IDs recorded without leaking secrets?
- How are records verified after key rotation?
- Which dashboards or alerts indicate signing failure rates, latency, and verification failure rates?

## Failure posture

Production-oriented managed-key registration fails closed by default because `ReturnUnsignedOnFailure` defaults to `false`.

When signing is required for a governed operation, a host should usually treat provider unavailability, unsupported algorithms, key mismatch, missing required key version, or verification failure as a denial, deferral, escalation, or dead-letter condition according to host policy.

Unsigned failure metadata is useful for local validation, diagnostics, and explicitly policy-routed fallback. It is not a successful signature and must not be described as signed governance receipt.

## Verification path

Signing and verification are separate responsibilities.

A signed record should preserve:

- signing hash;
- hash algorithm;
- signature value or signature reference;
- signature algorithm descriptor;
- key ID;
- key version;
- provider descriptor;
- signed UTC timestamp;
- safe provider operation ID when available.

A host that needs to trust records later must provide a verification path that can resolve the recorded key reference, use the recorded algorithm family, account for retired-key verification, and distinguish invalid signatures from provider unavailability.

## Safe wording

Safe wording:

- "The governance artifact hash was signed through the host-configured managed-key client."
- "AsiBackbone preserved provider-neutral signature metadata returned by the host-owned signing boundary."
- "Private key material remains outside AsiBackbone."
- "Production signing infrastructure, key custody, rotation, monitoring, verification, and legal interpretation are host-owned."
- "The managed-key package is an adapter boundary, not a cloud KMS or HSM provider package."

Avoid wording such as:

- "AsiBackbone supports Azure Key Vault/AWS KMS/GCP Cloud KMS/HSM out of the box."
- "AsiBackbone provides production key management."
- "The managed-key adapter proves legal non-repudiation."
- "Signing alone makes records tamper-evident."
- "Unsigned failure metadata means the artifact was signed."
- "A sample host is a production signing provider."

## Non-goals

AsiBackbone does not provide:

- production key custody;
- cloud KMS SDK wrappers;
- first-party Azure Key Vault, AWS KMS, GCP Cloud KMS, HSM, certificate-store, or enterprise KMS signing providers;
- production-style signing sample hosts;
- credential management;
- automatic key creation or rotation;
- legal non-repudiation guarantees;
- provider-specific compliance guarantees;
- immutable storage, blockchain anchoring, or append-only database behavior by default;
- tamper-evident records by default;
- incident response for a host's key-management environment.

## Related articles

- [Signing Provider Package Boundary](signing-provider-package-boundary.md)
- [Managed-Key Signing Provider](managed-key-signing-provider.md)
- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Signed Audit and Outbox Records](signed-audit-and-outbox-records.md)
- [Verification Policy and Result Handling](verification-policy-and-result-handling.md)
- [Key Rotation and Retired-Key Verification](key-rotation-and-retired-key-verification.md)
- [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md)
- [3.0.0 Consumer Verification Guide](consumer-verification-300.md)
