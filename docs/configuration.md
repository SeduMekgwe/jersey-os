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
| `ObjectStorage__Provider` | Storage adapter (`Local` \| `AzureBlob`; default `Local`) | No | All |
| `ObjectStorage__LocalRootPath` | Local filesystem root for media | No | Local |
| `ObjectStorage__PublicBasePath` | Relative public URL prefix for media (`/media`) | No | Local |
| `ObjectStorage__PublicBaseUrl` | Optional absolute public base (e.g. `https://api.example.com/media`); Local uses it for absolute `GetUrl`; Azure falls back to it when Azure public base is empty | No | Hosted |
| `ObjectStorage__AzureBlob__ConnectionString` | Azure Storage connection string | Yes | When `Provider=AzureBlob` |
| `ObjectStorage__AzureBlob__ContainerName` | Blob container name (created if missing) | No | When `Provider=AzureBlob` |
| `ObjectStorage__AzureBlob__PublicBaseUrl` | Optional override for absolute blob/CDN base URL | No | When `Provider=AzureBlob` |
| `Database__ConnectionString` | SQL Server connection | Yes | All |
| `Database__DefaultOrganizationId` | Default org for single-tenant-ready mode | No | All |
| `Jwt__SigningKey` | HMAC signing material (min 32 bytes) | Yes | All |
| `Shopify__ShopDomain` | Shopify shop hostname (e.g. `demo.myshopify.com`) | No | Hosted when publishing |
| `Shopify__AccessToken` | Admin API access token; empty selects null publisher | Yes | Hosted when publishing |
| `Shopify__ApiVersion` | Admin GraphQL API version | No | Hosted when publishing |
| `Shopify__WebhookSecret` | HMAC secret for inbound order webhooks | Yes | Hosted when publishing |
| `WooCommerce__StoreBaseUrl` | WooCommerce store base URL (e.g. `https://shop.example`) | No | Hosted when publishing to Woo |
| `WooCommerce__ConsumerKey` | WooCommerce REST consumer key | Yes | When Woo channel enabled |
| `WooCommerce__ConsumerSecret` | WooCommerce REST consumer secret | Yes | When Woo channel enabled |
| `WooCommerce__ApiVersion` | REST API version path segment (default `v3`) | No | When publishing to Woo |
| `Import__Scrape__Engine` | Scrape engine (`Fixture` local/CI default, or `Playwright`) | No | When using scrape suppliers |
| `Ai__Provider` | AI engine (`Fixture` local/CI default, or `OpenAI`) | No | When using AI content |
| `Ai__OpenAI__ApiKey` | OpenAI API key; empty is valid for Fixture | Yes | When `Provider=OpenAI` |
| `Ai__OpenAI__Model` | Chat Completions model (default `gpt-4o-mini`) | No | When `Provider=OpenAI` |
| `Ai__OpenAI__BaseUrl` | Chat Completions base URL | No | When `Provider=OpenAI` |
| `Notifications__Provider` | Notification channel (`Fixture` local/CI default, `Webhook`, or `Email`) | No | When using notifications |
| `Notifications__Webhook__Url` | HTTPS endpoint that receives JSON alerts | Sometimes | When `Provider=Webhook` |
| `Notifications__Email__SmtpHost` | SMTP host | Yes | When `Provider=Email` |
| `Notifications__Email__SmtpPort` | SMTP port (default 587) | No | When `Provider=Email` |
| `Notifications__Email__From` | From address | Yes | When `Provider=Email` |
| `Notifications__Email__To` | Operator inbox | Yes | When `Provider=Email` |
| `Notifications__Email__Username` | SMTP username | Yes | When SMTP auth required |
| `Notifications__Email__Password` | SMTP password | Yes | When SMTP auth required |

## Rules

- Defaults must be safe for local development and explicit for production.
- Bind to typed options and validate values, ranges, URIs, and cross-field invariants at startup.
- Do not log configuration snapshots or secret values.
- Rotate secrets through the provider; document restart requirements.
- Treat token lifetimes, worker concurrency, and retention as operational changes requiring review.
- Add every new setting here with owner, default behavior, sensitivity, and rollout impact.
