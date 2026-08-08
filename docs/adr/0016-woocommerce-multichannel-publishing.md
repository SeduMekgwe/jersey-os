# 0016 — WooCommerce and multi-channel publishing

## Status

Accepted

## Context

Publishing was Shopify-only: a single process-wide `ISalesChannelPublisher` and jobs hardcoded to `SalesChannelCodes.Shopify`. The product must project Active catalog products to WooCommerce as well, without changing Catalog as source of truth.

## Decision

- Add `SalesChannelCodes.WooCommerce` and bootstrap a disabled WooCommerce channel per organization.
- Introduce `ISalesChannelPublisherResolver` keyed by channel code; Shopify uses `NullSalesChannelPublisher` when `Shopify:AccessToken` is empty; Woo returns null (skip/fail channel run) when credentials are missing.
- Implement `WooCommerceSalesChannelPublisher` against WooCommerce REST (`/wp-json/wc/{version}`) with Basic auth (`ConsumerKey`/`ConsumerSecret`).
- `PublishProductJob` and `SyncInventoryJob` fan out across all **enabled** sales channels; each channel keeps its own `PublishRun` and `ExternalIdMap` rows.
- Variant external ids for Woo are stored as `{productId}:{variationId}` so inventory updates can target the variation endpoint.
- Config: `WooCommerce:StoreBaseUrl`, `ConsumerKey`, `ConsumerSecret`, `ApiVersion`.

## Consequences

Multi-channel outbound sync works with existing publish tables. Woo inbound order webhooks remain future work. Enabling Woo without credentials marks that channel’s run failed without blocking sibling channel adapters from running in the same job.
