# AsiBackbone.Signing.LocalDevelopment

[![NuGet](https://img.shields.io/nuget/v/AsiBackbone.Signing.LocalDevelopment?label=NuGet)](https://www.nuget.org/packages/AsiBackbone.Signing.LocalDevelopment)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AsiBackbone.Signing.LocalDevelopment?label=downloads)](https://www.nuget.org/packages/AsiBackbone.Signing.LocalDevelopment)

`AsiBackbone.Signing.LocalDevelopment` provides a local-development RSA signing and verification provider for exercising AsiBackbone signing abstractions without Azure Key Vault, HSM, cloud KMS, certificate-store, or external infrastructure dependencies.

> **New to AsiBackbone?** Start with the concept, not this package: [Intent to Execution: An Accountability Pattern](https://asibackbone.github.io/AsiBackbone/articles/intent-to-execution-pattern.html) and the [documentation site](https://asibackbone.github.io/AsiBackbone/). This README covers one package in the family.

> **Important:**
> This package is intended for local development, samples, and tests. It is not a production managed-key provider and does not provide tamper-evidence, immutability, legal non-repudiation, compliance certification, or protected key custody by itself.

## Boundary

This package implements:

- `IGovernanceSigningService`
- `IGovernanceSignatureVerificationService`

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
builder.Services.AddSingleton<IGovernanceSigningService>(localSigningService);
builder.Services.AddSingleton<IGovernanceSignatureVerificationService>(localSigningService);
```

## Production guard

`UseLocalDevelopmentSigning(...)` fails registration when its effective environment is `Production`. The guard uses
`LocalDevelopmentSigningOptions.EnvironmentName` when supplied; otherwise it checks `DOTNET_ENVIRONMENT` and then
`ASPNETCORE_ENVIRONMENT`. When none of those values is present, registration remains available for non-hosted unit-test
and local scenarios.

Production hosts should use `AsiBackbone.Signing.ManagedKey` with a reviewed host-owned managed-key implementation. See
[Signing provider package boundary](../../docs/articles/signing-provider-package-boundary.md) for the supported boundary
and key-custody responsibilities.

`LocalDevelopmentSigningOptions.AllowInProduction = true` is an explicit escape hatch for exceptional test/demo needs.
It is **not suitable for normal production use** and does not turn this package into a production key-custody control.
Code that requires the override should be treated as an exceptional configuration and reviewed accordingly.

The companion analyzer also reports local-development signing wired directly in a production branch (`ASIB002`) or
without an environment guard (`ASIB003`). These diagnostics complement the runtime guard; they do not replace it.

## Configured key

By default the provider generates a new RSA key in each process, so artifacts it signs stop verifying after a restart and
cannot be verified by another process. Set `RsaKeyPem` to an unencrypted PEM-encoded RSA key to give every process
configured with it the same signing identity, for example so a capability grant token issued by one process verifies in
another:

```csharp
var localSigningOptions = LocalDevelopmentSigningOptions.Create(keyId: "dev-shared-key", keyVersion: "dev");
localSigningOptions.RsaKeyPem = builder.Configuration["Signing:LocalDevelopmentKeyPem"];
```

A private key signs and verifies. A public key only verifies. The key must be at least 2048 bits, and `KeySizeBits` is
ignored. Load the key from a development secret store rather than source control. This is still a local-development
provider: the key has no custody, rotation, or revocation story, and production registration remains guarded.

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
