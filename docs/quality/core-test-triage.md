# Core Test Triage

Issue #134 tracks the first focused survivor-triage pass after the initial Core Stryker.NET baseline from Issue #121.

The goal of this pass was not to chase a raw score. The goal was to strengthen tests around Core governance behavior that would matter if it changed: evaluator composition, decision outcomes, audit residue, and acknowledgment or responsibility-handshake flow.

This page is now historical context for the first Core mutation survivor-triage pass. Post-`1.1.0` test hardening extends the broader Core coverage story into DLP/classification, provider-neutral governance emission, durable outbox contracts, signing and verification policy, canonical payload building, and capability grant validation. Those later xUnit coverage issues are coverage-hardening work unless a future issue explicitly expands mutation scope.

## Scope reviewed

| Area | Behavior under review | Triage classification | Action in this pass |
| --- | --- | --- | --- |
| `Evaluation` | Deny-wins composition, warning aggregation, policy metadata propagation, cancellation checks, and whether all constraint results reach decision policy. | Intentional behavior that needed stronger test coverage. | Added evaluator tests for multiple denials, warning aggregation, continued evaluation after denial, decision-policy result visibility, and cancellation between constraints. |
| `Decisions` | Outcome factories, reason-code/message preservation, trace and policy metadata propagation, and read-only reason snapshots. | Missing or weak assertion coverage. | Added decision tests for non-allow factory trace fields, reason message preservation, and read-only snapshot behavior. |
| `Audit` | `FromDecision` and `FromConstraint` propagation of outcome, reason codes, actor data, trace data, policy data, metadata, timestamp normalization, and source-collection aliasing. | Missing or weak assertion coverage. | Added audit tests for full trace/policy propagation and metadata snapshot behavior. |
| `Handshakes` | `FromDecision` reason selection, trace and policy propagation, metadata normalization, and acknowledgment response identity boundaries. | Missing or weak assertion coverage. | Added handshake tests for first-reason selection, decision trace/policy propagation, metadata snapshot behavior, and responding-actor identity. |

## Addressed themes

1. **Deny-wins composition**
   - Multiple denied constraint results must aggregate denial reason codes in order.
   - Warning reason codes must not leak into the final denied decision.
   - Correlation ID, policy version, and policy hash must continue to propagate from the evaluation context.

2. **Warning-only composition**
   - Multiple warning results must aggregate warning reason codes in order.
   - Allow and not-applicable results must not create reason codes.
   - Warning decisions remain non-blocking through `CanProceed`.

3. **Constraint-result visibility to decision policy**
   - The evaluator must continue to run all constraints before composing policy output.
   - Decision policy must receive the composed decision and the full constraint-result list.
   - A denial must not silently short-circuit away later result visibility unless a future design explicitly changes that behavior.

4. **Cancellation between constraints**
   - Cancellation requested after one constraint must prevent the next constraint from running.
   - This protects the cancellation check at the top of each evaluator loop iteration.

5. **Decision factory propagation**
   - Non-allow decision factories must preserve reason code, reason message, correlation ID, trace ID, policy version, and policy hash.
   - Deferred, acknowledgment-required, and escalation-recommended outcomes must stay non-proceeding.

6. **Read-only snapshot boundaries**
   - Decision reasons and reason codes must not alias source lists.
   - Read-only collection views must reject mutation attempts.
   - Audit and handshake metadata must snapshot normalized values rather than reflecting later source-dictionary changes.

7. **Audit and handshake boundary behavior**
   - Audit residue must preserve full decision and constraint trace/policy data.
   - Handshake requests created from decisions must use the first decision reason as the display reason.
   - Acknowledgment responses must use the responding actor identity while preserving the request handshake boundary and acknowledgment code.

## Post-1.1.0 coverage-hardening context

The first mutation-triage pass focused on evaluator, decision, audit, and handshake behavior. The `1.1.0` stable package family added or promoted additional Core surfaces that now receive targeted line and branch coverage hardening:

| Issue | Focus | Relationship to mutation triage |
| --- | --- | --- |
| [#246](https://github.com/AsiBackbone/AsiBackbone/issues/246) | Capability grant validation branches. | Coverage hardening for public capability validation behavior. |
| [#247](https://github.com/AsiBackbone/AsiBackbone/issues/247) | Signing verifier and verification policy outcome branches. | Coverage hardening for signing/verification trust behavior. |
| [#248](https://github.com/AsiBackbone/AsiBackbone/issues/248) | Canonical payload builder branches. | Coverage hardening for deterministic signing/hash payload behavior. |
| [#249](https://github.com/AsiBackbone/AsiBackbone/issues/249) | Governance emission and durable outbox domain branches. | Coverage hardening for provider-neutral emission/outbox public behavior. |
| [#250](https://github.com/AsiBackbone/AsiBackbone/issues/250) | DLP/classification policy branches. | Coverage hardening for DLP failure-policy public behavior. |
| [#262](https://github.com/AsiBackbone/AsiBackbone/issues/262) | Core-specific branch coverage quality gate. | CI/reporting guard that separates Core branch coverage from repository-wide line coverage. |

These issues should be read as public-behavior coverage work. They do not automatically mean Stryker.NET mutation scope has expanded to every related type. Mutation expansion should remain explicit, targeted, and documented.

## Equivalent or non-actionable items

No mutants were suppressed in the first triage pass.

Candidate equivalent or non-actionable items should be confirmed against the generated Stryker.NET report before suppression. Do not suppress a mutant only because it is inconvenient. Suppression should be reserved for cases where the changed code is provably behavior-equivalent at the public API boundary or where the line belongs to non-runtime documentation-only code.

## Acceptable gaps for follow-up

Remaining follow-up work should be driven by the next Stryker.NET report and by the Core branch coverage report. Likely follow-up areas include:

- additional edge cases in capability-token behavior;
- additional audit ledger persistence edge cases outside Core primitives;
- signing/verification policy behavior where mutation testing adds signal beyond branch coverage;
- DLP/classification and governance-emission behavior only if a narrow mutation target is useful;
- decision-policy override combinations that may belong in scenario or integration tests;
- equivalent-mutant documentation if Stryker.NET reports mutants that cannot be killed through public behavior assertions;
- quality report publication after the report artifact is regenerated.

## Regeneration command

Run the Core mutation report from the Core test-project folder:

```bash
cd ./tests/AsiBackbone.Core.Tests
dotnet tool run dotnet-stryker -- --config-file stryker-config.json
```

The expected report output location remains:

```text
artifacts/mutation-report/core
```

After generating the report, review the remaining live items against this triage note and either strengthen tests or document confirmed equivalent/non-actionable items.
