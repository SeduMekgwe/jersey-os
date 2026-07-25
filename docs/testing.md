# Testing guide

## Test layers

- **Unit:** domain invariants, policies, value objects, pure mapping, and failure paths; no network or database.
- **Application:** use cases with controlled ports; verify authorization, transactions, idempotency, and emitted events.
- **Integration:** real database and infrastructure containers; verify migrations, queries, concurrency, Identity, outbox, and Hangfire behavior.
- **Contract:** OpenAPI compatibility, Problem Details, generated TypeScript compilation, and external adapter contracts.
- **End-to-end:** few critical journeys through deployed HTTP boundaries.
- **Architecture:** dependency direction and prohibited cross-module references.

## Required scenarios

Every organization-scoped feature tests same-organization success, cross-organization denial, missing scope, and privileged override where one exists. Write operations test retries and concurrency. Jobs test duplicate delivery, transient exhaustion, permanent failure, cancellation, and correlation propagation.

## Quality rules

- Tests are deterministic, isolated, parallel-safe, and use a controllable clock and identifiers.
- Prefer behavior assertions over implementation details; do not mock domain models.
- Test data builders default to valid data and require organization ownership explicitly.
- Integration tests apply migrations from an empty database.
- Never use production services, secrets, or copied personal data.
- Flaky tests are defects: quarantine only with owner, issue, and expiry.

## Local and CI gates

Run formatting, restore, build with warnings enforced, unit tests, integration tests, architecture tests, OpenAPI generation/diff, and TypeScript client type-checking. Pull requests must identify intentionally omitted tests. Coverage trends guide review, but critical invariants require explicit tests regardless of percentage.
