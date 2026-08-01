using System.Text;
using JerseyOs.Application;

namespace JerseyOs.Application.Tests;

public sealed class CsvSupplierCatalogFeedTests
{
    [Fact]
    public async Task ParsesRequiredColumns()
    {
        var csv = """
                  style_code,name,sku,size,team,season,qty,image_url
                  H25,Home Kit,HOME25-M,M,Arsenal,2025/26,10,https://example.invalid/a.jpg
                  """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var rows = await new CsvSupplierCatalogFeed().ParseAsync(stream, CancellationToken.None);
        var row = Assert.Single(rows);
        Assert.Equal("H25", row.StyleCode);
        Assert.Equal("HOME25-M", row.Sku);
        Assert.Equal(10, row.Quantity);
    }
}
