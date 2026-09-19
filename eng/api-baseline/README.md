# Stable public API baselines

These files are the reviewable public managed-API baseline for the stable
AsiBackbone package family, including the intentional `6.0` removals on the
`release/6.0.0` branch and the coordinated semantic type renames in issue #782.

The initial baseline was generated from the DocFX managed-reference output for
the published `v5.0.0` source at commit
`54ddd57a4d272e9294ee5638aef723e6d5ae66a8`. Each baseline records the public
type/member UID together with the rendered C# declaration. Enum values are also
recorded with their displayed numeric value.

The committed files now describe the reviewed `6.0.0` public API after the
intentional major-version changes documented below.

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

Issue #783 removes exactly five obsolete partial evaluator constructors and two
obsolete `RequireGovernancePolicy` route-builder extensions at the 6.0 major
boundary. See `docs/articles/upgrade-500-to-600.md` for the complete inventory
and replacements. The Core and ASP.NET Core package compatibility suppression
files allow these previous-release member removals; package validation remains
enabled for all other compatibility changes.

Issue #782 reviews all 232 public type entries and renames 103 entries with 102
distinct simple names, preserving namespaces and generic arities. See
`docs/articles/public-api-naming-600.md` for every retain/rename decision and
`docs/articles/upgrade-500-to-600.md` for the complete migration table. Receipt
validation diagnostic wording is also updated. Exact compatibility entries
cover the resulting removed type identities, changed member signatures,
implemented interfaces, and generic constraints. Package IDs, JSON keys,
canonical tags, enum values, schema versions, and EF table/column names remain
unchanged; the package compatibility gate is still enabled.

Issue #794 completes the decision-receipt terminology pass for the signing and
contract-test helpers, including their named-argument parameter names and the
ASP.NET Core correlation extension class. These intentional 6.0 breaks and
their replacements are recorded in the upgrade guide.

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
