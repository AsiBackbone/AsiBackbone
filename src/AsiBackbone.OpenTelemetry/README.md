# AsiBackbone.OpenTelemetry

`AsiBackbone.OpenTelemetry` adapts provider-neutral AsiBackbone governance emission envelopes into OpenTelemetry-friendly .NET diagnostics primitives.

The package implements `IAsiBackboneGovernanceEmitter` without making `AsiBackbone.Core` depend on OpenTelemetry exporters, Azure SDKs, SIEM SDKs, Event Hubs, Purview, robotics packages, or cloud-provider dependencies.

> **New to AsiBackbone?** Start with the concept, not this package: [Intent to Execution: An Accountability Pattern](https://asibackbone.github.io/AsiBackbone/articles/intent-to-execution-pattern.html) and the [documentation site](https://cdcavell.github.io/AsiBackbone/). This README covers one package in the family.

## Boundary

This package emits governance envelopes through:

- `ActivitySource` activity events and tags;
- `Meter` counters and histograms;
- provider-neutral `GovernanceEmissionResult` and `GovernanceEmissionError` outcomes.

It does not configure exporters. Hosts can route the diagnostics pipeline to Azure Monitor, Datadog, Grafana, Splunk, Elastic, or another backend through normal host-owned OpenTelemetry configuration.

## Recommended flow

```text
GovernanceEmissionEnvelope
  -> durable governance outbox
  -> AsiBackboneGovernanceOutboxDrain
  -> OpenTelemetryGovernanceEmitter
  -> ActivitySource / Meter
  -> host-configured OpenTelemetry exporters
```

The durable audit/outbox record remains the accountability baseline. OpenTelemetry is a projection path, not the authoritative ledger.
