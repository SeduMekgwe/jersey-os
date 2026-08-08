using System.Text;
using JerseyOs.Application;

namespace JerseyOs.Application.Tests;

public sealed class SupplierCatalogFeedTests
{
    [Fact]
    public async Task JsonFeed_ParsesItemsArray()
    {
        const string json = """
            {
              "items": [
                {
                  "style_code": "H25",
                  "name": "Home Kit",
                  "sku": "HOME-M",
                  "size": "M",
                  "team": "Arsenal",
                  "qty": 3,
                  "price": 1299.5
                }
              ]
            }
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var rows = await new JsonSupplierCatalogFeed().ParseAsync(stream, CancellationToken.None);
        Assert.Single(rows);
        Assert.Equal("HOME-M", rows[0].Sku);
        Assert.Equal(3, rows[0].Quantity);
        Assert.Equal(1299.5m, rows[0].PriceAmount);
        Assert.Equal("Arsenal", rows[0].Team);
    }

    [Fact]
    public async Task CsvFeed_StillParsesRequiredColumns()
    {
        const string csv = """
            style_code,name,sku,size,qty
            H25,Home Kit,HOME-M,M,2
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var rows = await new CsvSupplierCatalogFeed().ParseAsync(stream, CancellationToken.None);
        Assert.Single(rows);
        Assert.Equal("HOME-M", rows[0].Sku);
        Assert.Equal(2, rows[0].Quantity);
    }
}
