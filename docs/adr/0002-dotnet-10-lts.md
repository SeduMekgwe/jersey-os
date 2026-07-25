# 0002 — .NET 10 LTS

- Status: Accepted
- Date: 2026-07-25

## Context

The platform requires a supported, productive server runtime with strong web, identity, data, background processing, and observability ecosystems.

## Decision

Target .NET 10 LTS and its compatible ASP.NET Core and EF Core releases. Pin the SDK for reproducible builds and stay on supported security patches.

## Consequences

The platform receives an LTS support horizon, modern runtime capabilities, and cohesive tooling. Contributors and build agents require the pinned SDK; upgrades need compatibility and regression review.

## Alternatives considered

Older LTS releases shorten the foundation's useful support window. A non-LTS release increases upgrade frequency. Other ecosystems would discard the accepted .NET architecture without a compensating need.

## Validation

Builds verify the pinned SDK and dependency support; maintenance tracks runtime end-of-support and patch status.
