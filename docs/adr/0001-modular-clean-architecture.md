# 0001 — Modular Clean Architecture

- Status: Accepted
- Date: 2026-07-25

## Context

The foundation needs rapid delivery without coupling business capabilities to frameworks or prematurely operating distributed services.

## Decision

Build a modular monolith using Clean Architecture. Each business module owns Domain, Application, Infrastructure, and published contracts; API is the composition edge. Dependencies point inward, and modules integrate only through contracts or integration events.

## Consequences

One deployable and database simplify operations while boundaries support independent reasoning and later extraction. The approach adds projects, mapping, architecture tests, and discipline against shared-model or direct-table shortcuts.

## Alternatives considered

Microservices add unjustified distributed consistency and operational cost. A layered monolith without module ownership makes capability coupling likely.

## Validation

Architecture tests enforce dependency rules; reviews reject cross-module persistence access.
