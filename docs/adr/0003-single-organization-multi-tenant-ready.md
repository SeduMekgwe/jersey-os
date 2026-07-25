# 0003 — Single-organization, multi-tenant-ready boundaries

- Status: Accepted
- Date: 2026-07-25

## Context

Initial operation serves one organization, but retrofitting tenant isolation later is costly and security-sensitive.

## Decision

Operate as a single organization while modeling organization ownership now. Organization-owned aggregates, memberships, roles, queries, unique constraints, events, jobs, cache keys, and audit records carry trusted organization context.

## Consequences

The first release has modest scope plumbing and test overhead. It avoids global-data assumptions and enables future organizations without claiming complete multi-tenancy. Isolation, provisioning, quotas, billing, and per-tenant operations still require explicit future work.

## Alternatives considered

Global-only models are simpler initially but create dangerous migrations. Full multi-tenant product behavior now adds features not yet required.

## Validation

Cross-organization integration tests fail closed; reviews require explicit ownership for new data and operations.
