# Managed-Key Signing Provider

This article documents the released `AsiBackbone.Signing.ManagedKey` provider package.

AsiBackbone provides provider-neutral signing, verification, and audit metadata seams. It does not provide immutable storage, external anchoring, blockchain, legal evidence guarantees, compliance certification, production key management, or tamper-evidence by itself.

> [!IMPORTANT]
> The managed-key package is a stable provider adapter and client boundary. It does not include a live Azure Key Vault, Managed HSM, AWS KMS, GCP Cloud KMS, certificate store, cloud KMS, or HSM implementation by default. Host applications supply the actual managed-key client, credentials, verification path, monitoring, and operational policy.
>
> Production runtime signing remains provider-neutral. AsiBackbone does not ship or maintain first-party production signing providers or production-style signing sample hosts. See the [Production Managed-Key Integration Guide](production-managed-key-integration.md).

## Purpose

`AsiBackbone.Signing.ManagedKey` lets a host application wire AsiBackbone signing to a managed-key system without forcing `AsiBackbone.Core` to reference cloud SDKs or handle private key material.

The dependency direction remains:

```text
AsiBackbone.Core
        ^
        |
AsiBackbone.Signing.ManagedKey
        ^
        |
Host application managed-key client
```

Core remains provider-neutral. Provider packages reference Core, not the reverse.

## Released boundary

| Boundary | Status | Notes |
| --- | --- | --- |
| Managed-key adapter package | Released stable package. | Provides options, DI registration, signing service, and client abstraction. |
| `IManagedKeySigningClient` | Host-owned implementation boundary. | The host implementation may call Azure Key Vault, Managed HSM, AWS KMS, GCP Cloud KMS, an HSM appliance, or an organization-owned signing service. |
| Concrete cloud/HSM/KMS client | Not shipped by default and not planned as a first-party production signing provider. | Host-owned implementation behind the managed-key boundary. |
| Verification service | Separate host/provider responsibility. | Signed metadata is preserved for later verification, but hosts must provide a matching verification path when trust is required. |
| Production tamper-evidence | Not provided by default. | Requires signing, verification, protected key management, durable storage controls, retention, monitoring, and operational procedures. |

## Key boundary

The package defines `IManagedKeySigningClient`:

```csharp
public interface IManagedKeySigningClient
{
    ValueTask<ManagedKeySignResult> SignAsync(
        ManagedKeySignRequest request,
        CancellationToken cancellationToken = default);
}
```

A host-owned implementation can call Azure Key Vault, Managed HSM, AWS KMS, GCP Cloud KMS, a cloud KMS, an HSM appliance, or an organization-owned signing service. The implementation should sign a precomputed AsiBackbone artifact hash and return provider-neutral metadata.

The client must not return:

- private keys;
- symmetric keys;
- client secrets;
- access tokens;
- managed identity tokens;
- connection strings;
- raw credential material.

## Signature algorithm descriptor

The default managed-key signature descriptor is `RSASSA-PSS-SHA256-MANAGED-KEY`.

This value is provider-neutral metadata. AsiBackbone passes it to the host-owned `IManagedKeySigningClient`, and the host-owned client maps it to the concrete algorithm identifier required by Azure Key Vault, Managed HSM, AWS KMS, GCP Cloud KMS, a cloud KMS, an HSM appliance, or an organization-owned signing service.

Migration note: earlier examples used `RSASSA-PKCS1-v1_5-SHA256-MANAGED-KEY`. Hosts that still require PKCS#1 v1.5 for an existing managed-key backend can override `ManagedKeySigningOptions.SignatureAlgorithm` explicitly, but new integrations should prefer RSA-PSS when the key provider supports it. Verification services must use the same algorithm family recorded in the signing metadata.

## Dependency injection

Use `AddAsiBackboneManagedKeySigning(...)` for production-oriented managed-key signing. This registration fails closed by default because `ManagedKeySigningOptions.ReturnUnsignedOnFailure` defaults to `false`.

```csharp
services.AddAsiBackboneManagedKeySigning(
    options =>
    {
        options.ProviderName = "azure-key-vault";
        options.KeyId = "https://vault-name.vault.azure.net/keys/audit-signing-key";
        options.KeyVersion = "00000000000000000000000000000000";
        options.SignatureAlgorithm = "RSASSA-PSS-SHA256-MANAGED-KEY";
        options.HashAlgorithm = "SHA-256";
        options.RequireKeyVersion = true;
        options.ReturnUnsignedOnFailure = false;
        options.MaxRetryAttempts = 2;
    },
    serviceProvider => new HostOwnedManagedKeySigningClient());
```

The `ProviderName`, `KeyId`, `KeyVersion`, hash algorithm, and signature algorithm values are provider-neutral descriptors recorded by AsiBackbone. The host-owned client maps them to concrete provider calls outside the AsiBackbone package boundary.

The package also supports using an already-registered `IManagedKeySigningClient`:

```csharp
services.AddSingleton<IManagedKeySigningClient, HostOwnedManagedKeySigningClient>();

services.AddAsiBackboneManagedKeySigning(options =>
{
    options.ProviderName = "managed-hsm";
    options.KeyId = "audit-signing-key";
    options.KeyVersion = "2026-06";
});
```

## Local validation mode

Use `AddAsiBackboneManagedKeySigningForLocalValidation(...)` only when samples, tests, diagnostics, or explicit host policy need unsigned failure metadata instead of an exception.

```csharp
services.AddAsiBackboneManagedKeySigningForLocalValidation(
    options =>
    {
        options.ProviderName = "managed-key-local-validation";
        options.KeyId = "local-validation-key";
        options.KeyVersion = "local-v1";
    },
    serviceProvider => new HostOwnedManagedKeySigningClient());
```

This helper explicitly sets `ReturnUnsignedOnFailure = true`. The returned result is not a successful signature; it is unsigned metadata describing why signing failed.

## Signing metadata

Successful signing returns `SigningMetadata` with:

| Field | Source |
| --- | --- |
| Signing hash | Original `SigningRequest.SigningHash`. The client signs `SigningRequest.SignatureInput`, which binds this hash, the canonical descriptors, and the signing policy context. |
| Hash algorithm | Request or configured hash algorithm. |
| Signature | Managed-key client result. |
| Signature algorithm | Managed-key client result. |
| Key ID | Managed-key client result. |
| Key version | Managed-key client result or resolved request/options value. |
| Provider | Configured provider name. |
| Signed UTC | Managed-key client result. |
| Provider operation ID | Safe managed-key client result metadata. |

The provider adds safe metadata such as `provider_kind = managed-key`, `remote_key_material = true`, `raw_private_key_loaded = false`, `signing_status`, and `retry_attempts`.

## Failure handling

The production-oriented default is fail closed.

When `ReturnUnsignedOnFailure` is `false`, managed-key signing failures throw, including validation failures such as unsupported hash algorithm, key mismatch, or missing required key version. A high-assurance host can catch these exceptions at the governance boundary and deny, defer, dead-letter, or escalate according to host policy.

When `ReturnUnsignedOnFailure` is `true`, the provider returns unsigned signing metadata with failure details:

```text
signing_status = failed
failure_code = managedkey.signing.provider-unavailable
failure_message = TimeoutException
```

Unsigned failure metadata is useful for local validation and policy-routed fallback, but it must not be treated as a signed governance artifact.

Supported failure codes include:

| Failure | Code |
| --- | --- |
| Unsupported hash algorithm | `managedkey.signing.hash-algorithm-unsupported` |
| Requested key ID mismatch | `managedkey.signing.key-mismatch` |
| Missing key version when required | `managedkey.signing.key-version-missing` |
| Requested key version mismatch | `managedkey.signing.key-version-mismatch` |
| Provider unavailable | `managedkey.signing.provider-unavailable` |
| Generic managed-key signing failure | Provider-supplied or `managedkey.signing.failed` |

## Retry behavior

`ManagedKeySigningService` retries only when the host-owned client throws `ManagedKeySigningException` with `IsRetryable = true` and retry attempts remain.

Timeouts and non-retryable provider errors are surfaced as failure metadata or thrown, depending on `ReturnUnsignedOnFailure`.

## Verification boundary

This package implements signing through a managed-key adapter boundary. Verification remains a separate host or provider responsibility.

Downstream verifiers can use the preserved metadata:

- provider name;
- key ID;
- key version;
- signature algorithm;
- signing hash;
- hash algorithm;
- signed timestamp.

Hosts should provide a matching verification service when signed records must be trusted later. A signed artifact should not be treated as verified merely because the managed-key adapter returned signature metadata.

A verifier that recomputes the signing hash from an artifact, including one written outside .NET, must follow [Canonical JSON v1 Format](canonical-json-v1.md) exactly. The format is AsiBackbone-defined, is not RFC 8785 JCS, and normalizes metadata before hashing.

## Operational prerequisites

Before production use, hosts should document:

- how the managed-key client authenticates to the key system;
- which identity has sign permission;
- which identity has verify/read-public-key permission;
- whether signing and verification permissions are separated;
- which key ID and key version are active;
- how disabled, retired, revoked, or missing key versions are surfaced;
- timeout and retry policy;
- monitoring and alerting for signing failure rates;
- fallback behavior when signing is unavailable.

## Safe wording

Safe wording:

- "The artifact hash was signed through the configured managed-key client."
- "The provider returned key ID, key version, signature algorithm, signature value, and signed timestamp metadata."
- "Private key material remains outside AsiBackbone Core."
- "Signing failures fail closed by default in the production-oriented registration."
- "Unsigned failure metadata is diagnostic or policy-routable evidence, not a successful signature."
- "The managed-key package is a released adapter boundary; the concrete key client is host-owned."
- "Production key custody, credentials, rotation, monitoring, verification, and provider-specific guarantees are host-owned."

Avoid wording such as:

- "This package provides Azure Key Vault support by default."
- "This package provides AWS KMS, GCP Cloud KMS, or HSM support by default."
- "AsiBackbone ships a production signing provider."
- "Managed-key signing makes records tamper-proof."
- "Managed-key signing proves legal non-repudiation."
- "Unsigned failure metadata means the artifact was signed."
- "Signed means verified."

## Related articles

- [Canonical JSON v1 Format](canonical-json-v1.md)
- [Production Managed-Key Integration Guide](production-managed-key-integration.md)
- [Signing Provider Package Boundary](signing-provider-package-boundary.md)
- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Verification Policy and Result Handling](verification-policy-and-result-handling.md)
- [Key Rotation and Retired-Key Verification](key-rotation-and-retired-key-verification.md)
