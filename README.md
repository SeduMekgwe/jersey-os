# Jersey OS

Jersey OS is the operational platform for Jersey No.10 Collective. The foundation is a modular .NET 10 application with explicit organization boundaries, secure identity, reliable background work, and generated client contracts.

## Status

Platform foundation and product catalog core are in place. Architectural decisions and operating expectations are authoritative in [`docs/`](docs/README.md).

## Principles

- Keep business modules independent and organization-scoped.
- Put domain policy inside modules; integrate through contracts and events.
- Treat API, database, security, and observability behavior as versioned contracts.
- Prefer reversible decisions and operationally safe defaults.

## Getting started

See the [local startup runbook](docs/runbooks/local-startup.md). No production secret belongs in the repository.

## Governance

Read [CONTRIBUTING.md](CONTRIBUTING.md) before proposing changes. This repository is proprietary; see [LICENSE](LICENSE).
