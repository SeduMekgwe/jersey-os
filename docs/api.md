# API conventions

## HTTP contract

- Base path: `/api/v1`; breaking changes require a new major route.
- Use resource nouns and standard methods. Commands that do not map cleanly use explicit action subresources.
- JSON uses `camelCase`, UTF-8, ISO 8601 UTC timestamps, UUID strings, and explicit nullable fields.
- Collection endpoints use stable cursor pagination, bounded page sizes, and documented filters/sorts.
- Return `201` with `Location` for creation, `202` for accepted asynchronous work, and `204` for successful no-content operations.
- Use RFC 9457 Problem Details with stable `type`, `title`, `status`, `code`, `traceId`, and safe field errors.
- Require `Idempotency-Key` for retryable externally visible commands; persist outcome within organization and operation scope.
- Use ETags or version fields for optimistic concurrency where lost updates matter.

## Security and scope

Bearer access tokens identify the actor. Organization scope comes from a validated route/header claim combination and membership lookup, never from request body ownership fields. Authorization checks permissions and resource scope server-side.

## OpenAPI and TypeScript

OpenAPI is the source of truth for public HTTP contracts. Operations have stable IDs, documented responses, security requirements, and examples. CI generates and type-checks the TypeScript client; generated code is never manually edited. Contract changes require compatibility review and regenerated artifacts.

## Compatibility

Additive fields and endpoints are preferred. Clients must tolerate unknown response fields. Removing or redefining fields, tightening accepted input, or changing status/error semantics is breaking. Deprecations state replacement and removal date.
