using System.Globalization;
using System.Text.Json;
using JerseyOs.Application;
using JerseyOs.Domain;
using Microsoft.Playwright;

namespace JerseyOs.Infrastructure;

public sealed class PlaywrightSupplierSiteCrawler : ISupplierSiteCrawler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<SupplierScrapeResult> CrawlAsync(
        SupplierScrapeRequest request, CancellationToken cancellationToken)
    {
        var profile = SupplierScrapeProfile.Parse(request.ProfileJson);
        var startUrl = string.IsNullOrWhiteSpace(profile.StartUrl) ? request.StartUrl : profile.StartUrl!;
        if (!Uri.TryCreate(startUrl, UriKind.Absolute, out var startUri)
            || (startUri.Scheme != Uri.UriSchemeHttp && startUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Scrape start URL must be http(s).");
        }

        try
        {
            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            }).ConfigureAwait(false);
            var page = await browser.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout(30_000);

            if (profile.Login is not null
                && !string.IsNullOrWhiteSpace(profile.Login.Url)
                && !string.IsNullOrWhiteSpace(request.Username)
                && !string.IsNullOrWhiteSpace(request.Password))
            {
                await page.GotoAsync(profile.Login.Url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(profile.Login.UsernameSelector))
                {
                    await page.FillAsync(profile.Login.UsernameSelector, request.Username).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(profile.Login.PasswordSelector))
                {
                    await page.FillAsync(profile.Login.PasswordSelector, request.Password).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(profile.Login.SubmitSelector))
                {
                    await page.ClickAsync(profile.Login.SubmitSelector).ConfigureAwait(false);
                    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
                }

                await DelayAsync(profile.NavigationDelayMs, cancellationToken).ConfigureAwait(false);
            }

            await page.GotoAsync(startUri.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
                .ConfigureAwait(false);
            var productUrls = await CollectProductUrlsAsync(page, profile, startUri, cancellationToken)
                .ConfigureAwait(false);
            var rows = new List<Dictionary<string, string?>>();
            foreach (var productUrl in productUrls.Take(profile.MaxProducts))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await page.GotoAsync(productUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
                    .ConfigureAwait(false);
                await DelayAsync(profile.NavigationDelayMs, cancellationToken).ConfigureAwait(false);
                var row = await ExtractProductAsync(page, profile.Product).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(row.GetValueOrDefault("name"))
                    && !string.IsNullOrWhiteSpace(row.GetValueOrDefault("sku"))
                    && !string.IsNullOrWhiteSpace(row.GetValueOrDefault("size")))
                {
                    rows.Add(row);
                }
            }

            var json = JsonSerializer.SerializeToUtf8Bytes(rows, JsonOptions);
            return new SupplierScrapeResult(json, rows.Count);
        }
        catch (PlaywrightException exception)
        {
            throw new InvalidOperationException(
                "Playwright scrape failed. Ensure browsers are installed (`pwsh bin/Debug/net10.0/playwright.ps1 install chromium`). "
                + exception.Message,
                exception);
        }
    }

    private static async Task<List<string>> CollectProductUrlsAsync(
        IPage page,
        SupplierScrapeProfile profile,
        Uri startUri,
        CancellationToken cancellationToken)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var pageIndex = 0; pageIndex < profile.MaxListPages; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hrefs = await page.EvalOnSelectorAllAsync<string[]>(
                    profile.List.ItemLinkSelector,
                    "els => els.map(e => e.href || e.getAttribute('href') || '')")
                .ConfigureAwait(false);
            foreach (var href in hrefs)
            {
                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                if (Uri.TryCreate(startUri, href, out var absolute))
                {
                    urls.Add(absolute.ToString());
                }
            }

            if (urls.Count >= profile.MaxProducts
                || string.IsNullOrWhiteSpace(profile.List.NextPageSelector))
            {
                break;
            }

            var next = page.Locator(profile.List.NextPageSelector);
            if (await next.CountAsync().ConfigureAwait(false) == 0)
            {
                break;
            }

            await next.First.ClickAsync().ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            await DelayAsync(profile.NavigationDelayMs, cancellationToken).ConfigureAwait(false);
        }

        return urls.Take(profile.MaxProducts).ToList();
    }

    private static async Task<Dictionary<string, string?>> ExtractProductAsync(
        IPage page, SupplierScrapeProductSelectors product)
    {
        async Task<string?> Text(string? selector)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                return null;
            }

            var locator = page.Locator(selector).First;
            if (await locator.CountAsync().ConfigureAwait(false) == 0)
            {
                return null;
            }

            var text = await locator.InnerTextAsync().ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        async Task<string?> Attr(string? selector, string name)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                return null;
            }

            var locator = page.Locator(selector).First;
            if (await locator.CountAsync().ConfigureAwait(false) == 0)
            {
                return null;
            }

            var value = await locator.GetAttributeAsync(name).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        var image = await Attr(product.ImageSelector, "src").ConfigureAwait(false)
                    ?? await Text(product.ImageSelector).ConfigureAwait(false);

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["style_code"] = await Text(product.StyleCodeSelector).ConfigureAwait(false),
            ["name"] = await Text(product.NameSelector).ConfigureAwait(false),
            ["sku"] = await Text(product.SkuSelector).ConfigureAwait(false),
            ["size"] = await Text(product.SizeSelector).ConfigureAwait(false),
            ["team"] = await Text(product.TeamSelector).ConfigureAwait(false),
            ["season"] = await Text(product.SeasonSelector).ConfigureAwait(false),
            ["qty"] = await Text(product.QuantitySelector).ConfigureAwait(false),
            ["price"] = await Text(product.PriceSelector).ConfigureAwait(false),
            ["image_url"] = image
        };
    }

    private static async Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        if (milliseconds <= 0)
        {
            return;
        }

        await Task.Delay(milliseconds, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Deterministic crawler for tests/local demos that honors profile limits without a browser.
/// </summary>
public sealed class FixtureSupplierSiteCrawler : ISupplierSiteCrawler
{
    public Task<SupplierScrapeResult> CrawlAsync(
        SupplierScrapeRequest request, CancellationToken cancellationToken)
    {
        var profile = SupplierScrapeProfile.Parse(request.ProfileJson);
        var count = Math.Min(profile.MaxProducts, 2);
        var rows = Enumerable.Range(1, count).Select(i => new Dictionary<string, string?>
        {
            ["style_code"] = $"SCRAPE-{i}",
            ["name"] = $"Scraped Kit {i}",
            ["sku"] = $"SCRAPE-{i}-M",
            ["size"] = "M",
            ["team"] = "Demo FC",
            ["season"] = "2025-26",
            ["qty"] = (10 + i).ToString(CultureInfo.InvariantCulture),
            ["price"] = "999.00",
            ["image_url"] = null
        }).ToArray();
        var json = JsonSerializer.SerializeToUtf8Bytes(rows);
        return Task.FromResult(new SupplierScrapeResult(json, rows.Length));
    }
}
