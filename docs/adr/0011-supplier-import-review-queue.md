# 0011 — Supplier import and review queue

## Status

Accepted

## Context

Catalog products need a controlled path from supplier feeds without auto-publishing or bypassing operator review.

## Decision

- Own import state in an Import module (`import_*` tables): `Supplier`, `ImportBatch`, `ImportItem`.
- First feed adapter is CSV upload through `ISupplierCatalogFeed` / `CsvSupplierCatalogFeed`.
- Parsing runs asynchronously via Hangfire (`ParseImportBatchJob`).
- Operators review items, then approve into Catalog as **Draft** products/variants (SKU match updates existing variants).
- Permissions: `import.read`, `import.upload`, `import.review`.

## Consequences

Import never writes Active catalog state. HTTP supplier APIs are covered in ADR 0018 via the same feed port and review queue. Publishing remains a separate concern.
