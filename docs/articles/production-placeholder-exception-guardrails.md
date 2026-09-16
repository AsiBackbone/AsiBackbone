# Production Placeholder Exception Guardrails

This article documents the repository rule that production library paths must not contain accidental placeholder exceptions.

## Rule

Production source under `src/` should not use `NotImplementedException` as a placeholder for incomplete behavior.

A production path should do one of the following instead:

- return a typed fail-closed governance result;
- throw a domain-specific exception when the failure is truly exceptional;
- throw `NotSupportedException` when the operation is explicitly unsupported;
- throw `InvalidOperationException` when the current state is invalid;
- document and test a deliberately unreachable branch.

`NotImplementedException` is treated as a development placeholder, not as a production behavior contract.

## Why this matters

AsiBackbone is a policy decision pipeline for accountable software decision flow. Consumers should not discover incomplete paths only after a production request enters a policy evaluator, storage provider, signing path, emitter, or host integration boundary.

The package should make invalid states explicit. Placeholder exceptions blur that line because they can hide an unfinished branch behind a successful build.

## Regression guard

The Core test suite includes a repository-level guard:

```text
tests/AsiBackbone.Core.Tests/RepositoryHygiene/ProductionPlaceholderExceptionGuardrailTests.cs
```

The test scans production C# source files under `src/` and fails if a `NotImplementedException` placeholder is introduced.

The guard intentionally excludes:

- test projects;
- generated build output under `bin/` and `obj/`;
- template content under `src/AsiBackbone.Templates/templates/`, because templates are sample scaffolding rather than compiled production library paths.

Templates, samples, and tests may use placeholders only when the placeholder is intentional and does not ship as a production library execution path.

## Contributor guidance

Before opening a pull request:

1. Search production source for `NotImplementedException`.
2. Replace placeholder behavior with explicit production behavior.
3. Add a test that proves the branch is either reachable and handled, or unreachable by design.
4. Document any intentional exception boundary if the behavior affects package consumers.

A safe replacement depends on context:

| Context | Prefer |
| --- | --- |
| Unsupported optional capability | `NotSupportedException` with a clear message. |
| Invalid state or invalid operation ordering | `InvalidOperationException` with safe diagnostics. |
| Host policy denial | A denied `GovernanceDecision` or fail-closed result. |
| Provider failure | Provider-neutral error/result models where available. |
| Internal invariant violation | A precise exception type plus a regression test. |

## Acceptable exceptions

An exception allowance should be rare and explicit. Generated code, non-production test scaffolding, or sample-only template code may be acceptable when clearly outside production library execution paths.

If a future production source path truly needs an allowance, do not bypass the guard silently. Add a documented allow-list entry in the guard test with a reason that explains why the branch is non-production, unreachable, or otherwise safe.

## Related documentation

- [Developer Checklist](developer-checklist.md)
- [Release Validation](release-validation.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
- [Constraint Exception Policy](constraint-exception-policy.md)
