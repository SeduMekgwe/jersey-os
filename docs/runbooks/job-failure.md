# Background job failure

Use when Hangfire failures, queue growth, outbox lag, duplicate effects, or a stalled worker threaten service objectives.

## Diagnose

1. Record UTC onset, queues, job/event types, affected organizations, backlog age, throughput, and recent releases.
2. Verify worker health, database/storage connectivity, locks, queue configuration, and dependency status.
3. Classify failures as transient, poison payload, code defect, capacity, configuration, or external dependency.
4. Inspect sanitized exception, attempt count, correlation ID, and idempotency state. Do not expose payload secrets.

## Contain

- Pause the affected queue or handler when continued processing can corrupt data or amplify external effects.
- Scale workers only after excluding poison jobs, lock contention, and downstream throttling.
- Roll back a defective deployment when schema and event compatibility permit.
- Isolate permanently invalid work to a failed state; retain evidence.

## Recover

Fix the cause, test with one representative item, then resume gradually. Retry only idempotent handlers and only from a known checkpoint. Never delete outbox records or mass-requeue without recording selection criteria and approval. Reconcile database state with external side effects.

## Verify and close

Confirm backlog age and failure rate return to baseline, no duplicate effects occurred, new work completes, and organization scope/correlation is preserved. Document impact, exact reprocessing range, residual failed items, and prevention actions.
