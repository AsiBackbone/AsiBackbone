# Developer Checklist

Use this checklist before opening a pull request against the stable AsiBackbone package family.

## Local validation

Use the release-hardening path when you need evidence that the repository source and test surface has been built and tested:

```bash
dotnet restore AsiBackbone.slnx --locked-mode -p:Configuration=Release
dotnet build AsiBackbone.slnx --configuration Release --no-restore
dotnet test AsiBackbone.slnx --configuration Release --no-build --no-restore
```

A default local Debug solution build is also expected to build all first-party package and test projects:

```bash
dotnet restore AsiBackbone.slnx --locked-mode -p:Configuration=Debug
dotnet build AsiBackbone.slnx -p:Configuration=Debug --no-restore
```

## Solution build configurations

`AsiBackbone.slnx` includes all first-party package projects under `src/` and all first-party test projects under `tests/` in Debug solution builds. This keeps the common default `dotnet build AsiBackbone.slnx` command from appearing healthy while skipping package or test project compile coverage.

`Release` remains the canonical release-hardening validation configuration because CI, stable release validation, package creation, documentation, and smoke-test gates run against the release posture. Use the Release commands above before tagging, publishing, or treating local results as release evidence.

Only non-package/non-test convenience surfaces are intentionally excluded from `Debug|*` solution builds:

| Excluded project | Why it is excluded from `Debug|*` solution builds | Full validation path |
| --- | --- | --- |
| `benchmarks/AsiBackbone.Benchmarks/AsiBackbone.Benchmarks.csproj` | Benchmark harness; benchmarks are not normal Debug solution validation. | Build/run the benchmark project intentionally, normally in Release. |
| `benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet/AsiBackbone.Benchmarks.BenchmarkDotNet.csproj` | BenchmarkDotNet project; benchmark execution should be an explicit performance activity, not a default Debug solution build. | Build/run the BenchmarkDotNet project intentionally, normally in Release. |
| `samples/PlainAspNetCoreHost/AsiBackbone.Samples.PlainAspNetCoreHost.csproj` | Optional sample host; samples demonstrate adoption paths and should not be confused with package/test validation from a Debug solution build. | Release solution build and sample/smoke validation when sample behavior changes. |

The Debug coverage posture is guarded by:

```powershell
./scripts/Validate-DebugSolutionBuildCoverage.ps1
```

That script fails if new `Debug|*` solution exclusions appear outside the reviewed allowlist above. Update the script and this table together if a future exclusion is intentional.

When package-consumer behavior changes, run the smoke checks when possible:

```bash
bash ./eng/smoke-tests/external-consumer-smoke.sh
bash ./eng/smoke-tests/stable-package-integration-smoke.sh
```

For documentation changes, preview the DocFX site locally:

```bash
dotnet tool restore
dotnet tool run docfx docs/docfx.json --serve
```

## Package boundary checklist

- Core remains framework-neutral.
- ASP.NET Core integration remains a thin host adapter.
- EF Core integration remains host-owned.
- In-memory storage remains non-durable and local-validation focused.
- Samples and smoke tests do not become hidden requirements for consumers.
- Provider-specific behavior is not implied to be part of `1.0.0` unless explicitly released as stable.

## Language and namespace posture checklist

- Repository-wide implicit usings remain enabled; see [Implicit Usings Posture](implicit-usings-posture.md).
- Use normal file-level `using` directives for provider-specific, package-specific, persistence, telemetry, signing, ASP.NET Core, EF Core, and benchmark namespaces when they improve boundary review.
- Do not add `GlobalUsings.cs` files only to mirror SDK-provided implicit usings.
- Add project-local global usings only when they make repeated local test, sample, or host-scaffolding namespaces easier to review without hiding package dependencies.
- Avoid churn-only using-directive changes unless they improve package-boundary clarity.

## Production hygiene checklist

- Production source under `src/` must not use `NotImplementedException` as a placeholder.
- Use explicit production behavior instead: fail-closed governance result, domain-specific exception, `NotSupportedException`, or `InvalidOperationException` as appropriate.
- Keep tests, samples, and template scaffolding clearly separated from production library execution paths.
- Update [Production Placeholder Exception Guardrails](production-placeholder-exception-guardrails.md) when an intentional allowance is added.

## Public API checklist

- Is this API part of a stable package?
- Is the change additive, or does it remove or rename public behavior?
- Does it affect XML documentation, README content, or DocFX articles?
- Does it affect package-consumer smoke tests?
- Does it require a release-note entry?
- Does it require an API compatibility note?
- Does it affect extension points used by host applications?

## Documentation checklist

Documentation should be updated when a change affects package installation, public API usage, host responsibilities, package boundaries, schema shape, release notes, sample behavior, or smoke-test expectations.

## Pull request checklist

- Create a focused branch.
- Keep the PR scoped to the issue.
- Include a concise summary.
- List tests run or state that tests were not run.
- Link or close the related issue.
- Include documentation updates when public behavior changes.
- Avoid unrelated formatting churn.

## Release-readiness checklist

- Release notes are current.
- Package versions are aligned.
- Public docs and README links resolve.
- Known limitations are documented.
- Stable API and schema boundaries are reviewed.
- Local release-hardening commands are documented and point to the canonical Release restore/build/test path.
- Debug solution build coverage keeps all first-party package and test projects enabled, with only reviewed non-package/non-test exclusions.
- Package-consumer smoke tests pass or failures are explained.
- Future provider work is not presented as already stable.
