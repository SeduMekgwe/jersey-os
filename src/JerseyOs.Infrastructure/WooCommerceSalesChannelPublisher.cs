using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JerseyOs.Application;
using JerseyOs.Domain;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure;

public sealed class WooCommerceOptions
{
    public const string Section = "WooCommerce";
    public string StoreBaseUrl { get; set; } = string.Empty;
    public string ConsumerKey { get; set; } = string.Empty;
    public string ConsumerSecret { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "v3";
}

public sealed class WooCommerceSalesChannelPublisher(
    IHttpClientFactory httpClientFactory,
    IOptions<WooCommerceOptions> options) : ISalesChannelPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<PublishProductResult> UpsertProductAsync(
        PublishProductInput input, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var sizes = input.Variants.Select(v => v.Size).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var productBody = new Dictionary<string, object?>
        {
            ["name"] = input.Title,
            ["type"] = "variable",
            ["status"] = "publish",
            ["slug"] = string.IsNullOrWhiteSpace(input.Handle) ? null : input.Handle,
            ["description"] = BuildDescription(input),
            ["short_description"] = string.IsNullOrWhiteSpace(input.SeoDescription) ? null : input.SeoDescription,
            ["sku"] = string.IsNullOrWhiteSpace(input.StyleCode) ? null : input.StyleCode,
            ["images"] = input.ImageUrls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => new Dictionary<string, string> { ["src"] = u })
                .ToArray(),
            ["categories"] = BuildCategories(input),
            ["attributes"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["name"] = "Size",
                    ["slug"] = "pa_size",
                    ["visible"] = true,
                    ["variation"] = true,
                    ["options"] = sizes
                }
            },
            ["meta_data"] = BuildMeta(input)
        };

        JsonElement product;
        if (string.IsNullOrWhiteSpace(input.ExternalProductId))
        {
            product = await SendAsync(HttpMethod.Post, "products", productBody, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            product = await SendAsync(
                    HttpMethod.Put, $"products/{input.ExternalProductId}", productBody, cancellationToken)
                .ConfigureAwait(false);
        }

        var productId = product.GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture);
        var variantExternalIds = new Dictionary<Guid, string>();

        foreach (var variant in input.Variants)
        {
            var price = variant.PriceAmount ?? throw new InvalidOperationException(
                $"Variant {variant.Sku} is missing a price for WooCommerce publish.");
            var compareAt = variant.CompareAtAmount;
            var regular = compareAt is > 0 && compareAt > price ? compareAt.Value : price;
            var variationBody = new Dictionary<string, object?>
            {
                ["sku"] = variant.Sku,
                ["regular_price"] = regular.ToString("0.00", CultureInfo.InvariantCulture),
                ["manage_stock"] = true,
                ["stock_quantity"] = variant.AvailableQuantity,
                ["attributes"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["name"] = "Size",
                        ["option"] = variant.Size
                    }
                }
            };
            if (compareAt is > 0 && compareAt > price)
            {
                variationBody["sale_price"] = price.ToString("0.00", CultureInfo.InvariantCulture);
            }

            JsonElement variation;
            if (TryParseCompositeVariantId(variant.ExternalVariantId, out var existingProductId, out var existingVariationId)
                && existingProductId == productId)
            {
                variation = await SendAsync(
                        HttpMethod.Put,
                        $"products/{productId}/variations/{existingVariationId}",
                        variationBody,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(variant.ExternalVariantId)
                     && long.TryParse(variant.ExternalVariantId, out var legacyVariationId))
            {
                variation = await SendAsync(
                        HttpMethod.Put,
                        $"products/{productId}/variations/{legacyVariationId}",
                        variationBody,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                variation = await SendAsync(
                        HttpMethod.Post,
                        $"products/{productId}/variations",
                        variationBody,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var variationId = variation.GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture);
            variantExternalIds[variant.VariantId] = ComposeVariantId(productId, variationId);
        }

        return new PublishProductResult(productId, variantExternalIds);
    }

    public async Task<string> UpsertCollectionAsync(
        PublishCollectionInput input, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var body = new Dictionary<string, object?>
        {
            ["name"] = input.Title,
            ["slug"] = input.Handle,
            ["description"] = input.Description
        };
        JsonElement category;
        if (!string.IsNullOrWhiteSpace(input.ExternalCollectionId))
        {
            category = await SendAsync(
                    HttpMethod.Put,
                    $"products/categories/{input.ExternalCollectionId}",
                    body,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            category = await SendAsync(HttpMethod.Post, "products/categories", body, cancellationToken)
                .ConfigureAwait(false);
        }

        return category.GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture);
    }

    public async Task UnpublishProductAsync(string externalProductId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProductId);
        await SendAsync(
                HttpMethod.Put,
                $"products/{externalProductId}",
                new Dictionary<string, object?> { ["status"] = "draft" },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetInventoryAsync(
        string externalVariantId, int availableQuantity, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (!TryParseCompositeVariantId(externalVariantId, out var productId, out var variationId))
        {
            throw new InvalidOperationException(
                "WooCommerce variant external id must be '{productId}:{variationId}'.");
        }

        await SendAsync(
                HttpMethod.Put,
                $"products/{productId}/variations/{variationId}",
                new Dictionary<string, object?>
                {
                    ["manage_stock"] = true,
                    ["stock_quantity"] = availableQuantity
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static string ComposeVariantId(string productId, string variationId) =>
        $"{productId}:{variationId}";

    internal static bool TryParseCompositeVariantId(
        string? externalVariantId, out string productId, out string variationId)
    {
        productId = string.Empty;
        variationId = string.Empty;
        if (string.IsNullOrWhiteSpace(externalVariantId))
        {
            return false;
        }

        var parts = externalVariantId.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        productId = parts[0];
        variationId = parts[1];
        return true;
    }

    private void EnsureConfigured()
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.StoreBaseUrl)
            || string.IsNullOrWhiteSpace(opts.ConsumerKey)
            || string.IsNullOrWhiteSpace(opts.ConsumerSecret))
        {
            throw new InvalidOperationException("WooCommerce credentials are not configured.");
        }
    }

    private async Task<JsonElement> SendAsync(
        HttpMethod method, string relativePath, object? body, CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var baseUrl = opts.StoreBaseUrl.TrimEnd('/');
        var version = string.IsNullOrWhiteSpace(opts.ApiVersion) ? "v3" : opts.ApiVersion.Trim().Trim('/');
        var url = $"{baseUrl}/wp-json/wc/{version}/{relativePath.TrimStart('/')}";

        using var request = new HttpRequestMessage(method, url);
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{opts.ConsumerKey}:{opts.ConsumerSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }

        var client = httpClientFactory.CreateClient("woocommerce");
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"WooCommerce HTTP {(int)response.StatusCode}: {Truncate(json)}");
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    private static object[] BuildMeta(PublishProductInput input)
    {
        var meta = new List<Dictionary<string, string>>();
        if (!string.IsNullOrWhiteSpace(input.StyleCode))
        {
            meta.Add(new Dictionary<string, string> { ["key"] = "style_code", ["value"] = input.StyleCode });
        }

        if (!string.IsNullOrWhiteSpace(input.TeamName))
        {
            meta.Add(new Dictionary<string, string> { ["key"] = "team", ["value"] = input.TeamName });
        }

        if (!string.IsNullOrWhiteSpace(input.SeasonName))
        {
            meta.Add(new Dictionary<string, string> { ["key"] = "season", ["value"] = input.SeasonName });
        }

        if (!string.IsNullOrWhiteSpace(input.SeoTitle))
        {
            meta.Add(new Dictionary<string, string> { ["key"] = "_yoast_wpseo_title", ["value"] = input.SeoTitle });
        }

        if (!string.IsNullOrWhiteSpace(input.SeoDescription))
        {
            meta.Add(new Dictionary<string, string>
            {
                ["key"] = "_yoast_wpseo_metadesc",
                ["value"] = input.SeoDescription
            });
        }

        return meta.ToArray<object>();
    }

    private static object[]? BuildCategories(PublishProductInput input)
    {
        var ids = (input.CollectionExternalIds ?? [])
            .Where(id => long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Select(id => new Dictionary<string, object?>
            {
                ["id"] = long.Parse(id, CultureInfo.InvariantCulture)
            })
            .ToArray();
        return ids.Length == 0 ? null : ids;
    }

    private static string BuildDescription(PublishProductInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.SeoDescription))
        {
            return input.SeoDescription;
        }
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(input.TeamName))
        {
            parts.Add(input.TeamName);
        }

        if (!string.IsNullOrWhiteSpace(input.SeasonName))
        {
            parts.Add(input.SeasonName);
        }

        if (!string.IsNullOrWhiteSpace(input.StyleCode))
        {
            parts.Add($"Style {input.StyleCode}");
        }

        return parts.Count == 0 ? input.Title : string.Join(" · ", parts);
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500];
}

public sealed class SalesChannelPublisherResolver(
    NullSalesChannelPublisher nullPublisher,
    IHttpClientFactory httpClientFactory,
    IOrganizationIntegrationSettings integrationSettings) : ISalesChannelPublisherResolver
{
    public ISalesChannelPublisher? Resolve(string channelCode, Guid organizationId)
    {
        if (string.Equals(channelCode, SalesChannelCodes.Shopify, StringComparison.OrdinalIgnoreCase))
        {
            var shopify = integrationSettings.ResolveShopify(organizationId);
            if (string.IsNullOrWhiteSpace(shopify.AccessToken))
            {
                return nullPublisher;
            }

            return new ShopifySalesChannelPublisher(
                httpClientFactory,
                Options.Create(
                    new ShopifyOptions
                    {
                        ShopDomain = shopify.ShopDomain,
                        AccessToken = shopify.AccessToken,
                        ApiVersion = shopify.ApiVersion,
                        WebhookSecret = shopify.WebhookSecret
                    }));
        }

        if (string.Equals(channelCode, SalesChannelCodes.WooCommerce, StringComparison.OrdinalIgnoreCase))
        {
            var woo = integrationSettings.ResolveWoo(organizationId);
            if (string.IsNullOrWhiteSpace(woo.StoreBaseUrl)
                || string.IsNullOrWhiteSpace(woo.ConsumerKey)
                || string.IsNullOrWhiteSpace(woo.ConsumerSecret))
            {
                return null;
            }

            return new WooCommerceSalesChannelPublisher(
                httpClientFactory,
                Options.Create(
                    new WooCommerceOptions
                    {
                        StoreBaseUrl = woo.StoreBaseUrl,
                        ConsumerKey = woo.ConsumerKey,
                        ConsumerSecret = woo.ConsumerSecret,
                        ApiVersion = woo.ApiVersion
                    }));
        }

        return null;
    }
}
