using System.Text.Json;
using JerseyOs.Application;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using JerseyOs.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;

namespace JerseyOs.Infrastructure.IntegrationTests;

public sealed class PublishingIntegrationTests : IAsyncLifetime
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
    public async Task PublishJobWritesExternalIdsAndSucceededRun()
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
        var team = new Team(orgId, "Arsenal", "arsenal");
        var season = new Season(orgId, "2025-26", "2025-26");
        db.TeamsSet.Add(team);
        db.SeasonsSet.Add(season);
        var now = DateTimeOffset.UtcNow;
        var product = new Product(orgId, "Home Kit", "home-kit", "H25", team.Id, season.Id, now);
        product.UpsertVariant(null, "HOME-M", "M", 0, now, 1299.00m);
        product.Activate(now);
        db.ProductsSet.Add(product);
        await db.SaveChangesAsync();

        var job = new PublishProductJob(
            db, FixedResolver.Null, CreateStorage(), new NoOpNotificationPublisher(), new NoOpAuditRecorder(), TimeProvider.System);
        await job.ExecuteAsync(orgId, product.Id, unpublish: false, CancellationToken.None);

        var map = await db.ExternalIdMapsSet.IgnoreQueryFilters()
            .SingleAsync(x => x.LocalId == product.Id && x.EntityType == PublishEntityTypes.Product);
        Assert.Contains(product.Id.ToString("N"), map.ExternalId, StringComparison.OrdinalIgnoreCase);
        var run = await db.PublishRunsSet.IgnoreQueryFilters().SingleAsync(x => x.ProductId == product.Id);
        Assert.Equal(PublishRunStatus.Succeeded, run.Status);
    }

    [Fact]
    public async Task DraftProductIsNotPublished()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgId = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        await using var db = CreateDb(_container!.GetConnectionString(), orgId);
        await db.Database.MigrateAsync();
        db.SalesChannelsSet.Add(new SalesChannel(orgId, SalesChannelCodes.Shopify, "Shopify"));
        var product = new Product(orgId, "Draft", "draft", null, null, null, DateTimeOffset.UtcNow);
        db.ProductsSet.Add(product);
        await db.SaveChangesAsync();

        var job = new PublishProductJob(
            db, FixedResolver.Null, CreateStorage(), new NoOpNotificationPublisher(), new NoOpAuditRecorder(), TimeProvider.System);
        await job.ExecuteAsync(orgId, product.Id, unpublish: false, CancellationToken.None);

        Assert.Empty(await db.ExternalIdMapsSet.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.PublishRunsSet.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task OutboxDispatcherEnqueuesPublishJob()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgId = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        await using var db = CreateDb(_container!.GetConnectionString(), orgId);
        await db.Database.MigrateAsync();
        var productId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            "jerseyos.product.activated",
            1,
            orgId,
            "corr",
            productId.ToString("N"),
            DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(new { productId, organizationId = orgId }));
        var message = new OutboxMessage
        {
            OrganizationId = orgId,
            Type = envelope.EventType,
            CorrelationId = envelope.CorrelationId,
            PayloadJson = JsonSerializer.Serialize(envelope)
        };
        db.OutboxMessagesSet.Add(message);
        await db.SaveChangesAsync();

        var scheduler = new CapturingPublishingJobScheduler();
        var dispatcher = new OutboxDispatcher(db, TimeProvider.System, scheduler);
        await dispatcher.DispatchAsync(message.Id, CancellationToken.None);

        Assert.Single(scheduler.PublishCalls);
        Assert.Equal(productId, scheduler.PublishCalls[0].ProductId);
        Assert.False(scheduler.PublishCalls[0].Unpublish);
        var processed = await db.OutboxMessagesSet.IgnoreQueryFilters().SingleAsync(x => x.Id == message.Id);
        Assert.NotNull(processed.ProcessedAtUtc);
    }

    [Fact]
    public async Task MapsAndRunsAreOrganizationIsolated()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgA = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        var orgB = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f54");
        await using var db = CreateDb(_container!.GetConnectionString(), orgA);
        await db.Database.MigrateAsync();
        var channelA = new SalesChannel(orgA, SalesChannelCodes.Shopify, "Shopify");
        db.SalesChannelsSet.Add(channelA);
        await db.SaveChangesAsync();
        db.ExternalIdMapsSet.Add(new ExternalIdMap(orgA, channelA.Id, PublishEntityTypes.Product, Guid.NewGuid(), "gid://a"));
        db.PublishRunsSet.Add(new PublishRun(orgA, channelA.Id, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await using (var other = CreateDb(_container.GetConnectionString(), orgB))
        {
            var channelB = new SalesChannel(orgB, SalesChannelCodes.Shopify, "Shopify");
            other.SalesChannelsSet.Add(channelB);
            await other.SaveChangesAsync();
            other.ExternalIdMapsSet.Add(new ExternalIdMap(orgB, channelB.Id, PublishEntityTypes.Product, Guid.NewGuid(), "gid://b"));
            other.PublishRunsSet.Add(new PublishRun(orgB, channelB.Id, Guid.NewGuid()));
            await other.SaveChangesAsync();
        }

        Assert.Equal(1, await db.ExternalIdMapsSet.CountAsync());
        Assert.Equal(1, await db.PublishRunsSet.CountAsync());
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

        Assert.Fail("Docker is required for publishing integration tests.");
        return false;
    }

    private static LocalObjectStorage CreateStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), "jerseyos-publish-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new LocalObjectStorage(
            Options.Create(new ObjectStorageOptions
            {
                LocalRootPath = root,
                PublicBasePath = "/media"
            }),
            new TestHostEnvironment(root));
    }

    private sealed class TestHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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

    private sealed class CapturingPublishingJobScheduler : IPublishingJobScheduler
    {
        public List<(Guid OrganizationId, Guid ProductId, bool Unpublish)> PublishCalls { get; } = [];
        public List<(Guid OrganizationId, Guid VariantId)> InventoryCalls { get; } = [];

        public void EnqueuePublishProduct(Guid organizationId, Guid productId, bool unpublish) =>
            PublishCalls.Add((organizationId, productId, unpublish));

        public void EnqueueSyncInventory(Guid organizationId, Guid variantId) =>
            InventoryCalls.Add((organizationId, variantId));

        public void EnqueueProcessWebhook(Guid deliveryId)
        {
        }
    }

    private sealed class FixedResolver(ISalesChannelPublisher publisher) : ISalesChannelPublisherResolver
    {
        public static FixedResolver Null { get; } = new(new NullSalesChannelPublisher());

        public ISalesChannelPublisher? Resolve(string channelCode) => publisher;
    }
}
