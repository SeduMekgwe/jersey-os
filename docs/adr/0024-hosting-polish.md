# 0024 — Hosting polish (CDN URLs, Blob RBAC, live ops status, API keys)

## Status

Accepted

## Context

ADR 0015 ships Azure Blob behind a connection string and documents `PublicBaseUrl` for CDN/custom domains, but production hosts prefer managed identity. Operators still poll import/publish/AI runs. Supplier and partner automations have no machine credential besides interactive JWT.

## Decision

- Keep `ObjectStorage:PublicBaseUrl` / `AzureBlob:PublicBaseUrl` as the CDN or custom-domain base for `GetUrl`. When set, values must be absolute http(s) URLs. Image transforms stay out of scope.
- Azure Blob `AuthMode` is `ConnectionString` (default) or `ManagedIdentity`. Managed identity uses `AzureBlob:ServiceUri` plus `DefaultAzureCredential` (RBAC on the storage account). Connection string remains valid.
- Live job status uses SignalR hub `/hubs/ops` (JWT via `access_token` query). Worker and API publish `OpsStatusEvent` over Redis `jerseyos:ops:status`. UI keeps HTTP polling as fallback when Redis/SignalR is down.
- Machine integrations use hashed API keys (`jos_{prefix}_{secret}`), header `X-Api-Key` or `Authorization: Bearer jos_...`, org-scoped permission claims from stored scopes. Permission `integrations.manage`. Plaintext is returned once at create; only SHA-256 is stored.

## Consequences

Hosted media can sit behind a CDN without code changes. Azure can drop storage account keys. Operators see import/publish/AI status without waiting for the next poll when Redis is up. Partners can call the same HTTP APIs without user passwords. SignalR is not a durability guarantee; Hangfire plus polling remain source of truth.

## Alternatives considered

- SAS URLs per media request — still unnecessary while containers or a CDN can be publicly readable.
- SignalR Redis backplane on API only — Worker is not a SignalR host, so explicit Redis pub/sub is required.
- Long-lived JWTs for machines — weaker revocation than hashed, prefixed keys.

## Validation

Object-storage tests cover invalid public URLs, connection-string fail-fast, and managed-identity ServiceUri validation without calling Azure. Domain tests cover API key scope/prefix/revoke. OpenAPI includes list/create/revoke API keys. Local/CI still default to Local storage, Fixture engines, and polling if Redis is absent.
