# Security assumptions and threat model

## Assets and trust boundaries

Protect credentials, signing keys, refresh tokens, personal data, organization records, permissions, audit evidence, and backups. Trust boundaries exist at the browser/API, API/database, worker queue, telemetry pipeline, secret provider, and every external integration.

Assume the public network and client are hostile; authenticated users may attempt cross-organization access; dependencies and operators can fail; tokens and backups may be stolen. TLS termination, database access controls, secret storage, patching, and backup durability are platform responsibilities and must be verified per environment.

## Primary threats and controls

- **Credential attack:** ASP.NET Identity hashing, generic login responses, rate limits, lockout policy, MFA-ready design, and authentication audit events.
- **Token theft/replay:** short-lived signed access tokens; hashed, rotating refresh tokens; family reuse detection and revocation; secure transport and browser storage strategy.
- **Broken authorization/tenant escape:** deny-by-default permission policies, trusted organization context, scoped queries and constraints, cross-tenant tests, and audited global administration.
- **Injection and unsafe input:** parameterized persistence, allow-listed parsing, size limits, output encoding, and no dynamic command construction from user input.
- **CSRF/CORS:** exact origin allow-list; if credentials use cookies, require SameSite and anti-forgery protection. Bearer tokens do not remove XSS risk.
- **Sensitive-data disclosure:** data minimization, log redaction, encryption in transit/at rest, restricted telemetry, and tested retention/deletion.
- **Supply-chain compromise:** locked dependencies, vulnerability scanning, provenance review, minimal runtime images, and prompt patching.
- **Job abuse or duplication:** authenticated enqueue paths, bounded payloads, idempotency, retry limits, dead-letter handling, and scoped execution context.
- **Availability attack:** request limits, timeouts, circuit breakers, bounded queues, backpressure, health probes, and capacity alerts.

## Security invariants

Authorization is server-side and checked at use-case boundaries. Resource existence is not disclosed across organizations. Raw tokens, passwords, keys, and sensitive payloads never enter logs. Privileged changes and authentication events are append-only audited. Production keys are externally managed and rotated.

## Verification and response

Threat-model changes accompany new trust boundaries. Security tests cover authorization matrices, tenant isolation, token rotation/reuse, input limits, and redaction. Suspected token compromise follows the [authentication incident runbook](runbooks/auth-incident.md). Residual risks require an owner, expiry, and explicit acceptance.
