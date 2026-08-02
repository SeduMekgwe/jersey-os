using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using JerseyOs.Application;
using JerseyOs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure;

public sealed class ShopifyOptions
{
    public const string Section = "Shopify";
    public string ShopDomain { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2025-01";
}

public static class PublishingModelBuilder
{
    public static void ConfigurePublishing(this ModelBuilder builder, Guid effectiveOrganizationId)
    {
        builder.Entity<SalesChannel>(b =>
        {
            b.ToTable("publish_sales_channels");
            b.Property(x => x.Code).HasMaxLength(64).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<ExternalIdMap>(b =>
        {
            b.ToTable("publish_external_ids");
            b.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
            b.Property(x => x.ExternalId).HasMaxLength(256).IsRequired();
            b.HasIndex(x => new { x.OrganizationId, x.ChannelId, x.EntityType, x.LocalId }).IsUnique();
            b.HasIndex(x => new { x.OrganizationId, x.ChannelId, x.ExternalId });
            b.HasOne(x => x.Channel).WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<PublishRun>(b =>
        {
            b.ToTable("publish_runs");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.Error).HasMaxLength(2000);
            b.HasIndex(x => new { x.OrganizationId, x.ChannelId, x.ProductId }).IsUnique();
            b.HasOne(x => x.Channel).WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
    }
}

public sealed class NullSalesChannelPublisher : ISalesChannelPublisher
{
    public Task<PublishProductResult> UpsertProductAsync(PublishProductInput input, CancellationToken cancellationToken)
    {
        var productExternalId = string.IsNullOrWhiteSpace(input.ExternalProductId)
            ? $"gid://shopify/Product/{input.ProductId:N}"
            : input.ExternalProductId;
        var variants = input.Variants.ToDictionary(
            v => v.VariantId,
            v => string.IsNullOrWhiteSpace(v.ExternalVariantId)
                ? $"gid://shopify/ProductVariant/{v.VariantId:N}"
                : v.ExternalVariantId!);
        return Task.FromResult(new PublishProductResult(productExternalId, variants));
    }

    public Task UnpublishProductAsync(string externalProductId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task SetInventoryAsync(string externalVariantId, int availableQuantity, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

public sealed class ShopifySalesChannelPublisher(
    IHttpClientFactory httpClientFactory,
    IOptions<ShopifyOptions> options) : ISalesChannelPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<PublishProductResult> UpsertProductAsync(
        PublishProductInput input, CancellationToken cancellationToken)
    {
        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(input.TeamName)) tags.Add(input.TeamName!);
        if (!string.IsNullOrWhiteSpace(input.SeasonName)) tags.Add(input.SeasonName!);

        var variants = input.Variants.Select(v =>
        {
            var payload = new Dictionary<string, object?>
            {
                ["optionValues"] = new[] { new { optionName = "Size", name = v.Size } },
                ["sku"] = v.Sku,
                ["inventoryItem"] = new { tracked = true },
                ["id"] = string.IsNullOrWhiteSpace(v.ExternalVariantId) ? null : v.ExternalVariantId
            };
            if (v.PriceAmount is { } price)
            {
                payload["price"] = price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            }

            return payload;
        }).ToArray();

        var media = input.ImageUrls
            .Where(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .Select(u => new { originalSource = u, mediaContentType = "IMAGE" })
            .ToArray();

        var productInput = new Dictionary<string, object?>
        {
            ["title"] = input.Title,
            ["status"] = "ACTIVE",
            ["tags"] = tags,
            ["productOptions"] = new[]
            {
                new { name = "Size", values = input.Variants.Select(v => new { name = v.Size }).Distinct().ToArray() }
            },
            ["variants"] = variants,
            ["metafields"] = string.IsNullOrWhiteSpace(input.StyleCode)
                ? null
                : new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["namespace"] = "jerseyos",
                        ["key"] = "style_code",
                        ["type"] = "single_line_text_field",
                        ["value"] = input.StyleCode
                    }
                }
        };
        if (!string.IsNullOrWhiteSpace(input.ExternalProductId))
        {
            productInput["id"] = input.ExternalProductId;
        }

        if (media.Length > 0)
        {
            productInput["files"] = media;
        }

        const string mutation = """
            mutation ProductSet($input: ProductSetInput!) {
              productSet(synchronous: true, input: $input) {
                product {
                  id
                  variants(first: 250) {
                    nodes { id sku }
                  }
                }
                userErrors { field message }
              }
            }
            """;

        var data = await SendGraphqlAsync(mutation, new { input = productInput }, cancellationToken)
            .ConfigureAwait(false);
        var productSet = data.GetProperty("productSet");
        ThrowIfUserErrors(productSet);
        var product = productSet.GetProperty("product");
        var externalProductId = product.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Shopify productSet returned no product id.");
        var variantNodes = product.GetProperty("variants").GetProperty("nodes");
        var bySku = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in variantNodes.EnumerateArray())
        {
            var sku = node.GetProperty("sku").GetString();
            var id = node.GetProperty("id").GetString();
            if (!string.IsNullOrWhiteSpace(sku) && !string.IsNullOrWhiteSpace(id))
            {
                bySku[sku] = id;
            }
        }

        var mapped = new Dictionary<Guid, string>();
        foreach (var variant in input.Variants)
        {
            if (!bySku.TryGetValue(variant.Sku, out var externalVariantId))
            {
                throw new InvalidOperationException($"Shopify did not return a variant for SKU {variant.Sku}.");
            }

            mapped[variant.VariantId] = externalVariantId;
        }

        return new PublishProductResult(externalProductId, mapped);
    }

    public async Task UnpublishProductAsync(string externalProductId, CancellationToken cancellationToken)
    {
        const string mutation = """
            mutation ProductUpdate($input: ProductInput!) {
              productUpdate(input: $input) {
                product { id }
                userErrors { field message }
              }
            }
            """;
        var data = await SendGraphqlAsync(
                mutation,
                new { input = new { id = externalProductId, status = "DRAFT" } },
                cancellationToken)
            .ConfigureAwait(false);
        ThrowIfUserErrors(data.GetProperty("productUpdate"));
    }

    public async Task SetInventoryAsync(
        string externalVariantId, int availableQuantity, CancellationToken cancellationToken)
    {
        const string query = """
            query VariantInventory($id: ID!) {
              productVariant(id: $id) {
                inventoryItem { id }
              }
            }
            """;
        var variantData = await SendGraphqlAsync(query, new { id = externalVariantId }, cancellationToken)
            .ConfigureAwait(false);
        var inventoryItemId = variantData
            .GetProperty("productVariant")
            .GetProperty("inventoryItem")
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException("Shopify variant has no inventory item.");

        const string locationQuery = """
            query PrimaryLocation {
              locations(first: 1) {
                nodes { id }
              }
            }
            """;
        var locationData = await SendGraphqlAsync(locationQuery, new { }, cancellationToken).ConfigureAwait(false);
        var locationNodes = locationData.GetProperty("locations").GetProperty("nodes").EnumerateArray().ToArray();
        if (locationNodes.Length == 0)
        {
            throw new InvalidOperationException("Shopify shop has no location.");
        }

        var locationId = locationNodes[0].GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Shopify shop has no location.");

        const string mutation = """
            mutation SetInventory($input: InventorySetQuantitiesInput!) {
              inventorySetQuantities(input: $input) {
                userErrors { field message }
              }
            }
            """;
        var setData = await SendGraphqlAsync(
                mutation,
                new
                {
                    input = new
                    {
                        name = "available",
                        reason = "correction",
                        ignoreCompareQuantity = true,
                        quantities = new[]
                        {
                            new
                            {
                                inventoryItemId,
                                locationId,
                                quantity = availableQuantity
                            }
                        }
                    }
                },
                cancellationToken)
            .ConfigureAwait(false);
        ThrowIfUserErrors(setData.GetProperty("inventorySetQuantities"));
    }

    private async Task<JsonElement> SendGraphqlAsync(
        string query, object variables, CancellationToken cancellationToken)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ShopDomain) || string.IsNullOrWhiteSpace(opts.AccessToken))
        {
            throw new InvalidOperationException("Shopify credentials are not configured.");
        }

        var client = httpClientFactory.CreateClient("shopify");
        var domain = opts.ShopDomain.Trim().TrimEnd('/');
        if (!domain.Contains('.', StringComparison.Ordinal))
        {
            domain = $"{domain}.myshopify.com";
        }

        var uri = $"https://{domain}/admin/api/{opts.ApiVersion.Trim()}/graphql.json";
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("X-Shopify-Access-Token", opts.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var body = JsonSerializer.Serialize(new { query, variables }, JsonOptions);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Shopify GraphQL HTTP {(int)response.StatusCode}: {json}");
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0)
        {
            throw new InvalidOperationException($"Shopify GraphQL errors: {errors}");
        }

        return document.RootElement.GetProperty("data").Clone();
    }

    private static void ThrowIfUserErrors(JsonElement container)
    {
        if (!container.TryGetProperty("userErrors", out var userErrors)
            || userErrors.ValueKind != JsonValueKind.Array
            || userErrors.GetArrayLength() == 0)
        {
            return;
        }

        var messages = userErrors.EnumerateArray()
            .Select(e => e.GetProperty("message").GetString())
            .Where(m => !string.IsNullOrWhiteSpace(m));
        throw new InvalidOperationException($"Shopify userErrors: {string.Join("; ", messages)}");
    }
}

public sealed class HangfirePublishingJobScheduler(IBackgroundJobClient jobs) : IPublishingJobScheduler
{
    public void EnqueuePublishProduct(Guid organizationId, Guid productId, bool unpublish) =>
        jobs.Enqueue<PublishProductJob>(job =>
            job.ExecuteAsync(organizationId, productId, unpublish, CancellationToken.None));

    public void EnqueueSyncInventory(Guid organizationId, Guid variantId) =>
        jobs.Enqueue<SyncInventoryJob>(job =>
            job.ExecuteAsync(organizationId, variantId, CancellationToken.None));
}

public sealed class PublishProductJob(
    JerseyOsDbContext db,
    ISalesChannelPublisher publisher,
    IObjectStorage storage,
    TimeProvider time)
{
    [AutomaticRetry(Attempts = 5)]
    public async Task ExecuteAsync(
        Guid organizationId, Guid productId, bool unpublish, CancellationToken cancellationToken)
    {
        var channel = await db.SalesChannelsSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId
                     && x.Code == SalesChannelCodes.Shopify
                     && x.Enabled,
                cancellationToken)
            .ConfigureAwait(false);
        if (channel is null)
        {
            return;
        }

        var product = await db.ProductsSet.IgnoreQueryFilters()
            .Include(x => x.Variants).ThenInclude(x => x.Inventory)
            .Include(x => x.Images)
            .Include(x => x.Team)
            .Include(x => x.Season)
            .SingleOrDefaultAsync(x => x.Id == productId && x.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (product is null || product.Status == ProductStatus.Draft)
        {
            return;
        }

        var run = await EnsureRunAsync(organizationId, channel.Id, productId, cancellationToken).ConfigureAwait(false);
        var now = time.GetUtcNow();
        try
        {
            var productMap = await GetMapAsync(
                    organizationId, channel.Id, PublishEntityTypes.Product, productId, cancellationToken)
                .ConfigureAwait(false);

            if (unpublish || product.Status == ProductStatus.Archived)
            {
                if (productMap is not null)
                {
                    await publisher.UnpublishProductAsync(productMap.ExternalId, cancellationToken)
                        .ConfigureAwait(false);
                }

                run.MarkSucceeded(now);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (product.Status != ProductStatus.Active)
            {
                return;
            }

            var variantMaps = await db.ExternalIdMapsSet.IgnoreQueryFilters()
                .Where(x => x.OrganizationId == organizationId
                            && x.ChannelId == channel.Id
                            && x.EntityType == PublishEntityTypes.Variant
                            && product.Variants.Select(v => v.Id).Contains(x.LocalId))
                .ToDictionaryAsync(x => x.LocalId, cancellationToken)
                .ConfigureAwait(false);

            var currency = await db.OrganizationsSet.IgnoreQueryFilters()
                .Where(x => x.Id == organizationId)
                .Select(x => x.DefaultCurrency)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(currency))
            {
                currency = "ZAR";
            }

            if (product.Variants.Any(v => v.PriceAmount is null or <= 0))
            {
                throw new InvalidOperationException("Active products require a price on every variant before publish.");
            }

            var input = new PublishProductInput(
                product.Id,
                product.OrganizationId,
                product.Name,
                product.StyleCode,
                product.Team?.Name,
                product.Season?.Name,
                product.Images.OrderBy(i => i.SortOrder).Select(i => storage.GetUrl(i.ObjectKey)).ToArray(),
                product.Variants.OrderBy(v => v.SortOrder).Select(v =>
                {
                    variantMaps.TryGetValue(v.Id, out var map);
                    var available = Math.Max(0, v.Inventory.OnHand - v.Inventory.Reserved);
                    return new PublishVariantInput(
                        v.Id,
                        v.Sku,
                        v.Size,
                        available,
                        v.PriceAmount,
                        currency,
                        map?.ExternalId);
                }).ToArray(),
                productMap?.ExternalId);

            var result = await publisher.UpsertProductAsync(input, cancellationToken).ConfigureAwait(false);
            await UpsertMapAsync(
                    organizationId,
                    channel.Id,
                    PublishEntityTypes.Product,
                    product.Id,
                    result.ExternalProductId,
                    cancellationToken)
                .ConfigureAwait(false);
            foreach (var (variantId, externalId) in result.VariantExternalIds)
            {
                await UpsertMapAsync(
                        organizationId,
                        channel.Id,
                        PublishEntityTypes.Variant,
                        variantId,
                        externalId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var variant in product.Variants)
            {
                if (!result.VariantExternalIds.TryGetValue(variant.Id, out var externalVariantId))
                {
                    continue;
                }

                var available = Math.Max(0, variant.Inventory.OnHand - variant.Inventory.Reserved);
                await publisher.SetInventoryAsync(externalVariantId, available, cancellationToken)
                    .ConfigureAwait(false);
            }

            run.MarkSucceeded(now);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            run.MarkFailed(exception.Message, now);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<PublishRun> EnsureRunAsync(
        Guid organizationId, Guid channelId, Guid productId, CancellationToken cancellationToken)
    {
        var run = await db.PublishRunsSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.ChannelId == channelId && x.ProductId == productId,
                cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            run = new PublishRun(organizationId, channelId, productId);
            db.PublishRunsSet.Add(run);
        }
        else
        {
            run.MarkPending();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return run;
    }

    private Task<ExternalIdMap?> GetMapAsync(
        Guid organizationId,
        Guid channelId,
        string entityType,
        Guid localId,
        CancellationToken cancellationToken) =>
        db.ExternalIdMapsSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId
                     && x.ChannelId == channelId
                     && x.EntityType == entityType
                     && x.LocalId == localId,
                cancellationToken);

    private async Task UpsertMapAsync(
        Guid organizationId,
        Guid channelId,
        string entityType,
        Guid localId,
        string externalId,
        CancellationToken cancellationToken)
    {
        var map = await GetMapAsync(organizationId, channelId, entityType, localId, cancellationToken)
            .ConfigureAwait(false);
        if (map is null)
        {
            db.ExternalIdMapsSet.Add(new ExternalIdMap(organizationId, channelId, entityType, localId, externalId));
        }
        else
        {
            map.UpdateExternalId(externalId);
        }
    }
}

public sealed class SyncInventoryJob(
    JerseyOsDbContext db,
    ISalesChannelPublisher publisher)
{
    [AutomaticRetry(Attempts = 5)]
    public async Task ExecuteAsync(Guid organizationId, Guid variantId, CancellationToken cancellationToken)
    {
        var channel = await db.SalesChannelsSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId
                     && x.Code == SalesChannelCodes.Shopify
                     && x.Enabled,
                cancellationToken)
            .ConfigureAwait(false);
        if (channel is null)
        {
            return;
        }

        var variant = await db.ProductVariantsSet.IgnoreQueryFilters()
            .Include(x => x.Inventory)
            .Include(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == variantId && x.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (variant is null || variant.Product.Status != ProductStatus.Active)
        {
            return;
        }

        var map = await db.ExternalIdMapsSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId
                     && x.ChannelId == channel.Id
                     && x.EntityType == PublishEntityTypes.Variant
                     && x.LocalId == variantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (map is null)
        {
            return;
        }

        var available = Math.Max(0, variant.Inventory.OnHand - variant.Inventory.Reserved);
        await publisher.SetInventoryAsync(map.ExternalId, available, cancellationToken).ConfigureAwait(false);
    }
}

public static class PublishingInfrastructureExtensions
{
    public static IServiceCollection AddPublishingServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ShopifyOptions>(configuration.GetSection(ShopifyOptions.Section));
        services.AddSingleton<NullSalesChannelPublisher>();
        services.AddScoped<ShopifySalesChannelPublisher>();
        services.AddHttpClient("shopify", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddScoped<ISalesChannelPublisher>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ShopifyOptions>>().Value;
            return string.IsNullOrWhiteSpace(opts.AccessToken)
                ? sp.GetRequiredService<NullSalesChannelPublisher>()
                : sp.GetRequiredService<ShopifySalesChannelPublisher>();
        });
        services.AddScoped<IPublishingJobScheduler, HangfirePublishingJobScheduler>();
        services.AddScoped<PublishProductJob>();
        services.AddScoped<SyncInventoryJob>();
        return services;
    }
}
