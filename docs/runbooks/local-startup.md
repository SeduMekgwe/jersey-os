# Local startup

## Prerequisites

.NET 10 SDK, Node.js 22+, pnpm via Corepack, Docker Desktop with Compose v2, and access to approved package feeds. Use only synthetic local data.

## One-command stack

```powershell
Copy-Item .env.example .env
# Replace every change-me value in .env, then:
.\scripts\dev.ps1 -Detached
```

This builds and starts SQL Server, Redis, API, Worker, and Web. Optional telemetry:

```powershell
.\scripts\dev.ps1 -WithObservability -Detached
```

## Manual procedure

1. Copy `.env.example` to `.env` and replace every change-me value; never commit `.env`.
2. Restore dependencies: `dotnet restore JerseyOs.slnx` and `corepack pnpm install` in `src/JerseyOs.Web`.
3. Generate API client types after OpenAPI changes: `corepack pnpm generate:api` in `src/JerseyOs.Web`.
4. Start infrastructure with `.\scripts\dev.ps1` or run API/Worker/Web individually against Compose SQL/Redis.
5. Confirm `GET /health/live`, `GET /health/ready`, OpenAPI at `/openapi/v1.json`, successful sign-in, and one authorized API request.
6. Confirm logs/traces share a correlation identifier and contain no token values.

## Troubleshooting

- Startup validation failure: compare the missing option with the [configuration catalog](../configuration.md).
- Database failure: verify container health, connection target, credentials, and migration state.
- `401`: verify issuer, audience, signing material, and clock.
- `403`: verify active membership, organization context, and permission assignment.
- Jobs idle: verify worker process, queue names, storage connectivity, and outbox backlog.
- Integration tests without Docker: set `JERSEYOS_ALLOW_SKIP_DOCKER=true` only on local machines. CI must run with Docker.

Stop processes and remove disposable data when finished.
