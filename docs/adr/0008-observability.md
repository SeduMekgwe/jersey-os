# 0008 — Platform observability

- Status: Accepted
- Date: 2026-07-25

## Context

Requests, transactions, outbox dispatch, and background jobs cross execution boundaries; diagnosis requires correlated evidence without exposing sensitive data.

## Decision

Adopt OpenTelemetry-compatible traces and metrics plus structured logs. Propagate trace/correlation, actor, organization, module, and job context where safe. Define health probes and service-level indicators for availability, latency, errors, authentication, outbox lag, and job backlog.

## Consequences

Incidents and performance can be diagnosed across boundaries. Instrumentation, sampling, storage cost, cardinality, redaction, retention, and alert ownership require governance. Telemetry failures must not break business transactions.

## Alternatives considered

Unstructured logs are insufficient for correlation. Vendor-specific instrumentation creates lock-in; supported exporters may still target an approved vendor.

## Validation

Integration checks verify propagation and redaction; operational exercises verify dashboards and actionable alerts before production.
