using System.Text.Json;
using JerseyOs.Application;
using JerseyOs.Domain;
using JerseyOs.Infrastructure;

namespace JerseyOs.Infrastructure.IntegrationTests;

public sealed class SupplierScrapeCrawlerTests
{
    [Fact]
    public async Task FixtureCrawler_ReturnsJsonRowsHonoringMaxProducts()
    {
        var profile = """
            {
              "list": { "itemLinkSelector": "a.product" },
              "product": { "nameSelector": "h1", "skuSelector": ".sku", "sizeSelector": ".size" },
              "maxProducts": 2
            }
            """;
        var result = await new FixtureSupplierSiteCrawler().CrawlAsync(
            new SupplierScrapeRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "https://example.com/products",
                profile,
                null,
                null),
            CancellationToken.None);

        Assert.Equal(2, result.ProductCount);
        using var document = JsonDocument.Parse(result.JsonUtf8);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(2, document.RootElement.GetArrayLength());
        Assert.Equal("SCRAPE-1-M", document.RootElement[0].GetProperty("sku").GetString());
    }
}
