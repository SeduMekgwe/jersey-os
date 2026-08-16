# 0023 — Notifications and audit modules

## Status

Accepted

## Context

Module boundaries name Audit and Notifications, but operators only had entity stamp fields (`CreatedBy`/`ModifiedBy`) plus a single inventory `AuditLog` write. Authz denials, import approve, publish/scrape failures, and config changes were not queryable. There was no operator-facing delivery of import-ready or failure alerts.

## Decision

- **Audit:** keep the existing `AuditLogs` table as append-only business/security events. `IAuditRecorder` writes via constructor (no updates/deletes in the API). Record authorization denials (authenticated 403), import approve/reject, AI approve, inventory adjust, publish/scrape failures, and config changes (pricing rules, collections, supplier feed). Query `GET /audit/events`. Permission `audit.read`.
- **Notifications:** templates + messages + delivery attempts. Kinds: `import.ready`, `publish.failed`, `scrape.failed`. Hangfire queue `notifications`. Port `INotificationChannel`: default `Fixture`; `Webhook` POSTs JSON; `Email` uses SMTP when `Notifications:Provider=Email`. Permission `notifications.read`.
- Secrets stay in config. Do not log webhook URLs with credentials or SMTP passwords.

## Consequences

Local/CI works without SMTP or a webhook sink. Live alerting requires Worker listening on `notifications` plus Provider=Webhook or Email. SMS and ITSM remain out of scope.

## Alternatives considered

- Auto-email from MediatR domain events: rejected; those events already feed the outbox. Operator alerts are a separate delivery spine.
- Separate audit store: rejected; the foundation `AuditLogs` table is sufficient if writes stay append-only.

## Validation

Domain tests cover template render and message status. Fixture channel test covers local delivery. Architecture tests still forbid Domain/Application depending on Infrastructure.
