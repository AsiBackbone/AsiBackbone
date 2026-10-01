# ASIB901: Legacy CreateExecutionBoundary

`ASIB901` is reported when source code calls `CapabilityGrantValidationOptions.CreateExecutionBoundary(...)`.

The method remains in the `7.x` binary surface for compatibility, but it is not a usable execution-boundary factory. It always throws `InvalidOperationException`, because consequential execution requires an authoritative subject binding that the legacy signature cannot supply. `ASIB901` is a compiler warning so that code still compiles within the stable major line; projects that enable `TreatWarningsAsErrors` will fail to build until the call sites are migrated.

## Migrate

Replace `CreateExecutionBoundary(...)` with `CreateBoundExecutionBoundary(...)` and pass `CapabilityGrantBindingExpectations`, built from the authenticated subject, as the required first argument. Include the requested operation when the issuer restricted the grant to an operation.

```csharp
// Legacy: compiles with ASIB901 in 7.x, then fails closed if executed.
CapabilityGrantValidationOptions.CreateExecutionBoundary(
    issuer: "policy-engine",
    audience: "robotics-gateway",
    scopes: ["robotics.execute"]);

// Replacement: authoritative subject binding is required.
CapabilityGrantValidationOptions.CreateBoundExecutionBoundary(
    CapabilityGrantBindingExpectations.Create(
        authenticatedSubjectId,
        "robotics.execute"),
    issuer: "policy-engine",
    audience: "robotics-gateway",
    scopes: ["robotics.execute"]);
```

Code that intentionally performs only structural or temporal validation should use `CreateMetadataValidation(...)` so the reduced validation contract is visible in review.

For the full execution-boundary profile, see [Capability Grant Hardening](capability-grant-hardening.md#legacy-createexecutionboundary-deprecation).

## Removal target

`CreateExecutionBoundary(...)` is planned for removal in `8.0`, subject to the repository's [deprecation and major-release policy](api-compatibility-and-semver.md#deprecation-and-major-release-policy). Because the method already fails closed, removing it changes compilation only, not runtime behavior.
