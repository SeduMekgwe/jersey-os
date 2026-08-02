# 0013 — Catalog variant pricing

## Status

Accepted

## Context

Published Active products need a sellable unit price. Catalog is the system of truth; Shopify only receives a projection. No pricing engine existed.

## Decision

- Store nullable `PriceAmount` (`decimal(18,2)`) on `ProductVariant`.
- Store org `DefaultCurrency` (ISO 4217) on `Organization` (bootstrap default `ZAR`).
- Activate requires every variant to have `PriceAmount > 0`.
- Price changes raise `ProductUpdated` and flow through existing publish jobs; Shopify `productSet` includes variant `price`.
- Import CSV may include optional `price` / `price_amount`; approve applies it when present.
- Permissions remain `catalog.read` / `catalog.write`.

## Consequences

Compare-at, discounts, tax, and channel-specific prices stay future work. Orders/webhooks remain the next inventory-loop milestone.
