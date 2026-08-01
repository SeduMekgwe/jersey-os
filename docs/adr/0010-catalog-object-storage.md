# 0010 — Catalog module and local object storage

## Status

Accepted

## Context

Product catalog core needs org-scoped products, variants, taxonomy, inventory, and product images without introducing channel publishing or a cloud object store yet.

## Decision

- Own catalog aggregates in the modular monolith under `catalog_*` tables with organization query filters.
- Product lifecycle uses explicit `Draft` / `Active` / `Archived` states rather than soft delete.
- Inventory is tracked per variant with optimistic concurrency.
- Image binaries use an `IObjectStorage` port with a local filesystem adapter (`ObjectStorage:LocalRootPath`) exposed under `ObjectStorage:PublicBasePath` for development; Azure Blob remains a future adapter behind the same port.
- Catalog and inventory permissions are `catalog.read`, `catalog.write`, and `inventory.adjust`.

## Consequences

Catalog features stay extractable later via table ownership and outbox events (`jerseyos.product.*`, `jerseyos.inventory.adjusted`). Local storage is not durable multi-instance storage; production deployments must swap the adapter before horizontal scale-out of media.
