# 0004 — ASP.NET Identity, JWT, and rotating refresh tokens

- Status: Accepted
- Date: 2026-07-25

## Context

Interactive clients need secure account management and renewable API sessions without long-lived bearer access.

## Decision

Use ASP.NET Identity for users and credential policy. Issue short-lived signed JWT access tokens. Use opaque refresh tokens stored only as one-way hashes, rotate on every use, link rotations into families, and revoke the family when reuse is detected.

## Consequences

APIs validate locally and access-token exposure is time-bounded. Refresh handling requires atomic rotation, revocation storage, cleanup, secure client storage, key rotation, clock discipline, and incident procedures.

## Alternatives considered

Long-lived JWTs make revocation weak. Persistent unrotated refresh tokens increase replay risk. A custom credential store duplicates mature security behavior.

## Validation

Tests cover rotation races, replay, expiry, revocation, key rollover, issuer/audience checks, and log redaction.
