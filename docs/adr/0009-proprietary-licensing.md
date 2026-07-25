# 0009 — Proprietary licensing

- Status: Accepted
- Date: 2026-07-25

## Context

The repository previously contained GPLv3 text, while the accepted ownership model requires Jersey OS to remain proprietary to Jersey No.10 Collective.

## Decision

Replace GPLv3 with an all-rights-reserved proprietary notice naming Jersey No.10 Collective and Jersey OS. No right to use, copy, modify, or distribute is granted without a separate written agreement. Contributions require authorization.

## Consequences

Repository access does not imply a software license. Distribution, external contributions, and third-party dependency licenses require explicit review. The notice is concise and is not a substitute for commercial agreements or legal advice.

## Alternatives considered

GPLv3 and permissive open-source licenses grant redistribution rights inconsistent with the accepted model. Source-available terms are not currently required.

## Validation

The root notice, README, contribution guidance, packaging, and release materials remain consistent; dependency license scanning prevents incompatible obligations.
