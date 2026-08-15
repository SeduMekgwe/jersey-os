using System.Net;
using System.Text;
using JerseyOs.Application;
using JerseyOs.Domain;
using JerseyOs.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure.IntegrationTests;

public sealed class WooCommerceSalesChannelPublisherTests
{
    [Fact]
    public async Task UpsertProductCreatesVariableProductAndVariations()
    {
        var calls = new List<(HttpMethod Method, string Url, string Body)>();
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            calls.Add((request.Method, request.RequestUri!.ToString(), body));

            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/products", StringComparison.Ordinal))
            {
                return JsonOk("""{ "id": 10, "type": "variable" }""");
            }

            if (request.Method == HttpMethod.Post
                && request.RequestUri!.AbsolutePath.Contains("/variations", StringComparison.Ordinal))
            {
                return JsonOk("""{ "id": 22, "sku": "HOME-M" }""");
            }

            return JsonOk("{}");
        });

        var publisher = CreateWooPublisher(handler);
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
                [new PublishVariantInput(variantId, "HOME-M", "M", 3, 1299.00m, null, "ZAR", null)],
                null,
                "home-kit",
                "Home Kit SEO",
                "Official home jersey."),
            CancellationToken.None);

        Assert.Equal("10", result.ExternalProductId);
        Assert.Equal("10:22", result.VariantExternalIds[variantId]);
        Assert.Contains(calls, c => c.Method == HttpMethod.Post && c.Url.Contains("/products", StringComparison.Ordinal));
        Assert.Contains(calls, c => c.Body.Contains("variable", StringComparison.Ordinal));
        Assert.Contains(calls, c => c.Body.Contains("HOME-M", StringComparison.Ordinal));
        Assert.Contains(calls, c => c.Body.Contains("1299.00", StringComparison.Ordinal));
        Assert.Contains(calls, c => c.Body.Contains("home-kit", StringComparison.Ordinal));
        Assert.Contains(calls, c => c.Body.Contains("Home Kit SEO", StringComparison.Ordinal));
        Assert.Contains(
            calls,
            c => c.Method == HttpMethod.Post && c.Url.Contains("/products/10/variations", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetInventoryUpdatesVariationStock()
    {
        string? url = null;
        string? body = null;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            url = request.RequestUri!.ToString();
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonOk("""{ "id": 22 }""");
        });

        await CreateWooPublisher(handler).SetInventoryAsync("10:22", 7, CancellationToken.None);
        Assert.Contains("/products/10/variations/22", url, StringComparison.Ordinal);
        Assert.Contains("\"stock_quantity\":7", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnpublishSetsDraftStatus()
    {
        string? body = null;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonOk("""{ "id": 10, "status": "draft" }""");
        });

        await CreateWooPublisher(handler).UnpublishProductAsync("10", CancellationToken.None);
        Assert.Contains("\"status\":\"draft\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolverReturnsNullWhenWooCredentialsMissing()
    {
        var resolver = new SalesChannelPublisherResolver(
            new NullSalesChannelPublisher(),
            CreateShopifyPublisher(new StubHandler((_, _) => Task.FromResult(JsonOk("{}")))),
            CreateWooPublisher(new StubHandler((_, _) => Task.FromResult(JsonOk("{}")))),
            Options.Create(new ShopifyOptions { AccessToken = "token" }),
            Options.Create(new WooCommerceOptions()));

        Assert.Null(resolver.Resolve(SalesChannelCodes.WooCommerce));
        Assert.IsType<ShopifySalesChannelPublisher>(resolver.Resolve(SalesChannelCodes.Shopify));
    }

    [Fact]
    public void ResolverReturnsNullPublisherWhenShopifyTokenMissing()
    {
        var resolver = new SalesChannelPublisherResolver(
            new NullSalesChannelPublisher(),
            CreateShopifyPublisher(new StubHandler((_, _) => Task.FromResult(JsonOk("{}")))),
            CreateWooPublisher(new StubHandler((_, _) => Task.FromResult(JsonOk("{}")))),
            Options.Create(new ShopifyOptions()),
            Options.Create(new WooCommerceOptions
            {
                StoreBaseUrl = "https://shop.example",
                ConsumerKey = "ck",
                ConsumerSecret = "cs"
            }));

        Assert.IsType<NullSalesChannelPublisher>(resolver.Resolve(SalesChannelCodes.Shopify));
        Assert.IsType<WooCommerceSalesChannelPublisher>(resolver.Resolve(SalesChannelCodes.WooCommerce));
    }

    private static WooCommerceSalesChannelPublisher CreateWooPublisher(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("woocommerce").ConfigurePrimaryHttpMessageHandler(() => handler);
        var provider = services.BuildServiceProvider();
        return new WooCommerceSalesChannelPublisher(
            provider.GetRequiredService<IHttpClientFactory>(),
            Options.Create(new WooCommerceOptions
            {
                StoreBaseUrl = "https://shop.example",
                ConsumerKey = "ck_test",
                ConsumerSecret = "cs_test",
                ApiVersion = "v3"
            }));
    }

    private static ShopifySalesChannelPublisher CreateShopifyPublisher(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("shopify").ConfigurePrimaryHttpMessageHandler(() => handler);
        var provider = services.BuildServiceProvider();
        return new ShopifySalesChannelPublisher(
            provider.GetRequiredService<IHttpClientFactory>(),
            Options.Create(new ShopifyOptions
            {
                ShopDomain = "demo.myshopify.com",
                AccessToken = "test-token"
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
