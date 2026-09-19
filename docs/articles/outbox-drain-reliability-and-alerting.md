# Outbox Drain Reliability and Alerting

This article documents provider-neutral operational guidance for monitoring, alerting, and recovering AsiBackbone outbox drain failures.

AsiBackbone can preserve local governance records and provide outbox/drain primitives, but it does not guarantee centralized compliance visibility, immutable evidence, legal non-repudiation, or successful external ledger delivery without host-owned configuration and operations.

## Why drain reliability matters

Durable local/outbox persistence reduces event-loss risk when a downstream provider is unavailable. It does not, by itself, prove that centralized monitoring, SIEM, compliance ledgers, governance catalogs, or observability backends received the event.

A regulated or review-sensitive host should treat sustained outbox backlog as an operational signal because it means governance events may be preserved locally but not visible to centralized reviewers.

The operational expectation is:

```text
Governed decision
  -> local audit / lifecycle record
  -> durable outbox record
  -> hosted or scheduled drain
  -> optional provider emission
  -> provider-side search, alerting, lineage, or review
```

The durable outbox is the reliability boundary. The drain process is the visibility bridge. If the bridge is stopped, misconfigured, or failing, the host may still retain records locally while losing centralized visibility.

Drain reliability is also a throughput concern. Slow host-owned stores, emitters, exporters, or provider SDKs can create backlog even when the AsiBackbone worker is healthy. See [High-Throughput Host Service Guidance](high-throughput-host-services.md) for async/non-blocking implementation guidance, blocking-call anti-patterns, batching, queue depth, backpressure, and host-service failure behavior.

## Host responsibility boundary

AsiBackbone provides provider-neutral outbox contracts, drain primitives, status vocabulary, and optional integration packages. The host remains responsible for:

- choosing the durable store;
- configuring the hosted drain or equivalent worker;
- configuring provider credentials, exporters, endpoints, and routing;
- exposing metrics, logs, traces, and health checks;
- alerting on sustained backlog or failure;
- defining retry and dead-letter policy;
- replaying or re-draining pending records during recovery;
- proving that provider-side retention, access control, and review workflows meet organizational requirements.

Do not describe local outbox persistence as centralized delivery, legal evidence, immutable storage, or compliance certification by itself.

## Observable signals

Hosts should expose the following provider-neutral signals regardless of whether the downstream target is OpenTelemetry, Azure Monitor, Event Hubs, Purview enrichment, a SIEM, or another system.

| Signal | What it shows | Suggested shape |
| --- | --- | --- |
| Outbox queue depth | Number of entries pending, retry-ready, deferred, failed, or dead-lettered. | Gauge by status, provider, tenant, region, or workload. |
| Oldest pending record age | How long the oldest non-delivered entry has waited. | Gauge or derived metric in seconds/minutes. |
| Drain attempt count | Whether the worker is actively attempting delivery. | Counter by provider and result. |
| Drain success count/rate | Whether provider handoff is succeeding. | Counter/rate by provider and result. |
| Drain failure count/rate | Whether provider handoff or worker execution is failing. | Counter/rate by normalized error code and provider. |
| Retry count | How many entries are cycling through retry. | Counter or gauge by retry bucket. |
| Dead-letter count | Number of entries that reached terminal or quarantine handling. | Counter/gauge by reason code and provider. |
| Last successful drain timestamp | Most recent successful provider handoff. | Timestamp or age-since-success gauge. |
| Last worker heartbeat timestamp | Whether the worker is alive even if no records are pending. | Timestamp, health-check value, or heartbeat metric. |
| Consecutive failure count | Whether failures are isolated or sustained. | Gauge by provider and worker instance. |
| Provider latency | Time spent attempting provider emission. | Histogram or summary by provider. |
| Store latency | Time spent reading/updating the durable outbox store. | Histogram or summary by store/provider. |

Prefer opaque identifiers and minimized metadata. Metrics should not expose raw prompts, raw document text, secrets, connection strings, access tokens, or protected data.

## Alerting recommendations

Thresholds should reflect host risk, provider behavior, expected volume, and regulatory expectations. The values below are starting points, not universal defaults.

| Alert condition | Suggested severity | Selection guidance |
| --- | --- | --- |
| Oldest pending record age exceeds expected drain SLA. | High | Start with 2-3 times the normal polling interval for low-volume systems; use a tighter bound for regulated workflows. |
| Outbox queue depth grows for multiple polling windows. | High | Alert when the backlog trend is sustained, not when one burst arrives. |
| No successful drain for a sustained period while queue depth is non-zero. | High | Treat as possible centralized visibility loss. |
| Worker heartbeat is stale. | High | Indicates the drain process may be stopped, blocked, or not scheduled. |
| Drain failure rate spikes above baseline. | Medium to high | Separate provider outage from application misconfiguration. |
| Retry count approaches max retry policy. | Medium | Gives operators time to intervene before dead-lettering. |
| Dead-letter count increases. | High | Requires review because records are no longer following the normal emission path. |
| Store read/update failures occur repeatedly. | High | Local reliability boundary may be compromised. |
| Provider latency remains elevated. | Medium | May indicate provider throttling, networking trouble, or downstream instability. |

For high-risk or regulated deployments, sustained backlog should page an operator or open an incident. For lower-risk telemetry-only deployments, backlog may create a ticket first, but it should still be visible.

## Suggested threshold-selection process

1. Measure normal queue depth, drain duration, and provider latency under expected traffic.
2. Define an expected delivery window for each provider path.
3. Choose an oldest-pending-age alert that is short enough to protect review visibility but long enough to avoid noisy alerts during routine bursts.
4. Add a backlog-growth alert so slow drains are caught before records age out.
5. Add a worker-heartbeat alert so a stopped drain is visible even when queue depth is low.
6. Add retry/dead-letter alerts for records approaching or reaching terminal handling.
7. Revisit thresholds after load tests, incident reviews, provider outages, and release changes.

Document the selected thresholds in the host runbook. AsiBackbone should not prescribe a single universal threshold because host systems differ in volume, risk, provider latency, and compliance obligations.

## Distinguishing temporary outage from persistent drain failure

Operators should be able to separate provider unavailability from permanent application failure.

| Symptom | Likely interpretation | Operator response |
| --- | --- | --- |
| Provider latency rises and retryable failures increase across many services. | Temporary provider or network instability. | Confirm provider status, reduce retry pressure if needed, monitor backlog age. |
| One service has stale heartbeat and no drain attempts. | Worker stopped, disabled, blocked, or not scheduled. | Check host configuration, deployment, service registration, and process health. |
| Queue depth grows but success count remains normal. | Drain may be underprovisioned for current volume. | Tune batch size, polling interval, workers, partitions, or provider throughput. |
| Dead-letter count increases with the same reason code. | Persistent payload, classification, schema, credential, or provider rejection issue. | Inspect minimized error details, quarantine if needed, fix mapping/configuration, replay when safe. |
| Store failures occur before provider emission. | Local persistence reliability issue. | Prioritize database/storage health; provider emission cannot be trusted until local state transitions persist. |

Provider-side outages should not erase the local accountability record. Application-side drain failures should be treated as incidents because they may prevent centralized governance visibility.

## Retry and dead-letter expectations

Retries should be bounded, observable, and recoverable.

Recommended host policy:

- Use retryable statuses for temporary provider, network, rate-limit, and timeout failures.
- Use deferred statuses when host policy intentionally delays emission.
- Use dead-letter handling for terminal, malformed, unauthorized, classification-blocked, or max-retry-exceeded records.
- Preserve normalized error codes and minimized error messages.
- Ensure operators can query dead-lettered records by correlation ID, provider, reason code, and time window.
- Treat dead-lettered governance records as review items, not as silently discarded telemetry.

Dead-letter handling is not proof of compliance. It is an operational quarantine or terminal-state mechanism that keeps failed records visible for review and recovery.

## Recovery and replay guidance

A host runbook should include recovery steps for pending, retryable, failed, deferred, and dead-lettered records.

Suggested recovery flow:

1. Confirm whether the local durable store is healthy.
2. Confirm whether the hosted drain or equivalent worker is running and configured.
3. Identify affected providers, tenants, regions, workloads, or time windows.
4. Inspect queue depth, oldest pending age, retry count, dead-letter count, and last successful drain timestamp.
5. Determine whether the failure is temporary provider unavailability, permanent provider rejection, local store failure, or worker misconfiguration.
6. Fix the underlying cause before replaying records.
7. Requeue eligible retryable or failed records according to host policy.
8. Re-drain in controlled batches to avoid provider throttling.
9. Verify that provider-side records arrive and can be searched by correlation ID or event ID.
10. Preserve an incident note that includes the affected time window, provider path, backlog size, oldest record age, recovery action, and residual dead-letter count.

Do not bulk-replay dead-lettered records without reviewing why they were dead-lettered. Some failures may indicate unsafe payloads, missing classification, invalid schema, authorization failure, or provider-side rejection.

## Dashboard checklist

A practical operations dashboard should answer:

- Is the worker alive?
- Is the queue empty or draining?
- How old is the oldest non-delivered record?
- Is backlog increasing or decreasing?
- When was the last successful drain?
- Which provider path is failing?
- Which reason code is most common?
- Are any records close to max retry?
- How many records are dead-lettered?
- Can an operator pivot from outbox record to correlation ID, decision receipt, provider event, and incident ticket?

The dashboard can be built in any monitoring system. AsiBackbone should remain provider-neutral.

## Optional provider-specific references

The guidance above is provider-neutral. Hosts may project or enrich the same signals through optional provider paths:

- [OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md) for .NET diagnostics projection and host-configured exporters.
- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md) for the overall local/outbox-before-provider architecture.
- [Purview Governance and Lineage Enrichment Strategy](purview-governance-lineage-enrichment-strategy.md) for optional governance/lineage enrichment rather than raw audit storage by default.
- [Event Hubs Governance Emission Provider Design](event-hubs-governance-emission-provider-design.md) for streaming provider design considerations.

If Azure Monitor, Log Analytics, SIEM, Purview, Event Hubs, or another provider is used, the host owns provider configuration, alert rules, access controls, retention, data minimization, and incident response.

## Non-goals

This guidance does not require a specific queue, database, telemetry provider, cloud service, or monitoring platform.

It does not implement a complete outbox worker, mandate a single retry policy, guarantee external ledger delivery, certify compliance, prove immutable storage, or create legal evidence guarantees.

## Related documentation

- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md)
- [High-Throughput Host Service Guidance](high-throughput-host-services.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Decision Receipt Observability Schema](decision-receipt-observability-schema.md)
- [Production Wording and Alpha Limitations](production-wording-and-alpha-limitations.md)
