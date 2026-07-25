# 0007 — OpenAPI-generated TypeScript client

- Status: Accepted
- Date: 2026-07-25

## Context

Hand-maintained browser API types and calls drift from server behavior and make contract changes unsafe.

## Decision

Treat the server OpenAPI document as the HTTP contract and generate the TypeScript client with a pinned generator and reproducible settings. Generated files are not manually edited; client custom behavior wraps the generated surface.

## Consequences

Server and client contracts stay aligned and breaking changes become reviewable. API annotations, stable operation IDs, generator upgrades, deterministic output, and generated-code ergonomics require active maintenance.

## Alternatives considered

Manual clients maximize customization but duplicate contracts. Schema-first OpenAPI is viable but splits implementation and specification ownership. GraphQL is not needed for the accepted API shape.

## Validation

CI regenerates cleanly, type-checks the client, and runs compatibility checks against the accepted baseline.
