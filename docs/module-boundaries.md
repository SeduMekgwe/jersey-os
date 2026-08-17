# Module boundaries

## Initial modules

- **Identity:** credentials, sessions, refresh-token families, account lifecycle.
- **Organizations:** organization identity, membership, invitations, tenant context, quotas, and per-tenant channel settings.
- **Access:** roles, permissions, assignments, authorization decisions. Tenant Admin excludes `platform.admin`; Member is catalog/import-oriented.
- **Access:** roles, permissions, assignments, authorization decisions.
- **Catalog:** products, variants (unit price, cost, compare-at), pricing rules (markup/margin/compare-at, optional channel overrides), SEO metadata, collections (manual + taxonomy membership), taxonomy (team/season/category/tag), images, inventory levels; org default currency.
- **Import:** suppliers (upload + HTTP + Playwright scrape feeds), CSV/JSON batches, scrape run logs, review-queue items, approve-to-draft catalog writes.
- **Publishing:** sales channels, external ID maps, publish runs, outbound sync to Shopify and WooCommerce, inbound Shopify order/fulfillment/refund webhooks for inventory reservation.
- **AI:** prompt templates, generation jobs (titles/descriptions/SEO/alt text), operator approve-before-apply onto catalog or import items.
- **Operations:** scheduled work, operational workflows, live job status, and their status.
- **Notifications:** message intent, templates, delivery attempts, provider adapters.
- **Audit:** append-only security and business audit records.
- **Integrations:** hashed machine API keys and their permission scopes (Access/Identity adjacent).

Names describe ownership, not required deployment units. A new module needs a cohesive capability, its own model and data ownership, and an explicit public contract.

## Dependency rules

- Domain projects depend on no other module or infrastructure package.
- Application code may depend on its Domain and stable contract types.
- Infrastructure implements ports; API composes modules without owning policy.
- Modules expose commands, queries, DTOs, and integration events through a contracts surface.
- No module accesses another module's DbContext, tables, repositories, internal types, or migrations.
- Synchronous calls are acceptable for immediate consistency; integration events are preferred for propagation.
- Shared kernel code is limited to stable primitives such as identifiers, results, clocks, and organization context.

## Organization isolation

Organization-owned aggregates carry `OrganizationId`. Every organization-scoped command derives scope from trusted request context, every query filters it, and every unique constraint includes it where uniqueness is local. Global administration is explicit, separately authorized, and audited.

## Boundary tests

Automated architecture tests must reject prohibited project references and namespace dependencies. Integration tests must prove cross-organization reads and writes fail closed.
