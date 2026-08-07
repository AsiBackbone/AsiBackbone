# Quality Reports

AsiBackbone uses quality reports to make the validation surface easier to inspect from the documentation site.

Coverage, mutation analysis, smoke testing, and concurrency validation answer different questions:

| Report | Question answered | Purpose |
| --- | --- | --- |
| Coverage Report | Did the tests execute this code? | Shows the tested surface area and highlights unvisited code paths. |
| Core Branch Coverage | Did Core tests exercise the decision branches that protect governance behavior? | Enforces a stricter branch gate for the framework-neutral Core package without applying that same threshold to every adapter or sample. |
| Adapter and Provider Package Coverage | Are consumer-facing adapter/provider packages visible independently from the repository-wide total? | Runs package-scoped coverage baselines for selected integration surfaces so adapter debt cannot hide behind total line coverage. |
| Mutation Analysis | Would tests fail if behavior changed? | Checks assertion strength by introducing small code mutations and verifying tests catch them. |
| External Consumer Smoke Test | Can a clean host consume package-shaped artifacts? | Validates package ergonomics, DI registration, host-owned EF persistence, in-memory audit storage, and HTTP allow/deny/acknowledgment flows. |
| EF Core Outbox Concurrency Validation | What happens under concurrent EF Core writes, retryable failures, and drain-worker contention? | Provides CI-friendly relational evidence for outbox/lifecycle persistence and documents the current non-claiming drain boundary. |

For a governance package, these views all matter. Coverage helps show that policy, acknowledgment, audit, capability-token, DLP/classification, provider-neutral emission, durable outbox, signing, verification, and canonical-hashing paths are exercised. The Core branch coverage gate protects the framework-neutral governance engine from missed policy, decision, signing, capability, acknowledgment, audit, emission, outbox, DLP/classification, and verification branches. Adapter/provider package coverage makes integration surfaces visible independently from the repository-wide total. Mutation analysis helps show that tests are strong enough to detect behavior changes in selected high-value decision logic and package boundaries. External consumer smoke testing helps prove that package-shaped adoption works from outside the repository's normal project-reference graph. EF Core outbox concurrency validation adds repeatable evidence for durable outbox behavior under concurrent local persistence and worker contention without claiming exactly-once delivery.

## Current quality posture

The `3.x` stable package family keeps the repository bounded as Accountable Systems Infrastructure for governed .NET decision flow. The current quality posture combines broad repository gates with targeted hardening for high-risk surfaces:

- repository-wide 75% line coverage for the full solution;
- Core-only 90% branch coverage for the framework-neutral governance engine;
- adapter/provider package coverage baselines for `AsiBackbone.AspNetCore`, `AsiBackbone.EntityFrameworkCore`, `AsiBackbone.OpenTelemetry`, `AsiBackbone.Signing.ManagedKey`, and `AsiBackbone.Analyzers`;
- staged public API XML documentation inventory with tracked `CS1591` ceilings and later project-by-project enforcement;
- targeted mutation reports for Core, ASP.NET Core acknowledgment handling, managed-key diagnostic provenance, and OpenTelemetry event-name contracts;
- external consumer and stable package smoke tests for package-shaped adoption.

Current Core coverage includes provider-neutral governance emission contracts, durable outbox contracts, DLP/classification failure policy primitives, signing-ready metadata, canonical hashing/signing seams, verification-policy primitives, and capability grant hardening. The targeted mutation reports remain quality signals for selected high-value behavior; they are not full-repository certification.

Tracked Core coverage-hardening work includes:

- [#246 — Core capability grant validation branch gaps](https://github.com/AsiBackbone/AsiBackbone/issues/246)
- [#247 — Core signing verifier and policy outcome branch gaps](https://github.com/AsiBackbone/AsiBackbone/issues/247)
- [#248 — Core canonical payload builder branch gaps](https://github.com/AsiBackbone/AsiBackbone/issues/248)
- [#249 — Core governance emission and outbox branch gaps](https://github.com/AsiBackbone/AsiBackbone/issues/249)
- [#250 — Core DLP classification policy branch gaps](https://github.com/AsiBackbone/AsiBackbone/issues/250)
- [#262 — Core-specific branch coverage quality gate](https://github.com/AsiBackbone/AsiBackbone/issues/262)
- [#563 — targeted mutation expansion across high-risk packages](https://github.com/AsiBackbone/AsiBackbone/issues/563)

## Available reports

### Coverage Reports

- [Open Coverage Report](../coverage/index.html)
- [Open Core Branch Coverage](../coverage/core/index.html)
- [Core Branch Coverage Quality Gate](core-branch-coverage.md)
- [Adapter and Provider Package Coverage Baselines](package-coverage-baselines.md)

The repository-wide coverage report is generated from the full test suite using Coverlet and ReportGenerator. It is published with the documentation site when the documentation workflow runs successfully.

The Core branch coverage report is generated separately from `AsiBackbone.Core.Tests`, filtered to `AsiBackbone.Core`, and enforced as a 90% branch coverage gate. The repository-wide 75% line coverage gate remains in place; the stricter Core gate does not apply to every adapter, storage provider, telemetry provider, or sample package.

The adapter/provider package coverage baseline gate runs selected package-scoped Coverlet targets from `eng/coverage/package-coverage-baselines.csv`. It starts with a low visibility floor so each adapter/provider report is independently produced, then records the next hardening step: calibrate thresholds from the first package-specific CI artifacts and raise them package by package.

### Mutation Analysis

- [Open Core Mutation Analysis](../mutation/core/index.html)
- [Open ASP.NET Core Mutation Analysis](../mutation/aspnetcore/index.html)
- [Open Managed-Key Signing Mutation Analysis](../mutation/signing-managedkey/index.html)
- [Open OpenTelemetry Mutation Analysis](../mutation/opentelemetry/index.html)
- [Mutation Coverage Scope and Deferrals](mutation-coverage-scope.md)
- [Core Test Triage](core-test-triage.md)

The mutation analysis reports are generated with Stryker.NET for targeted governance behavior. The Core report focuses on evaluator and policy-pipeline behavior, including denial precedence, decision outcome selection, reason-code preservation, and related edge cases. The ASP.NET Core report focuses on acknowledgment challenge round-trip behavior. The managed-key report protects the exact reserved-diagnostic boundary that prevents untrusted metadata from spoofing framework-owned signing diagnostics. The OpenTelemetry report protects stable governance event names and provider-failure mapping used by downstream telemetry consumers.

The mutation boundary and explicit deferrals are documented in [Mutation Coverage Scope and Deferrals](mutation-coverage-scope.md). Coverage-hardening work is tracked separately from mutation-scope expansion so readers can distinguish execution coverage improvements from assertion-strength testing.

### External Consumer Smoke Test

- [External Consumer Smoke Test](external-consumer-smoke-test.md)

The external consumer smoke test packs the repository projects into local NuGet packages, generates a temporary external xUnit project, installs the local packages without project references, and validates Core, ASP.NET Core adapter, EF Core ledger, in-memory audit storage, and HTTP decision-flow ergonomics.

### EF Core Outbox Concurrency Validation

- [EF Core Outbox Concurrency Validation](ef-core-outbox-concurrency-validation.md)

The EF Core outbox concurrency validation tests run inside `AsiBackbone.EntityFrameworkCore.Tests`. They use SQLite shared in-memory relational persistence to exercise concurrent outbox/lifecycle writes, retryable drain failures, and multi-worker drain contention. The validation intentionally preserves the current boundary: the provider-neutral drain is not an exactly-once delivery system.

> [!NOTE]
> If a report link is unavailable, the related workflow may not have generated that report yet. Generate the reports locally or rerun the **Publish Quality Reports** workflow after the report-producing steps are configured.

## Local validation

Restore tools before running report commands:

```bash
dotnet tool restore
```

Generate the standard coverage report through the normal test and ReportGenerator flow:

```bash
dotnet test ./AsiBackbone.slnx --configuration Release --collect:"XPlat Code Coverage" --results-directory ./artifacts/test-results

dotnet reportgenerator \
  -reports:"./artifacts/test-results/**/coverage.cobertura.xml" \
  -targetdir:"./artifacts/coverage-report" \
  -reporttypes:"Html;MarkdownSummaryGithub;Cobertura" \
  -assemblyfilters:"-*.Tests" \
  -filefilters:"-**/bin/**;-**/obj/**;-**/*.g.cs"
```

Run the Core branch coverage gate and generate the Core-only report:

```bash
dotnet test ./tests/AsiBackbone.Core.Tests/AsiBackbone.Core.Tests.csproj \
  --configuration Release \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput="./artifacts/core-coverage/" \
  /p:Include="[AsiBackbone.Core]*" \
  /p:Exclude="[*.Tests]*" \
  /p:Threshold=90 \
  /p:ThresholdType=branch \
  /p:ThresholdStat=total
```

Run the adapter/provider package coverage baseline gate after a Release build:

```powershell
./scripts/Validate-PackageCoverageBaselines.ps1 -Configuration Release -NoBuild -NoRestore
```

The package baseline report is written to:

```text
artifacts/coverage/package-baselines
```

Run the targeted mutation reports from their test-project folders:

```bash
cd ./tests/AsiBackbone.Core.Tests
dotnet tool run dotnet-stryker -- --config-file stryker-config.json

cd ../AsiBackbone.AspNetCore.Tests
dotnet tool run dotnet-stryker -- --config-file stryker-config.json

cd ../AsiBackbone.Signing.ManagedKey.Tests
dotnet tool run dotnet-stryker -- --config-file stryker-config.json

cd ../AsiBackbone.OpenTelemetry.Tests
dotnet tool run dotnet-stryker -- --config-file stryker-config.json
```

Run the external consumer smoke test from the repository root:

```bash
bash ./eng/smoke-tests/external-consumer-smoke.sh
```

Run the EF Core outbox concurrency validation from the repository root:

```bash
dotnet test ./tests/AsiBackbone.EntityFrameworkCore.Tests/AsiBackbone.EntityFrameworkCore.Tests.csproj --configuration Release --filter FullyQualifiedName~EfCoreOutboxConcurrencyValidationTests
```

## Interpreting mutation results

Mutation testing should be reviewed as a quality signal, not as a raw score chase.

Surviving mutants should be classified as:

- missing assertion or weak test behavior;
- equivalent or non-actionable mutant;
- acceptable gap to document for a later issue;
- intentional behavior that should be protected by a stronger test.

For Issue #134, see [Core Test Triage](core-test-triage.md) for the focused Core survivor-triage pass covering `Evaluation`, `Decisions`, `Audit`, and `Handshakes`.

## Interpreting external consumer smoke-test results

External consumer smoke-test failures should be treated as package-consumer ergonomics failures first. The generated test project is intentionally outside the solution's normal project-reference graph, so failures can expose packaging, dependency, registration, or host-boundary regressions that normal repository tests may not catch.

## Interpreting EF Core outbox concurrency validation results

EF Core outbox concurrency validation failures should be reviewed as reliability-boundary regressions or documentation-boundary mismatches. Passing results show repeatable evidence for the tested relational path; they do not prove universal production throughput, exactly-once delivery, or distributed locking across all database providers.
