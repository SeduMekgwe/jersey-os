# 0019 — Playwright supplier scrape intake

## Status

Accepted

## Context

CSV upload and HTTP feeds (ADR 0018) do not cover supplier sites that only expose HTML catalogs. The original PIM vision requires browser automation into the same Import review queue, without writing Active catalog state.

## Decision

- Add `SupplierFeedKinds.Scrape` with `ScrapeProfileJson` (CSS selectors), optional login credentials, start URL, and optional Hangfire cron.
- Port `ISupplierSiteCrawler`; production engine `PlaywrightSupplierSiteCrawler`; local/CI default `FixtureSupplierSiteCrawler` via `Import:Scrape:Engine` (`Fixture`|`Playwright`).
- `ScrapeSupplierFeedJob` runs on Hangfire queue `scrape` (isolated from default/Shopify work): crawl → JSON in `IObjectStorage` → `ImportBatch` → existing parse/review/approve.
- Persist `SupplierScrapeRun` attempt logs; list via `GET /import/suppliers/{id}/scrape-runs`.
- Ship a demo selector profile at `docs/fixtures/supplier-scrape/demo-profile.json`. Operators must respect site terms/robots; Jersey OS does not bypass legal/ToS constraints.

## Consequences

Scraped rows share the Import review spine. Real scrapes require Playwright browser install on workers when Engine=Playwright. Site-specific profiles remain operator-owned; no supplier marketplace. FTP and AI enrichment stay later milestones.
