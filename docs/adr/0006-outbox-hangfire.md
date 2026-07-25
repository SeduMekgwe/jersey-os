# 0006 — Transactional outbox and Hangfire

- Status: Accepted
- Date: 2026-07-25

## Context

Database changes and asynchronous side effects must not diverge, and scheduled/retryable work needs durable execution.

## Decision

Write integration events to an outbox in the same transaction as domain state. A dispatcher publishes or enqueues them after commit. Use Hangfire for durable background and scheduled jobs. Delivery is at least once; handlers must be idempotent, bounded, organization-scoped, and observable.

## Consequences

The design closes the database/message atomicity gap and supplies retries and operations tooling. It introduces outbox lag, cleanup, duplicate delivery, job-version compatibility, and storage/capacity monitoring.

## Alternatives considered

Direct post-commit publishing can lose events. Distributed transactions are poorly supported and operationally costly. An external broker may be added when scale or integration needs justify it.

## Validation

Integration tests simulate crashes around commit/dispatch, duplicate delivery, retry exhaustion, and concurrent workers.
