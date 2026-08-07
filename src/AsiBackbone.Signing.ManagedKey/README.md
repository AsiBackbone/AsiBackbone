# AsiBackbone.Signing.ManagedKey

Provider-neutral managed-key signing adapter for AsiBackbone governance artifacts.

This package keeps `AsiBackbone.Core` provider-neutral while allowing host applications to connect signing flow to a host-owned managed key system, HSM, cloud KMS, Azure Key Vault / Managed HSM adapter, or organization-owned signing service through the existing adapter boundary.

> **New to AsiBackbone?** Start with the concept, not this package: [Intent to Execution: An Accountability Pattern](https://asibackbone.github.io/AsiBackbone/articles/intent-to-execution-pattern.html) and the [documentation site](https://cdcavell.github.io/AsiBackbone/). This README covers one package in the family.

> **Important:**
> This package does not include a live Azure Key Vault, AWS KMS, GCP Cloud KMS, HSM, certificate store, or blockchain implementation by default. Host applications provide the concrete client, credentials, verification path, monitoring, and operational policy; private keys, tokens, secrets, and raw key material must not be returned to AsiBackbone.
>
> For production guidance, see the [Production Managed-Key Integration Guide](https://asibackbone.github.io/AsiBackbone/articles/production-managed-key-integration.html). AsiBackbone remains provider-neutral and does not ship first-party production signing providers or production-style signing sample hosts.

## Boundary

```csharp
public interface IManagedKeySigningClient
{
    ValueTask<ManagedKeySignResult> SignAsync(
        ManagedKeySignRequest request,
        CancellationToken cancellationToken = default);
}
```

Host applications provide the client implementation and credentials. Private keys, symmetric keys, access tokens, client secrets, connection strings, managed identity tokens, and raw key material must not be returned to AsiBackbone.

The default managed-key signature descriptor is `RSASSA-PSS-SHA256-MANAGED-KEY`. It is provider-neutral metadata that the host-owned managed-key client maps to the concrete algorithm name used by its KMS, HSM, or signing service. Hosts whose key service still requires PKCS#1 v1.5 should override `SignatureAlgorithm` explicitly and document that compatibility choice.

## Dependency injection

The production-oriented registration fails closed by default. Signing failures throw unless the host explicitly opts into unsigned failure metadata.

```csharp
services.AddAsiBackboneManagedKeySigning(
    options =>
    {
        options.ProviderName = "azure-key-vault";
        options.KeyId = "https://vault-name.vault.azure.net/keys/audit-signing-key";
        options.KeyVersion = "00000000000000000000000000000000";
        options.SignatureAlgorithm = "RSASSA-PSS-SHA256-MANAGED-KEY";
        options.RequireKeyVersion = true;
        options.ReturnUnsignedOnFailure = false;
        options.MaxRetryAttempts = 2;
        options.RetryDelay = TimeSpan.FromMilliseconds(200);
        options.MaxRetryDelay = TimeSpan.FromSeconds(5);
    },
    serviceProvider => new HostOwnedManagedKeySigningClient());
```

The `ProviderName`, `KeyId`, and `KeyVersion` values are provider-neutral descriptors recorded by AsiBackbone. The host-owned `IManagedKeySigningClient` maps them to Azure Key Vault, AWS KMS, GCP Cloud KMS, HSM, certificate-store, or enterprise key-management calls outside the AsiBackbone package boundary.

For samples, tests, diagnostics, or explicit policy-routed fallback behavior, use the local-validation registration:

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

The registration wires `ManagedKeySigningService` as `IAsiBackboneSigningService`. Verification remains a separate provider or host responsibility.

## Failure behavior

When `ReturnUnsignedOnFailure` is `false`, provider, validation, and configuration-related signing failures throw so high-assurance hosts can fail closed at the governance boundary.

When `ReturnUnsignedOnFailure` is `true`, signing failures return unsigned `SigningMetadata` with safe failure metadata:

- `signing_status = failed`
- `failure_code`
- `failure_message`
- `failure_exception_type` when a provider/client exception was observed
- `failure_retryable` when a provider/client exception was observed
- `provider_kind = managed-key`
- `raw_private_key_loaded = false`
- `retry_attempts`
- `provider_attempts`
- `max_retry_attempts`
- `retry_delay_milliseconds`
- `max_retry_delay_milliseconds`
- `last_retry_delay_milliseconds`
- `total_retry_delay_milliseconds`
- `retry_delay_count`
- `retry_backoff_strategy`
- `retry_delay_configured`
- `retry_delay_applied`

Unsigned failure metadata is not a successful signature and must not be described as a signed governance artifact.

## Retry diagnostics

`ManagedKeySigningService` retries only when the host-owned client throws `ManagedKeySigningException` with `IsRetryable = true` and retry attempts remain. Non-retryable failures are returned or thrown immediately according to `ReturnUnsignedOnFailure`.

`RetryDelay` is the base delay. Before each retry, the service calculates an exponentially growing delay window and applies jitter inside the upper half of that window. The delay never exceeds `MaxRetryDelay`, and a later retry is never scheduled earlier than the previously applied retry delay. This avoids identical workers repeatedly retrying on the same fixed schedule while keeping backoff bounded and operationally predictable.

The defaults remain two retries, a 200 ms base delay, and a 5 second maximum delay. Existing configurations that set only `RetryDelay` continue to use that value as the base. Set `RetryDelay = TimeSpan.Zero` to retain immediate retries with no waiting or jitter. `MaxRetryDelay` must be greater than or equal to `RetryDelay`.

Delay waits honor the signing cancellation token. Cancellation interrupts a pending backoff and prevents the next provider call.

Signed and unsigned results include the configured base and maximum delay, the last and cumulative applied delays, delay count, and `retry_backoff_strategy = exponential-jitter`. Request metadata and provider-supplied metadata are not allowed to overwrite these service-owned diagnostic keys or other fields such as `signing_status`, `retry_attempts`, `provider_attempts`, `provider_operation_id`, or `failure_code`.

Provider-supplied retry timing is not accepted from the general provider metadata dictionary. A future explicit provider-neutral retry-after contract would require separate validation and bounds rather than trusting arbitrary metadata.

## Safe metadata

Provider metadata is an untrusted external input and is minimized before it can reach signing metadata, logs, governance residue, or audit records. The managed-key result boundary retains only these provider-neutral diagnostic keys:

- `provider_region`
- `provider_zone`
- `provider_service`
- `provider_request_id`
- `provider_status_code`
- `provider_key_state`

Matching is ordinal and case-insensitive, and retained keys are emitted in the canonical lowercase form shown above. Prefix, suffix, separator, and near-match variants are rejected rather than normalized into an allowed key.

Provider metadata is also bounded to 16 entries, 64 characters per key, 256 characters per value, and 2,048 aggregate key/value characters. Values containing control characters are rejected. The source dictionary is copied so later host or provider mutation cannot alter retained signing metadata.

Sensitive or unrecognized fields—including passwords, API keys, authorization headers, bearer tokens, cookies, certificates, connection material, credentials, private keys, and secret-like variants—are dropped by default. Provider operation IDs remain a separate explicit field on `ManagedKeySignResult` and are recorded only through the service-owned `provider_operation_id` diagnostic.

Signed results otherwise preserve provider-neutral signing information:

- signing hash;
- hash algorithm;
- signature value or signature reference;
- signature algorithm;
- key ID;
- key version;
- provider descriptor;
- signed UTC timestamp;
- safe provider operation ID when supplied;
- retry diagnostics such as `retry_attempts`, `provider_attempts`, and retry delay fields.

## Non-goals

This package does not provide production key custody, cloud KMS SDK wrappers, first-party Azure Key Vault/AWS KMS/GCP Cloud KMS/HSM/certificate-store/enterprise KMS providers, production-style signing sample hosts, automatic key rotation, credential management, tamper-evidence, immutable storage, append-only database behavior, external anchoring, legal non-repudiation, incident response, or compliance certification by itself. Those guarantees require concrete host-managed signing infrastructure, durable storage controls, verification, audit-chain or anchoring strategy, key-retention policy, monitoring, and operational procedures.
