using JerseyOs.Domain;

namespace JerseyOs.Domain.Tests;

public sealed class SupplierScrapeProfileTests
{
    [Fact]
    public void ParseRequiresCoreSelectors()
    {
        Assert.Throws<InvalidOperationException>(() => SupplierScrapeProfile.Parse("{}"));
        var profile = SupplierScrapeProfile.Parse("""
            {
              "list": { "itemLinkSelector": "a.product" },
              "product": {
                "nameSelector": "h1",
                "skuSelector": ".sku",
                "sizeSelector": ".size"
              },
              "maxProducts": 10,
              "navigationDelayMs": 100
            }
            """);
        Assert.Equal("a.product", profile.List.ItemLinkSelector);
        Assert.Equal(10, profile.MaxProducts);
    }

    [Fact]
    public void ConfigureScrapeFeed_SetsKindAndJsonFormat()
    {
        var supplier = new Supplier(Guid.NewGuid(), "Scrape Co", "scrape-co");
        var profile = """
            {
              "list": { "itemLinkSelector": "a.product" },
              "product": { "nameSelector": "h1", "skuSelector": ".sku", "sizeSelector": ".size" }
            }
            """;
        supplier.ConfigureScrapeFeed("https://shop.example/products", profile, "user", "pass", "0 */12 * * *");
        Assert.Equal(SupplierFeedKinds.Scrape, supplier.FeedKind);
        Assert.Equal(SupplierFeedFormats.Json, supplier.FeedFormat);
        Assert.Equal("https://shop.example/products", supplier.FeedUrl);
        Assert.Equal("user", supplier.ScrapeUsername);
        Assert.NotNull(supplier.ScrapeProfileJson);
    }
}
