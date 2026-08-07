# AsiBackbone.Signing.LocalDevelopment

`AsiBackbone.Signing.LocalDevelopment` provides a local-development RSA signing and verification provider for exercising AsiBackbone signing abstractions without Azure Key Vault, HSM, cloud KMS, certificate-store, or external infrastructure dependencies.

> **New to AsiBackbone?** Start with the concept, not this package: [Intent to Execution: An Accountability Pattern](https://asibackbone.github.io/AsiBackbone/articles/intent-to-execution-pattern.html) and the [documentation site](https://asibackbone.github.io/AsiBackbone/). This README covers one package in the family.

> **Important:**
> This package is intended for local development, samples, and tests. It is not a production managed-key provider and does not provide tamper-evidence, immutability, legal non-repudiation, compliance certification, or protected key custody by itself.

## Boundary

This package implements:

- `IAsiBackboneSigningService`
- `IAsiBackboneSignatureVerificationService`

It signs the `SigningRequest.SigningHash` value using an in-process RSA key generated for the service instance and returns provider-neutral `SigningMetadata`.

The default local-development signature descriptor is `RSASSA-PSS-SHA256-LOCAL-DEV`, and the in-process RSA signer uses RSA-PSS with SHA-256. Existing local-development fixtures that assumed the earlier PKCS#1 v1.5 descriptor should be regenerated or configured explicitly for their test-only compatibility path.

Core remains provider-neutral. `AsiBackbone.Core` does not reference this package.

## RSA key size

The generated local-development RSA key defaults to **2048 bits**. Explicit values must be at least `LocalDevelopmentSigningOptions.MinimumKeySizeBits` (currently 2048 bits).

Values below the minimum, including zero and negative values, fail configuration validation. They are not silently replaced with the default. This makes local configuration mistakes visible while preserving the secure default when no key size is supplied.

Supported larger values, such as 3072 or 4096 bits, are passed directly to the platform RSA implementation. Hosts should still use a managed-key or HSM-backed provider for production key custody.

## Metadata returned

Successful signing results include:

- signing hash
- hash algorithm
- Base64 signature value
- signature algorithm descriptor
- key ID
- key version
- provider descriptor
- signed UTC timestamp
- local-development warning metadata

Signing failures in normal flow return unsigned signing metadata with explicit `signing_status`, `failure_code`, and `failure_message` values unless the host opts out by setting `ReturnUnsignedOnFailure = false`.

## Example registration

```csharp
var localSigningOptions = LocalDevelopmentSigningOptions.Create(
    keyId: "sample-local-dev-key",
    keyVersion: "dev",
    keySizeBits: 3072);

var localSigningService = new LocalDevelopmentSigningService(localSigningOptions);

builder.Services.AddSingleton(localSigningService);
builder.Services.AddSingleton<IAsiBackboneSigningService>(localSigningService);
builder.Services.AddSingleton<IAsiBackboneSignatureVerificationService>(localSigningService);
```

The builder facade validates configuration during registration:

```csharp
builder.Services
    .AddAsiBackbone()
    .UseLocalDevelopmentSigning(localSigningOptions);
```

## Example flow

```text
AuditLedgerRecord
  -> CanonicalPayloadBuilder.ForAuditLedgerRecord(...)
  -> CanonicalPayloadHasher.ComputeHash(...)
  -> SigningRequest
  -> LocalDevelopmentSigningService.SignAsync(...)
  -> SignatureVerificationRequest
  -> LocalDevelopmentSigningService.VerifyAsync(...)
```

## Non-goals

This package does not:

- integrate with Azure Key Vault, Managed HSM, local machine certificate stores, or cloud KMS services;
- persist private key material;
- provide production key rotation;
- provide legal non-repudiation;
- verify an audit hash chain;
- provide immutable storage or external anchoring;
- make unsigned, signed, or verified records tamper-evident by default.

Use a managed-key or HSM-backed provider for production workflows where signing is part of a security or audit-control boundary.
