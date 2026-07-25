# Local startup

## Prerequisites

.NET 10 SDK, supported Node.js package tooling, container runtime, and access to approved package feeds. Use only synthetic local data.

## Procedure

1. Copy documented development configuration into user secrets or local environment variables; never create a committed secret file.
2. Start the relational database and any required local telemetry dependencies.
3. Restore .NET and web dependencies.
4. Apply migrations to a disposable local database.
5. Start the API/workers, then the web application.
6. Confirm readiness, OpenAPI availability, successful sign-in, organization selection, and one authorized API request.
7. Confirm logs/traces share a correlation identifier and contain no token values.

Use repository commands once implementation establishes them; this runbook intentionally does not invent command names.

## Troubleshooting

- Startup validation failure: compare the missing option with the [configuration catalog](../configuration.md).
- Database failure: verify container health, connection target, credentials, and migration state.
- `401`: verify issuer, audience, signing material, and clock.
- `403`: verify active membership, organization context, and permission assignment.
- Jobs idle: verify worker process, queue names, storage connectivity, and outbox backlog.

Stop processes and remove disposable data when finished.
