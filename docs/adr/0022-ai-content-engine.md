# 0022 — AI content engine

## Status

Accepted

## Context

Catalog copy (titles, descriptions, SEO, image alt text) is still operator-typed. Import review can propose product identity but not generated storefront copy. The product mission includes an AI content engine with operator approve-before-apply — never autonomous publish.

## Decision

- New **AI** module owns prompt templates and generation jobs. Permissions are `ai.read` / `ai.write`.
- Kinds: `title`, `description`, `seo-title`, `seo-description`, `alt-text`. Targets: `product`, `import-item`, `product-image` (alt text requires `product-image`).
- Status spine: Pending → Succeeded/Failed → Approved → Applied, or Rejected. Apply copies onto catalog/import fields only after approve.
- Port `IAiContentGenerator`. Default engine `FixtureAiContentGenerator` via `Ai:Provider=Fixture` for local/CI. Production `OpenAI` uses Chat Completions over HttpClient (`Ai:OpenAI:ApiKey`, model default `gpt-4o-mini`). No extra SDK. API keys stay in config/secret provider; never logged.
- `GenerateAiContentJob` runs on Hangfire queue `ai` (isolated from default/scrape). Seed default templates at bootstrap.
- HTTP under `/api/v1/ai`. Operators generate from product detail and import review; approve is explicit.

## Consequences

Local and CI work without OpenAI keys. Real generations incur token cost recorded on the generation row. Fully autonomous publish and image generation remain out of scope.

## Alternatives considered

- Auto-apply on success: rejected; operators must approve.
- OpenAI SDK package: rejected; HttpClient keeps the dependency surface small.

## Validation

Domain tests cover approve-before-apply. Fixture generator tests cover local/CI copy. Architecture tests still forbid Domain/Application depending on Infrastructure.
