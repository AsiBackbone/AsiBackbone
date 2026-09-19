# Policy Evaluator Allocation Review

This note documents the allocation-review outcome for `DefaultGovernancePolicyEvaluator<TContext>` hot paths.

## Review scope

The review focused on three allocation-sensitive areas:

- constraint-result collection when an `IGovernanceDecisionPolicy<TContext>` is present;
- read-only constraint-result views passed to decision policies;
- warning-plus-denial reason composition when fast-abort mode preserves warnings already evaluated before the first denial.

## Current behavior to preserve

The evaluator must preserve decision-policy visibility. When a decision policy is registered, the policy receives the evaluated constraint results that led to the composed decision. This matters even when `ShortCircuitOnFirstDenial = true`: the policy should see only the constraints actually evaluated before the short-circuit point, not skipped constraints.

The constraint-result view must remain read-only from the policy consumer's perspective. A host policy should not be able to mutate the evaluator's internal result collection and thereby alter decision composition after evaluation.

Warning and denial reason ordering must also remain stable. In fast-abort mode, warnings produced before the first denial remain part of the denied decision because they are part of the evaluated path. Later constraints are intentionally skipped and cannot add reasons.

## Benchmark coverage

`PolicyEvaluatorAllocationBenchmarks` adds focused BenchmarkDotNet scenarios for:

- `policy_evaluator.all_allow_with_decision_policy_8`;
- `policy_evaluator.warning_and_denial_full_with_decision_policy`;
- `policy_evaluator.first_denial_with_decision_policy`;
- `policy_evaluator.first_denial_reason_composition`.

These scenarios complement the broader `AsiBackboneHotPathBenchmarks` suite by isolating decision-policy visibility and reason-composition paths.

`FirstDenialShortCircuitBenchmarks` adds follow-up scenarios for:

- `policy_evaluator.first_denial_expensive_tail_full`;
- `policy_evaluator.first_denial_expensive_tail_short_circuit`.

Those scenarios place CPU-bound tail constraints after the first denial so the benchmark measures the condition where short-circuiting is expected to help: skipping meaningful work. Static-constraint baselines can still show the short-circuit path as neutral, slower, or more allocating because the skipped constraints are trivial and the evaluator still has to compose a denied governance decision.

Run the allocation-focused review benchmarks from the repository root with:

```bash
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet --filter '*PolicyEvaluatorAllocationBenchmarks*'
```

Run the first-denial expensive-tail follow-up benchmarks with:

```bash
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet --filter '*FirstDenialShortCircuitBenchmarks*'
```

## Code-change posture

This review treats allocation work as incremental performance analysis rather than a correctness defect. The current posture avoids changing public API and pins sensitive behavior with regression tests before any deeper evaluator refactor.

The current implementation already avoids constraint-result list allocation when no decision policy is registered. When a decision policy is registered, at least one evaluated result normally has to be retained for policy visibility, so a broad "remove the list" change would risk hiding information from the policy layer.

Future optimization work should compare benchmark results before and after any implementation change and should only proceed if it preserves:

- decision-policy semantics;
- evaluated constraint-result visibility;
- read-only result surfaces for policy consumers;
- warning/denial reason ordering;
- no public API changes unless separately justified.

## Regression coverage

`DefaultAsiBackbonePolicyEvaluatorAllocationReviewTests` pins the two behavior points most likely to be affected by allocation-focused refactors:

- decision policies receive only evaluated constraint results when first-denial short-circuiting occurs;
- decision-policy constraint results remain read-only for policy consumers.