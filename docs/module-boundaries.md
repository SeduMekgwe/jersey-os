# Module boundaries

## Initial modules

- **Identity:** credentials, sessions, refresh-token families, account lifecycle.
- **Organizations:** organization identity, membership, invitations, tenant context.
- **Access:** roles, permissions, assignments, authorization decisions.
- **Catalog:** products, variants (including unit price), taxonomy (team/season/category/tag), images, inventory levels; org default currency.
- **Import:** suppliers, CSV feed batches, review-queue items, approve-to-draft catalog writes.
- **Publishing:** sales channels, external ID maps, publish runs, outbound sync to Shopify and WooCommerce, inbound Shopify order/fulfillment/refund webhooks for inventory reservation.
- **Operations:** scheduled work, operational workflows, and their status.
- **Notifications:** message intent, templates, delivery attempts, provider adapters.
- **Audit:** append-only security and business audit records.

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
