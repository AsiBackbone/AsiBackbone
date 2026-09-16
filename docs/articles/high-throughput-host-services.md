# High-Throughput Host Service Guidance

This article documents implementation guidance for host-provided governance services that may run on ASP.NET Core request hot paths, hosted outbox drain paths, or other high-volume execution paths.

AsiBackbone provides governance contracts, decision orchestration, host adapters, audit residue models, and outbox/drain primitives. It does not own the host application's database, network clients, external telemetry providers, retry policy, queue infrastructure, or production operations.

## Why host service throughput matters

AsiBackbone Core and ASP.NET Core adapters are intentionally small and provider-neutral. In real applications, the dominant runtime cost is often the host-owned implementation behind an AsiBackbone contract.

Common high-impact host services include:

- `IGovernancePolicyEvaluator<TContext>` registrations and host-owned constraints;
- `IEndpointCapabilityGrantValidator` implementations;
- `IDecisionReceiptSink.WriteAsync` implementations;
- `IGovernanceAuditLedgerStore`, lifecycle store, and outbox store implementations;
- `IGovernanceEmitter.EmitAsync` implementations used by the hosted outbox drain;
- DLP, classification, signing, verification, enrichment, SIEM, or exporter services invoked by the host.

A lightweight framework path can still become slow if a host implementation blocks threads, performs expensive network I/O per request, writes synchronously to a remote database, or starts unbounded background work.

## Where host code may run

### Request hot path

Endpoint governance can invoke host-provided services before the protected endpoint executes:

```text
HTTP request
  -> endpoint governance metadata lookup
  -> host-owned policy evaluation
  -> host-owned capability validation
  -> optional host-owned audit sink write
  -> host-owned endpoint execution when allowed
```

The policy evaluator, capability validator, and audit sink may run while an ASP.NET Core request is waiting. Their latency, allocation behavior, cancellation behavior, and failure behavior become part of the request experience.

### Outbox drain path

The hosted outbox drain can invoke host-provided storage and emitter services outside the request path:

```text
Hosted drain pass
  -> host-owned outbox store lookup
  -> host-owned governance emitter
  -> host-owned outbox status update
  -> host-owned retry, alerting, or dead-letter review
```

Moving expensive provider delivery behind a durable outbox removes that provider from the request hot path, but it does not remove the need for throughput management. Drain workers still need batching, bounded retry, queue-depth monitoring, backpressure decisions, and provider-failure handling.

## Responsibility boundary

| Concern | AsiBackbone responsibility | Host responsibility |
| --- | --- | --- |
| Core contracts | Define provider-neutral interfaces and models. | Implement storage, providers, credentials, and operational behavior. |
| ASP.NET Core endpoint governance | Read endpoint metadata, build context, invoke host services, and map safe outcomes. | Keep policy evaluators, validators, and audit sinks efficient and cancellable. |
| Decision receipt | Provide decision receipt models and sink abstractions. | Choose durable storage, indexing, retention, batching, signing, and write behavior. |
| Outbox | Provide provider-neutral outbox models and drain primitives. | Provide durable store semantics, leasing/claiming if needed, idempotency, retry, and monitoring. |
| Provider emission | Define provider-neutral emission result vocabulary. | Configure exporters, SIEM, cloud, network clients, throttling, credentials, and failure handling. |
| Backpressure | Expose places where host policy can fail, defer, retry, or dead-letter. | Decide whether to reject, defer, shed load, queue, throttle, page operators, or pause a provider path. |

## Request hot-path guidance

For services that run before a protected endpoint executes:

- Use async, non-blocking I/O end to end.
- Propagate the supplied `CancellationToken` to database, HTTP, storage, signing, verification, and exporter calls.
- Keep policy evaluation deterministic and bounded where possible.
- Prefer precomputed policy data, cached static metadata, compiled rules, or local lookup tables for routine checks.
- Avoid per-request reflection, full-document parsing, expensive cryptographic work, remote telemetry delivery, or large payload serialization unless the endpoint risk justifies the latency.
- Return fail-closed or degraded outcomes explicitly when dependencies are missing, unavailable, or too slow.
- Avoid exposing reason-code internals or sensitive metadata in HTTP responses just because a host service failed.

### Blocking anti-pattern

Do not wrap asynchronous provider calls in blocking waits or add artificial sleeps inside request-time governance services.

```csharp
public ValueTask WriteAsync(
    IDecisionReceipt receipt,
    CancellationToken cancellationToken = default)
{
    // Anti-pattern: blocks a request thread and ignores cancellation.
    _httpClient.PostAsJsonAsync("/audit", receipt).Result;

    // Anti-pattern: ties up the request path during bursts.
    Thread.Sleep(TimeSpan.FromMilliseconds(250));

    return ValueTask.CompletedTask;
}
```

Problems with this pattern:

- request threads are blocked while I/O waits;
- cancellation is ignored after the client disconnects or request times out;
- thread-pool starvation can amplify under load;
- a slow downstream audit service becomes a request-throughput bottleneck;
- the failure mode is difficult to distinguish from framework overhead.

### Async host-service pattern

Prefer asynchronous implementation, cancellation propagation, timeout-aware clients, minimized payloads, and a local durable write or bounded outbox where the operation must survive provider failure.

```csharp
public async ValueTask WriteAsync(
    IDecisionReceipt receipt,
    CancellationToken cancellationToken = default)
{
    AuditRecord record = AuditRecord.FromResidue(receipt);

    await _db.AuditRecords
        .AddAsync(record, cancellationToken)
        .ConfigureAwait(false);

    await _db.SaveChangesAsync(cancellationToken)
        .ConfigureAwait(false);
}
```

For expensive external delivery, prefer this request-time shape:

```text
Request path
  -> write minimized local audit record
  -> enqueue governance emission envelope
  -> return governed response

Background path
  -> drain bounded batches
  -> emit to provider
  -> persist delivered, deferred, retryable, failed, or dead-lettered state
```

## Policy evaluator and validator guidance

Host-owned policy evaluators and capability validators should be treated as security-sensitive hot-path code.

Recommended characteristics:

- independent cancellation for all remote dependencies;
- stable reason-code vocabulary rather than expensive dynamic explanation generation;
- bounded metadata size and bounded capability-scope lists;
- explicit timeout policy for remote policy stores or capability introspection;
- deterministic deny/defer behavior when required policy data cannot be loaded;
- caching only for data that is safe to cache under host policy;
- clear cache invalidation when policy version, policy hash, tenant, region, or workload changes.

Avoid:

- network calls to multiple providers for every request when the policy can be locally projected;
- loading entire user, tenant, document, or policy graphs when a narrow claim set is enough;
- unbounded recursive policy evaluation;
- synchronous database or LDAP calls;
- CPU-heavy rule compilation inside `EvaluateAsync`;
- calling external AI/model services from a request-time validator without timeout, budget, and fallback policy.

## Audit sink guidance

An audit sink can sit directly on the request path. Keep its behavior clear:

- For low-risk development flows, in-memory sinks are acceptable but non-durable.
- For production-style flows, write a minimized local audit record before optional provider emission.
- Keep audit payloads bounded; store opaque identifiers, policy versions, policy hashes, correlation IDs, trace IDs, and minimized reason codes.
- Do not store secrets, access tokens, raw capability grants, connection strings, raw prompts, full protected documents, or unredacted sensitive data.
- Avoid remote telemetry delivery as the only audit write on a request path.
- If the audit write is required for accountability and fails, fail closed or defer according to host policy rather than silently continuing.

When audit delivery to external systems is expensive or unreliable, use a durable outbox so the request path records the local accountability artifact and the background drain handles provider pressure.

## Outbox emitter guidance

`IGovernanceEmitter.EmitAsync` implementations run during drain passes and may dominate background-worker throughput.

Recommended characteristics:

- batch provider calls where the provider supports batching;
- honor provider throttling and retry-after signals;
- return provider-neutral `GovernanceEmissionResult` values instead of throwing for expected provider failures;
- use bounded retries with dead-letter or quarantine behavior;
- preserve provider record identifiers when available;
- make provider delivery idempotent by envelope ID, outbox entry ID, or host-selected idempotency key;
- expose provider latency, result counts, retry counts, and dead-letter counts.

Avoid:

- one unbounded outbound HTTP request per outbox record when batching is available;
- infinite retry loops inside the emitter;
- ignoring cancellation during shutdown or deployment recycle;
- swallowing provider errors without updating the outbox state;
- using `Task.Run` to create untracked emission work that can be lost when the process stops.

## Queue depth, backpressure, and bounded work

High-throughput hosts should decide what happens when governance work arrives faster than it can be written, drained, or emitted.

Useful host policies include:

- bounded queue depth for in-process channels or worker queues;
- explicit rejection, defer, or fail-closed behavior when a queue is full;
- separate queues or partitions by tenant, region, workload, or provider path;
- larger drain batches before reducing polling intervals too aggressively;
- backoff when downstream providers throttle or fail;
- circuit-breaker style pausing for known-bad provider paths;
- operator alerts when oldest pending record age or queue depth exceeds the host threshold.

Avoid unbounded per-request work:

```csharp
// Anti-pattern: unbounded fire-and-forget work can hide failure and exhaust resources.
_ = Task.Run(() => _externalTelemetryClient.Send(receipt));
```

A better shape is a bounded, observable handoff:

```text
Try enqueue locally
  -> accepted: return or continue according to policy
  -> full: deny, defer, shed, or fail closed according to host risk
  -> monitor: queue depth, oldest item age, enqueue failures, drain rate
```

## Cancellation-token expectations

Host implementations should treat the supplied cancellation token as meaningful:

- Request-path services should stop avoidable work when the request is aborted.
- Outbox drain services should stop promptly during graceful shutdown.
- Database, HTTP, signing, verification, and exporter calls should receive the token.
- Long-running loops should check the token between records or batches.
- `OperationCanceledException` caused by the supplied token should be logged or classified differently from provider failure when the distinction matters.

Cancellation should not corrupt local accountability state. If a host must guarantee an audit write before returning, the host should use a durable transaction boundary that matches its own risk policy and should document whether request cancellation can interrupt that boundary.

## Failure behavior by path

| Path | Common failure | Recommended host behavior |
| --- | --- | --- |
| Request-time policy evaluator | Remote policy store unavailable. | Deny, defer, or use documented cached policy only when safe. |
| Request-time capability validator | Token introspection times out. | Fail closed or require a fresh validated grant. |
| Request-time audit sink | Local durable store unavailable. | Fail closed or return a host-defined unavailable/deferred response for governed endpoints. |
| Request-time external telemetry call | Provider slow or unavailable. | Move external delivery behind local durable outbox. |
| Outbox store lookup/update | Database unavailable or concurrency conflict. | Retry with backoff, alert on sustained store failures, avoid duplicate provider calls without claiming/idempotency. |
| Governance emitter | Provider throttling or outage. | Return retryable/deferred result, honor retry-after, monitor backlog, dead-letter terminal failures. |
| Drain worker | Worker stopped or blocked. | Alert on stale heartbeat and non-zero queue depth. |

## Review checklist

Before putting a host-provided governance service on a hot path, confirm:

- It uses async non-blocking I/O.
- It propagates `CancellationToken` to downstream calls.
- It has a defined timeout budget.
- It avoids `.Result`, `.Wait()`, `Thread.Sleep`, synchronous network calls, and synchronous database calls.
- It keeps CPU-bound work bounded or precomputed.
- It uses bounded queue or batch behavior where work can accumulate.
- It exposes latency, failure count, and backlog metrics when relevant.
- It has documented fail-closed, defer, retry, or dead-letter behavior.
- It keeps audit and telemetry payloads minimized.
- It distinguishes AsiBackbone orchestration responsibilities from host-owned infrastructure responsibilities.

## Related documentation

- [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
- [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md)
- [Outbox Drain Reliability and Alerting](outbox-drain-reliability-and-alerting.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Safe Audit and Telemetry Data Guidance](safe-audit-telemetry-data.md)
- [Performance Benchmark Baseline](performance-benchmark-baseline.md)
