# AsiBackbone.Storage.InMemory

Non-durable in-memory storage helpers for Accountable Systems Infrastructure local validation, samples, and tests.

This package provides non-durable storage implementations that make it easy to exercise ASI Backbone governance flows without requiring a database, EF Core provider, or host infrastructure.

> **New to AsiBackbone?** Start with the concept, not this package: [Intent to Execution: An Accountability Pattern](https://asibackbone.github.io/AsiBackbone/articles/intent-to-execution-pattern.html) and the [documentation site](https://cdcavell.github.io/AsiBackbone/). This README covers one package in the family.

> **Important:**
> This package is not durable storage. Do not use it as a production audit ledger, compliance archive, tamper-evident store, replay-protection store, or long-term accountability store.

## What this package provides

- In-memory audit ledger behavior for local validation and tests.
- In-memory governance outbox behavior for local validation, samples, and integration tests.
- In-memory capability grant use tracking for local validation, samples, and integration tests.
- Storage implementations that depend on `AsiBackbone.Core` only.
- A simple bridge for samples that need audit, outbox, or bounded-use capability grant records without introducing EF Core or a database.

## Intended usage

Use this package when:

- writing unit tests or integration tests around policy evaluation;
- building sample applications;
- validating audit residue and audit ledger behavior locally;
- validating governance outbox state transitions locally;
- validating first-use and replay-denied capability grant flows locally;
- demonstrating host-neutral ASI Backbone flows before adding durable storage.

For production persistence, use a host-owned durable storage strategy such as `AsiBackbone.EntityFrameworkCore` or a custom implementation of the Core storage contracts.

## In-memory capability grant use semantics

`InMemoryCapabilityGrantUseStore` implements `ICapabilityGrantUseStore` for tests, samples, and local validation. It is keyed by the stable capability grant token ID, consumes one use when accepted, honors `maxUseCount`, and returns `capability.use-limit-exceeded` when the local in-process count reaches that limit.

The store is thread-safe inside one process, and exposes local-validation helpers to mark grants as stopped or cancelled. It does not persist across process restarts, coordinate multiple replicas, provide distributed locks, or guarantee production replay protection.

## In-memory outbox concurrency semantics

`InMemoryGovernanceOutboxStore` is single-process and non-durable. It does not claim work across replicas, coordinate distributed workers, or provide exactly-once provider delivery.

Within that single process, same-entry outbox status transitions use compare-and-swap updates so local tests do not accidentally observe last-write-wins behavior. Delivered and dead-lettered entries are treated as terminal for subsequent in-memory state updates, and retryable failure updates against the same entry advance from the latest observed entry state.

## Boundary

The in-memory package should remain lightweight and host-neutral. It should not select a database provider, own migrations, expose ASP.NET Core middleware, or imply durable compliance-grade audit storage or production replay protection.
