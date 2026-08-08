# 0015 — Azure Blob object storage adapter

## Status

Accepted

## Context

Catalog and import store media and feed files behind `IObjectStorage`. The local filesystem adapter is fine for development but is not durable across API/worker instances, and Shopify `productSet` media requires absolute HTTPS URLs. ADR 0010 deferred Azure Blob; production hosting needs that adapter now.

## Decision

- Select the adapter with `ObjectStorage:Provider` = `Local` | `AzureBlob` (default `Local`).
- Implement `AzureBlobObjectStorage` with Azure.Storage.Blobs (connection string + container; ensure container exists at resolve).
- Extend `IObjectStorage` with `OpenReadAsync` so import parsing works for both adapters (no Local cast).
- `GetUrl` for Azure returns absolute HTTPS URLs (`AzureBlob:PublicBaseUrl`, else `ObjectStorage:PublicBaseUrl`, else container URI + key).
- Local may set `ObjectStorage:PublicBaseUrl` to emit absolute URLs; otherwise keep relative `/media/...`.
- Fail fast at DI registration when Azure is selected without connection string or container name.
- API and Worker share the same `ObjectStorage` configuration section.

## Consequences

Hosted multi-instance deployments can share durable media and publish absolute image URLs to Shopify. Existing local files are not migrated automatically; operators copy once if needed. CDN/custom domains can sit in front via `PublicBaseUrl` without changing call sites. WooCommerce and image transforms remain out of scope.

## Alternatives considered

- Keep Local only and mount a shared volume — fragile across platforms and still awkward for Shopify absolute URLs.
- Signed/SAS URLs per request — unnecessary while containers (or a CDN) can be publicly readable for product media.
- RBAC/managed identity only — deferred; connection string (or account+key via secrets) matches current ops model.

## Validation

Unit tests cover Azure URL shape and put/delete/open via a fake blob gateway; Local put/delete/url/open; DI throws when Azure secrets are missing. Build/test suite must pass without Docker beyond existing coverage.
