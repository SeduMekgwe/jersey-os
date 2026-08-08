# 0017 — Shopify refunds and partial fulfillments

## Status

Accepted

## Context

ADR 0014 reserved on `orders/create`, released on `orders/cancelled`, and committed on `orders/fulfilled`. Partial fulfillments and refunds could oversell or leave reserved stock stuck because line quantities on cancel/fulfill did not match remaining reserved units after partial ship.

## Decision

- Add `InventoryLevel.ReleaseUpTo` / `CommitUpTo` for webhook tolerance when reserved is lower than payload quantity.
- Handle `fulfillments/create` by committing fulfillment line quantities (partials).
- On `orders/cancelled`, release `fulfillable_quantity` when present (remainder after partial fulfill), else `quantity`.
- Handle `refunds/create`: `restock_type=cancel` → release reserved; `return` → restock via `Adjust`; `no_restock` → ignore.
- Ignore `orders/partially_fulfilled` for inventory (commits come from fulfillments).
- Keep `orders/fulfilled` as CommitUpTo for full-order completion (safe if fulfillments already committed).

## Consequences

Shopify inventory loop covers partial ship and refund restock without a per-order ledger. Multi-order contention on the same variant can still race under extreme concurrency; webhook idempotency remains keyed by Shopify webhook id. WooCommerce inbound webhooks stay out of scope.
