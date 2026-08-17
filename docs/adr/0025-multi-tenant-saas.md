# 0025 — Multi-tenant SaaS hardening

## Status

Accepted

## Context

ADR 0003 shipped organization-scoped data with a single bootstrap org. Operators now need to provision additional tenants, invite members, isolate channel credentials, enforce quotas, and inspect tenants from a platform-admin surface. Billing is out of scope.

## Decision

- `platform.admin` provisions an organization (name, slug, admin credentials, currency). Seed includes Admin (all permissions except `platform.admin`), Member, sales channels, AI/notification templates, and a quota row. The bootstrap default org still grants `platform.admin` to its Admin role.
- Invitations use hashed tokens (`inv_…`), permission `org.members.manage`. Accept is anonymous; new users require a password, existing users attach.
- Per-tenant Shopify/Woo settings overlay global `IOptions`. Publishers resolve with `organizationId`. Shopify webhooks prefer `X-Shopify-Shop-Domain`.
- Quotas (`OrganizationQuota`) cap products, members, daily imports, and daily AI jobs. Defaults come from `Tenancy:DefaultMax*`.
- Login may target an organization; otherwise the earliest membership wins. `GET /auth/organizations` and `POST /auth/switch-organization` re-issue JWTs. No impersonation into another org’s data plane.
- Platform admin lists orgs and edits quotas. Those actions are audited. No billing.

## Consequences

Multiple orgs can run on one deployment with hard query-filter isolation. Channel secrets stay tenant-local. Operators switch orgs without a second password prompt. Cross-tenant reads remain closed except the audited admin list/quota APIs.

## Alternatives considered

- Billing/subscriptions now — deferred until a product requirement exists.
- Impersonation tokens for support — rejected; too easy to leak tenant data.
- Rewrite ADR 0003 — superseded in place by this ADR instead.

## Validation

Domain tests cover invitation/quota invariants. Application tests cover provision validation. OpenAPI includes admin orgs, invitations, settings, and org switch. Local bootstrap still creates the default org with `platform.admin`.
