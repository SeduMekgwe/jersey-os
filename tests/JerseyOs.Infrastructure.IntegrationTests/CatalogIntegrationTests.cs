using System.Text.Json;
using JerseyOs.Application;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using JerseyOs.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;

namespace JerseyOs.Infrastructure.IntegrationTests;

public sealed class CatalogIntegrationTests : IAsyncLifetime
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
    public async Task ProductsAreOrganizationIsolatedAndEmitOutbox()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgA = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        var orgB = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f54");
        await using var db = CreateDb(_container!.GetConnectionString(), orgA);
        await db.Database.MigrateAsync();

        db.ProductsSet.Add(new Product(orgA, "Home", "home", null, null, null, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        await using (var other = CreateDb(_container.GetConnectionString(), orgB))
        {
            other.ProductsSet.Add(new Product(orgB, "Away", "away", null, null, null, DateTimeOffset.UtcNow));
            await other.SaveChangesAsync();
        }

        Assert.Equal(1, await db.ProductsSet.CountAsync());
        var message = await db.OutboxMessagesSet.IgnoreQueryFilters()
            .SingleAsync(x => x.Type == "jerseyos.product.created");
        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(message.PayloadJson);
        Assert.NotNull(envelope);
        Assert.Equal(1, envelope.SchemaVersion);
    }

    [Fact]
    public async Task InventoryAdjustIsConcurrentAndAudited()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgA = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        var connectionString = _container!.GetConnectionString();
        Guid variantId;
        await using (var setup = CreateDb(connectionString, orgA))
        {
            await setup.Database.MigrateAsync();
            var product = new Product(orgA, "Third", "third", null, null, null, DateTimeOffset.UtcNow);
            var variant = product.UpsertVariant(null, "THIRD-L", "L", 0, DateTimeOffset.UtcNow);
            variant.Inventory.Adjust(10, "seed", DateTimeOffset.UtcNow);
            setup.ProductsSet.Add(product);
            await setup.SaveChangesAsync();
            variantId = variant.Id;
        }

        await using var first = CreateDb(connectionString, orgA);
        await using var second = CreateDb(connectionString, orgA);
        var left = await first.InventoryLevelsSet.SingleAsync(x => x.VariantId == variantId);
        var right = await second.InventoryLevelsSet.SingleAsync(x => x.VariantId == variantId);
        left.Adjust(1, "a", DateTimeOffset.UtcNow);
        await first.SaveChangesAsync();
        right.Adjust(1, "b", DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var verify = CreateDb(connectionString, orgA);
        var inventory = await verify.InventoryLevelsSet.SingleAsync(x => x.VariantId == variantId);
        Assert.Equal(11, inventory.OnHand);
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

        Assert.Fail("Docker is required for catalog integration tests. Install Docker or set JERSEYOS_ALLOW_SKIP_DOCKER=true.");
        return false;
    }

    private static JerseyOsDbContext CreateDb(string connectionString, Guid organizationId, string actor = "tests")
    {
        var outbox = new OutboxIntegrationEventPublisher();
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IIntegrationEventPublisher>(outbox);
        services.AddSingleton(outbox);
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<ProductCreatedHandler>());
        var provider = services.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<JerseyOsDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new JerseyOsDbContext(
            options,
            new FixedRequest(organizationId, actor),
            Options.Create(new DatabaseOptions
            {
                ConnectionString = connectionString,
                DefaultOrganizationId = organizationId
            }),
            provider.GetRequiredService<IPublisher>(),
            outbox);
    }

    private sealed class FixedRequest(Guid organizationId, string actor) : ICurrentRequest
    {
        public Guid? UserId => null;
        public Guid? OrganizationId => organizationId;
        public string Actor => actor;
        public string CorrelationId => "tests";
    }
}
