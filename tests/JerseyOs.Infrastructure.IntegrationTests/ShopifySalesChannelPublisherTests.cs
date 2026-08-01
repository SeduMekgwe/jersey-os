using System.Net;
using System.Text;
using JerseyOs.Application;
using JerseyOs.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure.IntegrationTests;

public sealed class ShopifySalesChannelPublisherTests
{
    [Fact]
    public async Task UpsertProductPostsProductSetMutation()
    {
        string? requestBody = null;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Equal("test-token", request.Headers.GetValues("X-Shopify-Access-Token").Single());
            return JsonOk("""
                {
                  "data": {
                    "productSet": {
                      "product": {
                        "id": "gid://shopify/Product/1",
                        "variants": {
                          "nodes": [
                            { "id": "gid://shopify/ProductVariant/11", "sku": "HOME-M" }
                          ]
                        }
                      },
                      "userErrors": []
                    }
                  }
                }
                """);
        });

        var publisher = CreatePublisher(handler);
        var variantId = Guid.NewGuid();
        var result = await publisher.UpsertProductAsync(
            new PublishProductInput(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Home Kit",
                "H25",
                "Arsenal",
                "2025-26",
                ["https://cdn.example/kit.jpg"],
                [new PublishVariantInput(variantId, "HOME-M", "M", 3, null)],
                null),
            CancellationToken.None);

        Assert.Equal("gid://shopify/Product/1", result.ExternalProductId);
        Assert.Equal("gid://shopify/ProductVariant/11", result.VariantExternalIds[variantId]);
        Assert.Contains("productSet", requestBody, StringComparison.Ordinal);
        Assert.Contains("HOME-M", requestBody, StringComparison.Ordinal);
        Assert.Contains("style_code", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnpublishSendsDraftStatus()
    {
        string? requestBody = null;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonOk("""
                {
                  "data": {
                    "productUpdate": {
                      "product": { "id": "gid://shopify/Product/1" },
                      "userErrors": []
                    }
                  }
                }
                """);
        });

        await CreatePublisher(handler).UnpublishProductAsync("gid://shopify/Product/1", CancellationToken.None);
        Assert.Contains("DRAFT", requestBody, StringComparison.Ordinal);
        Assert.Contains("productUpdate", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetInventoryResolvesItemAndLocation()
    {
        var calls = new List<string>();
        var handler = new StubHandler((request, _) =>
        {
            var body = request.Content!.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult();
            calls.Add(body);
            if (body.Contains("productVariant", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonOk("""
                    {
                      "data": {
                        "productVariant": {
                          "inventoryItem": { "id": "gid://shopify/InventoryItem/9" }
                        }
                      }
                    }
                    """));
            }

            if (body.Contains("PrimaryLocation", StringComparison.Ordinal) ||
                body.Contains("locations(first: 1)", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonOk("""
                    {
                      "data": {
                        "locations": {
                          "nodes": [ { "id": "gid://shopify/Location/2" } ]
                        }
                      }
                    }
                    """));
            }

            return Task.FromResult(JsonOk("""
                {
                  "data": {
                    "inventorySetQuantities": {
                      "userErrors": []
                    }
                  }
                }
                """));
        });

        await CreatePublisher(handler).SetInventoryAsync("gid://shopify/ProductVariant/11", 7, CancellationToken.None);
        Assert.Equal(3, calls.Count);
        Assert.Contains(calls, c => c.Contains("inventorySetQuantities", StringComparison.Ordinal));
        Assert.Contains("\"quantity\":7", calls[^1], StringComparison.Ordinal);
    }

    private static ShopifySalesChannelPublisher CreatePublisher(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("shopify").ConfigurePrimaryHttpMessageHandler(() => handler);
        var provider = services.BuildServiceProvider();
        return new ShopifySalesChannelPublisher(
            provider.GetRequiredService<IHttpClientFactory>(),
            Options.Create(new ShopifyOptions
            {
                ShopDomain = "demo.myshopify.com",
                AccessToken = "test-token",
                ApiVersion = "2025-01"
            }));
    }

    private static HttpResponseMessage JsonOk(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request, cancellationToken);
    }
}
