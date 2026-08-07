# EF Core Outbox Bulk-Claim Performance Evaluation

Issue: [#589](https://github.com/AsiBackbone/AsiBackbone/issues/589)

Status: **Evaluation complete. Keep the portable per-row implementation as the production baseline. Do not add provider-specific raw SQL to `EfCoreGovernanceOutboxStore`.**

## Purpose

This record evaluates whether `EfCoreGovernanceOutboxStore` should replace or supplement its provider-neutral per-row optimistic claim path with a set-based claim operation.

The investigation covers:

* batch sizes of 1, 10, 50, and 100;
* relational command-count and latency sensitivity;
* provider-neutral `ExecuteUpdateAsync` feasibility;
* SQL Server and PostgreSQL-style atomic claim patterns;
* claim ordering, eligibility, lease, token, attempt-count, and competing-worker guarantees;
* the package boundary for any future optimized implementation.

This is a performance investigation, not a correctness repair. The existing implementation remains valid and intentionally favors portable, explicit lease semantics.

## Current portable claim path

For an uncontended successful pending batch, the current flow is:

1. query an ordered candidate-ID batch;
2. re-read each candidate by outbox entry ID;
3. recheck eligibility and lease state;
4. create a unique claim token and increment `ClaimAttemptCount`;
5. save one optimistic-concurrency update;
6. return only successful claim winners.

The retry-ready path uses the same claim loop after applying its retry eligibility and ordering rules.

The ordering rules remain:

* pending: `CreatedUtc`, then `OutboxEntryId`;
* retry-ready: `NextRetryUtc ?? UpdatedUtc`, then `OutboxEntryId`.

The second read is not redundant from a correctness perspective. It narrows the stale-candidate window by rechecking eligibility immediately before the concurrency-checked update.

## Relational integration harness

`EfCoreGovernanceOutboxClaimRoundTripTests` runs the portable path against relational SQLite and verifies the uncontended command count for requested batches of 1, 10, 50, and 100.

For `N` successful claims:

```text
commands = 1 candidate query + N row reads + N row updates
         = 1 + 2N
```

| Requested batch | Confirmed database commands |
| ---: | ---: |
| 1 | 3 |
| 10 | 21 |
| 50 | 101 |
| 100 | 201 |

The harness also confirms that every requested row is claimed, claim tokens are unique, attempt counts increment to one, and the persisted claim owner, token, acquisition time, and expiration match the returned claims.

Under contention, a candidate that becomes ineligible before its second read may consume only the read. A candidate that loses during its concurrency-checked update still consumes both per-row commands. The table therefore describes the stable all-success baseline used for latency analysis.

## Latency sensitivity

The command count makes network latency the dominant scaling factor for a remote relational database. The following table is an analytical latency-only floor derived from the confirmed command count. It excludes query execution, serialization, EF Core materialization, context creation, server load, locking, and network jitter.

| Batch | Commands | Added floor at 1 ms/command | Latency-only ceiling at 1 ms | Added floor at 5 ms/command | Latency-only ceiling at 5 ms |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 3 | 3 ms | 333.3 claims/s | 15 ms | 66.7 claims/s |
| 10 | 21 | 21 ms | 476.2 claims/s | 105 ms | 95.2 claims/s |
| 50 | 101 | 101 ms | 495.0 claims/s | 505 ms | 99.0 claims/s |
| 100 | 201 | 201 ms | 497.5 claims/s | 1,005 ms | 99.5 claims/s |

The asymptotic throughput ceiling is approximately `1 / (2L)` claims per unit time, where `L` is per-command latency. Larger batches amortize the one candidate query, but they do not remove the two commands per successful row.

These are not universal production benchmark numbers. Hosts should measure with their actual provider, connection topology, isolation configuration, indexes, workload, and contention level before selecting an optimized adapter.

## Provider-neutral `ExecuteUpdateAsync` evaluation

`ExecuteUpdateAsync` is useful when a caller only needs to apply one set of values to a translated query and receive an affected-row count. It does not provide the complete portable primitive required by the outbox claim contract.

A correct bulk claim must atomically:

1. select a bounded ordered eligible set;
2. skip or resolve rows held by competing workers;
3. assign a unique claim token to every winning row;
4. assign a new concurrency stamp to every winning row;
5. increment each row's existing attempt count;
6. return the actual winning rows and their generated claim metadata;
7. preserve enough ordering information to return claims deterministically.

The provider-neutral limitations are:

* EF Core does not expose a cross-provider ordered, bounded update-and-return contract;
* `ExecuteUpdateAsync` returns an affected-row count, not the updated entities needed to construct `GovernanceOutboxClaim` values;
* client-evaluated token values do not provide a portable per-row uniqueness guarantee;
* database-side UUID functions and update-returning syntax are provider-specific;
* a bulk update followed by a second read reintroduces an ownership race unless enclosed by carefully controlled provider-specific transaction semantics;
* skip-locked and lock-hint behavior is not portable through the base EF Core API.

A provider-neutral set-based branch would therefore either weaken current guarantees or hide provider-specific behavior behind a misleadingly portable surface.

## Provider-specific prototype evaluation

### SQL Server

A viable SQL Server adapter could use an updateable ordered CTE with `UPDLOCK`, `READPAST`, an appropriate isolation mode, and `OUTPUT`. This may reduce claim acquisition to one server round trip while returning actual winners.

A production implementation must still validate against SQL Server itself:

* lock hints and transaction isolation, including read-committed snapshot behavior;
* the updateable-CTE plan and index use;
* starvation under sustained `READPAST` contention;
* deadlock and retry handling;
* result ordering, which may require client-side reordering;
* retry-ready predicate and nullable ordering equivalence;
* token and concurrency-stamp format compatibility;
* cancellation and transaction cleanup.

### PostgreSQL

A PostgreSQL adapter could use a locked candidate CTE with `FOR UPDATE SKIP LOCKED`, followed by `UPDATE ... RETURNING`. This can return partial winners while avoiding rows already held by competing workers.

It still requires real-provider coverage for extension availability, isolation, ordering, starvation, retry-ready equivalence, token generation, and result reordering.

### SQLite

SQLite supports set-based updates and returning clauses in modern versions, but its database-level write serialization does not provide the same skip-locked row-claim model as SQL Server or PostgreSQL. A SQLite-specific bulk path would add complexity without addressing the primary high-volume multi-worker scenario. The portable optimistic path remains the appropriate SQLite baseline.

## Correctness requirements for a future optimized store

| Requirement | Required behavior |
| --- | --- |
| Eligibility | Re-evaluate status, retry readiness, and lease expiration in the atomic claim statement. |
| Ordering | Select the same bounded order and return claims in that order after provider result reordering. |
| Partial winners | Return only rows actually acquired by the worker. |
| Claim token | Generate a unique opaque token per winning row. |
| Attempt count | Increment exactly once for every successful acquisition or reacquisition. |
| Lease timestamps | Store normalized UTC claim and expiration values derived from the request. |
| Concurrency stamp | Replace the stamp on every winning update. |
| Competing workers | Prevent two workers from both receiving a valid claim for the same lease instance. |
| Completion | Continue requiring owner/token verification for final transitions. |
| Failure behavior | Roll back or return no ambiguous claims when the atomic statement fails. |

Provider-specific race tests must run against the actual provider. SQLite tests cannot prove SQL Server lock-hint or PostgreSQL skip-locked behavior.

## Package-boundary decision

Do not branch on provider name inside `EfCoreGovernanceOutboxStore`.

If a provider-optimized claim path is later justified, prefer a separate implementation or package, for example:

```text
AsiBackbone.EntityFrameworkCore.SqlServer
AsiBackbone.EntityFrameworkCore.PostgreSql
```

The optimized store should implement `IAsiBackboneGovernanceOutboxClaimStore` and remain explicitly registered by the host. This keeps raw SQL out of the portable package, provider dependencies optional, transaction behavior explicit, and correctness tests scoped to the engine supplying the guarantee.

## Optimization threshold

Provider complexity is justified only when representative real-provider testing demonstrates:

* at least a 2x sustained throughput improvement for batches of 50 or 100 under the target network latency;
* materially lower p95 claim latency under competing workers;
* no duplicate claim winners in stress testing;
* no loss of ordering, retry eligibility, token uniqueness, attempt counts, lease expiration, or concurrency-stamp behavior;
* acceptable starvation, deadlock, and cancellation behavior;
* a production host or provider package with a demonstrated need for the additional maintenance surface.

## Decision

The current investigation does not justify a production bulk path in the provider-neutral store.

The accepted outcome is:

1. retain the existing per-row optimistic-concurrency implementation;
2. keep its command shape and claim metadata covered by relational integration tests;
3. document the latency-sensitive throughput model and its limitations;
4. document SQL Server and PostgreSQL set-based approaches as provider-specific future options;
5. require a separate optimized store or package rather than branching inside the base store;
6. introduce no raw SQL or production behavior change for this issue.

This closes the performance investigation while preserving a measurable path for future provider-backed optimization when real workload evidence warrants it.
