# Stable public API baselines

These files are the reviewable public managed-API baseline for the stable
AsiBackbone `5.x` package family.

The initial baseline was generated from the DocFX managed-reference output for
the published `v5.0.0` source at commit
`54ddd57a4d272e9294ee5638aef723e6d5ae66a8`. Each baseline records the public
type/member UID together with the rendered C# declaration. Enum values are also
recorded with their displayed numeric value.

## Included stable managed assemblies

- `AsiBackbone.Analyzers.dll`
- `AsiBackbone.AspNetCore.dll`
- `AsiBackbone.Core.dll`
- `AsiBackbone.DependencyInjection.dll`
- `AsiBackbone.EntityFrameworkCore.dll`
- `AsiBackbone.OpenTelemetry.dll`
- `AsiBackbone.Signing.LocalDevelopment.dll`
- `AsiBackbone.Signing.ManagedKey.dll`
- `AsiBackbone.Storage.InMemory.dll`
- `AsiBackbone.Testing.dll`

`AsiBackbone.Templates` is intentionally excluded from this managed-API gate
because it is a content-only `dotnet new` package and does not expose a managed
consumer assembly. Its released contract remains protected by template package
smoke tests. Test projects, samples, benchmarks, generated DocFX artifacts, and
other non-package projects are also outside this stable package baseline.

## Validation

Build the DocFX managed-reference output and compare it to the committed
baseline:

```powershell
dotnet tool restore
dotnet tool run docfx -- docs/docfx.json
./scripts/Validate-PublicApiBaseline.ps1
```

Any public addition, removal, enum-value change, or signature/declaration change
in an included assembly fails validation until the change is explicitly
reviewed. Baseline row order is not semantically significant during validation;
`-Update` writes rows using ordinal ordering so generated files are deterministic
across Windows and Linux runners.

## Intentional API changes

A baseline update is an approval artifact, not an approval mechanism. First
classify the API change under the project's Semantic Versioning policy. Additive
stable API requires at least a minor release; a breaking stable API change
requires a major release and migration guidance.

After that review, regenerate the baseline:

```powershell
./scripts/Validate-PublicApiBaseline.ps1 -Update
git diff -- eng/api-baseline
```

Commit the baseline diff in the same pull request as the intentional API change
and record the SemVer impact in the pull request/release documentation. Do not
use `-Update` merely to make CI green without reviewing the API difference.
