# 0005 — Organization-scoped RBAC

- Status: Accepted
- Date: 2026-07-25

## Context

Authorization must be understandable to administrators, enforceable across modules, and safe for organization boundaries.

## Decision

Use role-based access control scoped to organization membership. Stable code-defined permission keys express capabilities; organization-owned roles group permissions; active memberships receive roles. Policies evaluate actor, organization, permission, and resource scope at application boundaries.

## Consequences

RBAC is auditable and administratively familiar. Permission taxonomy and role changes need governance and cache invalidation. Fine-grained ownership rules remain explicit policies rather than role proliferation.

## Alternatives considered

Global roles cannot safely represent organization context. User-by-user grants are hard to administer. Full attribute-based policy is more flexible but unnecessarily complex as the foundation.

## Validation

Authorization-matrix and cross-organization tests cover each protected capability; assignment changes are audited.
