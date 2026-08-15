# 0021 — Catalog SEO and collections

## Status

Accepted

## Context

Catalog owned product identity and taxonomy but not storefront SEO or merchandising collections. Channels were projecting title/slug only. The product mission includes SEO and Collections as Catalog capabilities, with channels remaining projections.

## Decision

- Store optional product `SeoTitle`, `SeoDescription`, and `SeoHandle` on `Product`. Empty handle falls back to catalog slug at publish.
- Own `Collection` aggregates in Catalog: `Manual` membership (`catalog_collection_products`) or `Taxonomy` rules (team/season/category/tag AND filters).
- Permissions remain `catalog.read` / `catalog.write`. API under `/catalog/collections`.
- Publish upserts channel collections (Shopify custom collections, WooCommerce categories) via `ExternalIdMap` entity type `collection`, then sends resolved SEO plus collection IDs with the product.

## Consequences

Catalog remains source of truth for SEO and collection membership. Storefront rendering and a marketing CMS stay out of scope. AI-generated SEO copy is a later milestone (M7).
