using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JerseyOs.Application;
using JerseyOs.Domain;
using JerseyOs.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;

namespace JerseyOs.Infrastructure.IntegrationTests;

public sealed class ShopifyWebhookTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private bool _ready;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder().Build();
            await _container.StartAsync();
            _ready = true;
        }
        catch
        {
            _ready = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public void HmacRejectsInvalidSignature()
    {
        var hmac = new ShopifyWebhookHmac(Options.Create(new ShopifyOptions { WebhookSecret = "secret" }));
        Assert.False(hmac.IsValid("{}", "not-valid-base64!!!"));
        var valid = Sign("secret", "{\"ok\":true}");
        Assert.True(hmac.IsValid("{\"ok\":true}", valid));
        Assert.False(hmac.IsValid("{\"ok\":false}", valid));
    }

    [Fact]
    public async Task OrderCreateReservesMappedVariant()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgId = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        await using var db = CreateDb(_container!.GetConnectionString(), orgId);
        await db.Database.MigrateAsync();

        var channel = new SalesChannel(orgId, SalesChannelCodes.Shopify, "Shopify");
        db.SalesChannelsSet.Add(channel);
        var now = DateTimeOffset.UtcNow;
        var team = new Team(orgId, "Arsenal", "arsenal");
        var season = new Season(orgId, "2025-26", "2025-26");
        db.TeamsSet.Add(team);
        db.SeasonsSet.Add(season);
        var product = new Product(orgId, "Home", "home", "H1", team.Id, season.Id, now);
        var variant = product.UpsertVariant(null, "HOME-M", "M", 0, now, 100m);
        variant.Inventory.Adjust(10, "seed", now);
        product.Activate(now);
        db.ProductsSet.Add(product);
        await db.SaveChangesAsync();

        var externalVariantId = "gid://shopify/ProductVariant/999001";
        db.ExternalIdMapsSet.Add(new ExternalIdMap(
            orgId, channel.Id, PublishEntityTypes.Variant, variant.Id, externalVariantId));
        await db.SaveChangesAsync();

        var payload = JsonSerializer.Serialize(new
        {
            id = 42,
            line_items = new[]
            {
                new { variant_id = 999001, quantity = 3 }
            }
        });
        var delivery = new WebhookDelivery(orgId, "wh-1", "orders/create", payload);
        db.WebhookDeliveriesSet.Add(delivery);
        await db.SaveChangesAsync();

        var job = new ProcessShopifyWebhookJob(db, TimeProvider.System);
        await job.ExecuteAsync(delivery.Id, CancellationToken.None);

        var inventory = await db.InventoryLevelsSet.IgnoreQueryFilters().SingleAsync(x => x.VariantId == variant.Id);
        Assert.Equal(10, inventory.OnHand);
        Assert.Equal(3, inventory.Reserved);
        var reloaded = await db.WebhookDeliveriesSet.IgnoreQueryFilters().SingleAsync(x => x.Id == delivery.Id);
        Assert.Equal(WebhookDeliveryStatus.Succeeded, reloaded.Status);
    }

    [Fact]
    public async Task OrderCancelledReleasesAndFulfilledCommits()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgId = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        await using var db = CreateDb(_container!.GetConnectionString(), orgId);
        await db.Database.MigrateAsync();

        var channel = new SalesChannel(orgId, SalesChannelCodes.Shopify, "Shopify");
        db.SalesChannelsSet.Add(channel);
        var now = DateTimeOffset.UtcNow;
        var team = new Team(orgId, "Chelsea", "chelsea");
        var season = new Season(orgId, "2025-26", "2025-26");
        db.TeamsSet.Add(team);
        db.SeasonsSet.Add(season);
        var product = new Product(orgId, "Away", "away", "A1", team.Id, season.Id, now);
        var variant = product.UpsertVariant(null, "AWAY-M", "M", 0, now, 100m);
        variant.Inventory.Adjust(8, "seed", now);
        product.Activate(now);
        db.ProductsSet.Add(product);
        await db.SaveChangesAsync();
        db.ExternalIdMapsSet.Add(new ExternalIdMap(
            orgId, channel.Id, PublishEntityTypes.Variant, variant.Id, "gid://shopify/ProductVariant/55"));
        await db.SaveChangesAsync();

        var payload = """{"line_items":[{"variant_id":55,"quantity":2}]}""";
        var create = new WebhookDelivery(orgId, "wh-create", "orders/create", payload);
        db.WebhookDeliveriesSet.Add(create);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(create.Id, CancellationToken.None);

        var cancel = new WebhookDelivery(orgId, "wh-cancel", "orders/cancelled", payload);
        db.WebhookDeliveriesSet.Add(cancel);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(cancel.Id, CancellationToken.None);
        var afterCancel = await db.InventoryLevelsSet.IgnoreQueryFilters().SingleAsync(x => x.VariantId == variant.Id);
        Assert.Equal(0, afterCancel.Reserved);

        var create2 = new WebhookDelivery(orgId, "wh-create-2", "orders/create", payload);
        db.WebhookDeliveriesSet.Add(create2);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(create2.Id, CancellationToken.None);

        var fulfill = new WebhookDelivery(orgId, "wh-fulfill", "orders/fulfilled", payload);
        db.WebhookDeliveriesSet.Add(fulfill);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(fulfill.Id, CancellationToken.None);

        var afterFulfill = await db.InventoryLevelsSet.IgnoreQueryFilters().SingleAsync(x => x.VariantId == variant.Id);
        Assert.Equal(6, afterFulfill.OnHand);
        Assert.Equal(0, afterFulfill.Reserved);
    }

    [Fact]
    public async Task PartialFulfillmentCommitsThenCancelReleasesRemainder()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgId = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        await using var db = CreateDb(_container!.GetConnectionString(), orgId);
        await db.Database.MigrateAsync();

        var channel = new SalesChannel(orgId, SalesChannelCodes.Shopify, "Shopify");
        db.SalesChannelsSet.Add(channel);
        var now = DateTimeOffset.UtcNow;
        var team = new Team(orgId, "Liverpool", "liverpool");
        var season = new Season(orgId, "2025-26", "2025-26");
        db.TeamsSet.Add(team);
        db.SeasonsSet.Add(season);
        var product = new Product(orgId, "Third", "third", "T1", team.Id, season.Id, now);
        var variant = product.UpsertVariant(null, "THIRD-M", "M", 0, now, 100m);
        variant.Inventory.Adjust(10, "seed", now);
        product.Activate(now);
        db.ProductsSet.Add(product);
        await db.SaveChangesAsync();
        db.ExternalIdMapsSet.Add(new ExternalIdMap(
            orgId, channel.Id, PublishEntityTypes.Variant, variant.Id, "gid://shopify/ProductVariant/77"));
        await db.SaveChangesAsync();

        var create = new WebhookDelivery(
            orgId,
            "wh-pf-create",
            "orders/create",
            """{"line_items":[{"variant_id":77,"quantity":5}]}""");
        db.WebhookDeliveriesSet.Add(create);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(create.Id, CancellationToken.None);

        var fulfillPartial = new WebhookDelivery(
            orgId,
            "wh-pf-fulfill",
            "fulfillments/create",
            """{"line_items":[{"variant_id":77,"quantity":2}]}""");
        db.WebhookDeliveriesSet.Add(fulfillPartial);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(fulfillPartial.Id, CancellationToken.None);

        var afterPartial = await db.InventoryLevelsSet.IgnoreQueryFilters().SingleAsync(x => x.VariantId == variant.Id);
        Assert.Equal(8, afterPartial.OnHand);
        Assert.Equal(3, afterPartial.Reserved);

        var cancel = new WebhookDelivery(
            orgId,
            "wh-pf-cancel",
            "orders/cancelled",
            """{"line_items":[{"variant_id":77,"quantity":5,"fulfillable_quantity":3}]}""");
        db.WebhookDeliveriesSet.Add(cancel);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(cancel.Id, CancellationToken.None);

        var afterCancel = await db.InventoryLevelsSet.IgnoreQueryFilters().SingleAsync(x => x.VariantId == variant.Id);
        Assert.Equal(8, afterCancel.OnHand);
        Assert.Equal(0, afterCancel.Reserved);
    }

    [Fact]
    public async Task RefundReturnRestocksCommittedInventory()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgId = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        await using var db = CreateDb(_container!.GetConnectionString(), orgId);
        await db.Database.MigrateAsync();

        var channel = new SalesChannel(orgId, SalesChannelCodes.Shopify, "Shopify");
        db.SalesChannelsSet.Add(channel);
        var now = DateTimeOffset.UtcNow;
        var team = new Team(orgId, "Spurs", "spurs");
        var season = new Season(orgId, "2025-26", "2025-26");
        db.TeamsSet.Add(team);
        db.SeasonsSet.Add(season);
        var product = new Product(orgId, "Home", "home-spurs", "HS1", team.Id, season.Id, now);
        var variant = product.UpsertVariant(null, "HS-M", "M", 0, now, 100m);
        variant.Inventory.Adjust(5, "seed", now);
        product.Activate(now);
        db.ProductsSet.Add(product);
        await db.SaveChangesAsync();
        db.ExternalIdMapsSet.Add(new ExternalIdMap(
            orgId, channel.Id, PublishEntityTypes.Variant, variant.Id, "gid://shopify/ProductVariant/88"));
        await db.SaveChangesAsync();

        var create = new WebhookDelivery(
            orgId, "wh-rf-create", "orders/create",
            """{"line_items":[{"variant_id":88,"quantity":2}]}""");
        db.WebhookDeliveriesSet.Add(create);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(create.Id, CancellationToken.None);

        var fulfill = new WebhookDelivery(
            orgId, "wh-rf-fulfill", "orders/fulfilled",
            """{"line_items":[{"variant_id":88,"quantity":2}]}""");
        db.WebhookDeliveriesSet.Add(fulfill);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(fulfill.Id, CancellationToken.None);

        var refund = new WebhookDelivery(
            orgId,
            "wh-rf-refund",
            "refunds/create",
            """
            {
              "refund_line_items": [
                {
                  "quantity": 1,
                  "restock_type": "return",
                  "line_item": { "variant_id": 88, "quantity": 2 }
                }
              ]
            }
            """);
        db.WebhookDeliveriesSet.Add(refund);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(refund.Id, CancellationToken.None);

        var inventory = await db.InventoryLevelsSet.IgnoreQueryFilters().SingleAsync(x => x.VariantId == variant.Id);
        Assert.Equal(4, inventory.OnHand);
        Assert.Equal(0, inventory.Reserved);
        var delivery = await db.WebhookDeliveriesSet.IgnoreQueryFilters().SingleAsync(x => x.WebhookId == "wh-rf-refund");
        Assert.Equal(WebhookDeliveryStatus.Succeeded, delivery.Status);
    }

    [Fact]
    public async Task RefundCancelReleasesUnfulfilledReservation()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgId = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        await using var db = CreateDb(_container!.GetConnectionString(), orgId);
        await db.Database.MigrateAsync();

        var channel = new SalesChannel(orgId, SalesChannelCodes.Shopify, "Shopify");
        db.SalesChannelsSet.Add(channel);
        var now = DateTimeOffset.UtcNow;
        var team = new Team(orgId, "City", "city");
        var season = new Season(orgId, "2025-26", "2025-26");
        db.TeamsSet.Add(team);
        db.SeasonsSet.Add(season);
        var product = new Product(orgId, "Away", "away-city", "AC1", team.Id, season.Id, now);
        var variant = product.UpsertVariant(null, "AC-M", "M", 0, now, 100m);
        variant.Inventory.Adjust(5, "seed", now);
        product.Activate(now);
        db.ProductsSet.Add(product);
        await db.SaveChangesAsync();
        db.ExternalIdMapsSet.Add(new ExternalIdMap(
            orgId, channel.Id, PublishEntityTypes.Variant, variant.Id, "gid://shopify/ProductVariant/89"));
        await db.SaveChangesAsync();

        var create = new WebhookDelivery(
            orgId, "wh-rc-create", "orders/create",
            """{"line_items":[{"variant_id":89,"quantity":2}]}""");
        db.WebhookDeliveriesSet.Add(create);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(create.Id, CancellationToken.None);

        var refund = new WebhookDelivery(
            orgId,
            "wh-rc-refund",
            "refunds/create",
            """
            {
              "refund_line_items": [
                {
                  "quantity": 2,
                  "restock_type": "cancel",
                  "line_item": { "variant_id": 89 }
                }
              ]
            }
            """);
        db.WebhookDeliveriesSet.Add(refund);
        await db.SaveChangesAsync();
        await new ProcessShopifyWebhookJob(db, TimeProvider.System).ExecuteAsync(refund.Id, CancellationToken.None);

        var inventory = await db.InventoryLevelsSet.IgnoreQueryFilters().SingleAsync(x => x.VariantId == variant.Id);
        Assert.Equal(5, inventory.OnHand);
        Assert.Equal(0, inventory.Reserved);
    }

    private bool EnsureDockerOrSkip()
    {
        if (_ready)
        {
            return true;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("JERSEYOS_ALLOW_SKIP_DOCKER"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Assert.Fail("Docker is required for Shopify webhook integration tests.");
        return false;
    }

    private static string Sign(string secret, string body)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hash);
    }

    private static JerseyOsDbContext CreateDb(string connectionString, Guid organizationId)
    {
        var outbox = new OutboxIntegrationEventPublisher();
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IIntegrationEventPublisher>(outbox);
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<ProductCreatedHandler>());
        var provider = services.BuildServiceProvider();
        return new JerseyOsDbContext(
            new DbContextOptionsBuilder<JerseyOsDbContext>().UseSqlServer(connectionString).Options,
            new FixedRequest(organizationId),
            Options.Create(new DatabaseOptions { ConnectionString = connectionString, DefaultOrganizationId = organizationId }),
            provider.GetRequiredService<IPublisher>(),
            outbox);
    }

    private sealed class FixedRequest(Guid organizationId) : ICurrentRequest
    {
        public Guid? UserId => null;
        public Guid? OrganizationId => organizationId;
        public string Actor => "tests";
        public string CorrelationId => "tests";
    }
}
