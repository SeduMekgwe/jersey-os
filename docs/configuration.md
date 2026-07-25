# Configuration catalog

Configuration is loaded from `appsettings*.json`, environment variables, and an approved secret provider, in increasing precedence. Environment variables use `__` for nesting. Production fails fast on missing or invalid required values.

| Section | Purpose | Secret | Required |
| --- | --- | --- | --- |
| `ConnectionStrings__Primary` | Application database connection | Yes | All |
| `Auth__Issuer` | JWT issuer URI | No | All |
| `Auth__Audience` | JWT audience | No | All |
| `Auth__SigningKey` or key reference | Access-token signing material | Yes | All |
| `Auth__AccessTokenMinutes` | Short access-token lifetime | No | All |
| `Auth__RefreshTokenDays` | Refresh-token lifetime | No | All |
| `Auth__ClockSkewSeconds` | Validation tolerance | No | All |
| `Hangfire__WorkerCount` | Worker concurrency | No | All |
| `Hangfire__Queues` | Ordered queue names | No | All |
| `Outbox__BatchSize` | Dispatch batch bound | No | All |
| `Outbox__PollIntervalSeconds` | Dispatch polling interval | No | All |
| `OpenTelemetry__ServiceName` | Telemetry service identity | No | All |
| `OpenTelemetry__OtlpEndpoint` | OTLP collector endpoint | Sometimes | Hosted |
| `Cors__AllowedOrigins` | Exact trusted web origins | No | Hosted |
| `DataProtection__KeyStore` | Shared encrypted key-ring location | Sensitive | Hosted |

## Rules

- Defaults must be safe for local development and explicit for production.
- Bind to typed options and validate values, ranges, URIs, and cross-field invariants at startup.
- Do not log configuration snapshots or secret values.
- Rotate secrets through the provider; document restart requirements.
- Treat token lifetimes, worker concurrency, and retention as operational changes requiring review.
- Add every new setting here with owner, default behavior, sensitivity, and rollout impact.
