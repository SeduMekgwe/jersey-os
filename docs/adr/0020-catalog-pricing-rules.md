# 0020 — Catalog pricing rules

## Status

Accepted

## Context

ADR 0013 shipped unit price and org currency, and deferred compare-at, discounts, and channel-specific prices. Operators need markup/margin rules from supplier cost, compare-at, and optional per-channel overrides without a tax or promotions engine.

## Decision

- Keep pricing inside Catalog: `CostAmount` and `CompareAtAmount` on `ProductVariant`; org-scoped `PricingRule` rows in `catalog_pricing_rules`.
- Rule kinds: `MarkupPercent`, `MarginPercent`, `CompareAtPercent`. Optional `SalesChannelId` for publish-time overrides (ID reference only; no cross-module FK).
- `PricingCalculator` resolves catalog prices (org rules) and publish prices (channel rule preferred, else org rule, else stored amounts).
- Apply on import approve and variant upsert when cost is present and sell/compare-at are not explicit. Publish maps resolved sell + compare-at to Shopify `price`/`compareAtPrice` and WooCommerce `regular_price`/`sale_price`.
- Permissions: `pricing.read` / `pricing.write`. API under `/catalog/pricing-rules` plus preview.

## Consequences

Catalog remains source of truth for stored cost/price/compare-at; channels receive resolved projections. Tax, promotions, and B2B price lists stay out of scope.
