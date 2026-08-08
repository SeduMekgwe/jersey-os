# 0014 — Shopify order webhooks and inventory reservation

## Status

Accepted

## Context

Published available stock is `OnHand − Reserved`, but nothing reserved stock when Shopify sold. Oversell was possible until orders closed the loop.

## Decision

- Extend `InventoryLevel` with `Reserve`, `Release`, and `Commit`.
- Accept HMAC-verified Shopify webhooks at `POST /api/v1/publishing/webhooks/shopify` (`orders/create|cancelled|fulfilled`).
- Persist idempotent `WebhookDelivery` rows (`publish_webhook_deliveries`) keyed by Shopify webhook id; process via Hangfire.
- Map line-item variant GIDs through `ExternalIdMap`; do not create catalog entities from webhooks.
- Config: `Shopify:WebhookSecret`. Operator list requires `publishing.read`.

## Consequences

Shopify remains a projection for catalog and a driver only for order-driven inventory movement. Refunds/partials and multi-channel webhooks stay future work.
