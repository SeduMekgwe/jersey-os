# 0018 — HTTP supplier feeds

## Status

Accepted

## Context

Import only accepted CSV uploads. Operators need scheduled and on-demand pulls from supplier HTTP endpoints into the same review queue, including JSON payloads, without bypassing approve-to-Draft.

## Decision

- Extend `Supplier` with `FeedKind` (`upload`|`http`), `FeedFormat` (`csv`|`json`), `FeedUrl`, optional `FeedBearerToken`, optional Hangfire `SyncCron`, and last-sync status fields.
- Introduce `ISupplierCatalogFeedResolver` keyed by format; add `JsonSupplierCatalogFeed` alongside CSV.
- `FetchSupplierFeedJob` GETs the feed (optional Bearer), stores bytes in `IObjectStorage`, creates `ImportBatch`, enqueues existing `ParseImportBatchJob`.
- Manual sync: `POST /api/v1/import/suppliers/{id}/sync`. Recurring schedules register on create/update and Worker startup.
- Bearer tokens are stored for the org supplier record and never returned from the API (`hasFeedAuth` only).

## Consequences

HTTP and upload suppliers share one review/approve path. Scrape/Playwright remains a later milestone. Token encryption at rest and non-Bearer auth schemes stay follow-ups.
