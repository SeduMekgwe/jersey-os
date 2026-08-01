# 0012 — Publishing module and Shopify sales channel

## Status

Accepted

## Context

Active catalog products need to reach a customer storefront without making Shopify the system of record. Catalog and import already own product truth; publishing must push and sync outward.

## Decision

- Own channel state in a Publishing module (`publish_*` tables): `SalesChannel`, `ExternalIdMap`, `PublishRun`.
- Port `ISalesChannelPublisher` with production adapter `ShopifySalesChannelPublisher` (Admin GraphQL) and `NullSalesChannelPublisher` when credentials are absent.
- Publish **Active** products only; archive maps to Shopify draft/unpublish; available stock is `OnHand − Reserved`.
- Trigger via outbox events `jerseyos.product.activated|updated|archived` and `jerseyos.inventory.adjusted` into Hangfire `PublishProductJob` / `SyncInventoryJob`.
- Org-scoped Shopify settings: `Shopify:ShopDomain`, `Shopify:AccessToken`, `Shopify:ApiVersion`.
- Permissions: `publishing.read`, `publishing.manage`.

## Consequences

Shopify remains a projection. Multi-channel adapters can implement the same publisher port. Webhooks inbound as source of truth stay out of scope.
