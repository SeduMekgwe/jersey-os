# Database conventions

Use a supported relational database with EF Core migrations. Each module owns a schema or consistently prefixed tables and its migration history.

## Conventions

- Tables and columns use `snake_case`; application types use idiomatic .NET naming.
- Primary keys are non-meaningful UUIDs generated application-side.
- Store timestamps as UTC `timestamp with time zone`; suffix names with `_at`.
- Organization-owned rows require non-null `organization_id` and an indexed foreign key.
- Concurrency-sensitive aggregates use an optimistic concurrency token.
- Foreign keys are enforced inside a module. Cross-module references store IDs without cross-schema foreign keys.
- Money uses decimal amount plus ISO 4217 currency; never floating point.
- Soft deletion is exceptional; prefer explicit lifecycle state and immutable audit records.
- Schema changes are forward-compatible, reviewed migrations. Use expand/migrate/contract for breaking changes.
- Never place secrets or raw refresh tokens in the database; store one-way token hashes.

## Foundation model

```mermaid
erDiagram
  ORGANIZATION ||--o{ MEMBERSHIP : has
  USER ||--o{ MEMBERSHIP : joins
  ORGANIZATION ||--o{ ROLE : defines
  ROLE ||--o{ ROLE_PERMISSION : grants
  PERMISSION ||--o{ ROLE_PERMISSION : included_in
  MEMBERSHIP ||--o{ MEMBERSHIP_ROLE : receives
  ROLE ||--o{ MEMBERSHIP_ROLE : assigned
  USER ||--o{ REFRESH_TOKEN : owns
  ORGANIZATION ||--o{ OUTBOX_MESSAGE : scopes
  ORGANIZATION ||--o{ AUDIT_ENTRY : scopes

  ORGANIZATION {
    uuid id PK
    string name
    string status
    datetime created_at
  }
  USER {
    uuid id PK
    string email
    string status
    datetime created_at
  }
  MEMBERSHIP {
    uuid id PK
    uuid organization_id
    uuid user_id
    string status
  }
  ROLE {
    uuid id PK
    uuid organization_id
    string name
  }
  PERMISSION {
    string key PK
    string description
  }
  ROLE_PERMISSION {
    uuid role_id
    string permission_key
  }
  MEMBERSHIP_ROLE {
    uuid membership_id
    uuid role_id
  }
  REFRESH_TOKEN {
    uuid id PK
    uuid user_id
    string token_hash
    uuid family_id
    datetime expires_at
  }
  OUTBOX_MESSAGE {
    uuid id PK
    uuid organization_id
    string event_type
    datetime occurred_at
    datetime processed_at
  }
  AUDIT_ENTRY {
    uuid id PK
    uuid organization_id
    uuid actor_user_id
    string action
    datetime occurred_at
  }
```

The diagram is a conceptual foundation, not a substitute for migrations. Identity provider tables may use provider-required shapes while preserving these ownership and security rules.

## Catalog model

Tables are prefixed `catalog_` and organization-filtered.

```mermaid
erDiagram
  PRODUCT ||--o{ PRODUCT_VARIANT : has
  PRODUCT ||--o{ PRODUCT_IMAGE : has
  PRODUCT }o--o| TEAM : classified_by
  PRODUCT }o--o| SEASON : belongs_to
  PRODUCT }o--o{ CATEGORY : in
  PRODUCT }o--o{ TAG : tagged
  PRODUCT_VARIANT ||--|| INVENTORY_LEVEL : tracks
```

Unique constraints include `(organization_id, slug)` on products and taxonomy, and `(organization_id, sku)` on variants. Inventory concurrency uses a SQL Server `rowversion` token.

## Import model

Tables are prefixed `import_` and organization-filtered.

```mermaid
erDiagram
  SUPPLIER ||--o{ IMPORT_BATCH : provides
  IMPORT_BATCH ||--o{ IMPORT_ITEM : contains
```

Batches track parse/review lifecycle; items store proposed fields, match hints, and applied product/variant links after approve.

## Publishing model

Tables are prefixed `publish_` and organization-filtered.

```mermaid
erDiagram
  SALES_CHANNEL ||--o{ EXTERNAL_ID_MAP : maps
  SALES_CHANNEL ||--o{ PUBLISH_RUN : tracks
```

`ExternalIdMap` keys on `(organization_id, channel_id, entity_type, local_id)`. `PublishRun` tracks the latest attempt per product/channel (`Pending` | `Succeeded` | `Failed`).
