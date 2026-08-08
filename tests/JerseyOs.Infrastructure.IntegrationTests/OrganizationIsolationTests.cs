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

public sealed class OrganizationIsolationTests : IAsyncLifetime
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
    public async Task QueryFiltersHideOtherOrganizations()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgA = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        var orgB = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f54");
        await using var db = CreateDb(_container!.GetConnectionString(), orgA);
        await db.Database.MigrateAsync();

        db.OrganizationsSet.Add(new Organization("Org A", "org-a", DateTimeOffset.UtcNow, orgA));
        await db.SaveChangesAsync();

        await using (var other = CreateDb(_container.GetConnectionString(), orgB))
        {
            other.OrganizationsSet.Add(new Organization("Org B", "org-b", DateTimeOffset.UtcNow, orgB));
            await other.SaveChangesAsync();
        }

        var visible = await db.OrganizationsSet.Select(x => x.Id).ToListAsync();
        Assert.Equal([orgA], visible);
    }

    [Fact]
    public async Task AuditFieldsAreStampedOnInsert()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgA = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        await using var db = CreateDb(_container!.GetConnectionString(), orgA, actor: "auditor-1");
        await db.Database.MigrateAsync();
        db.OrganizationsSet.Add(new Organization("Org A", "org-a", DateTimeOffset.UtcNow, orgA));
        await db.SaveChangesAsync();

        var organization = await db.OrganizationsSet.IgnoreQueryFilters().SingleAsync(x => x.Id == orgA);
        Assert.Equal("auditor-1", organization.CreatedBy);
        Assert.Equal("auditor-1", organization.ModifiedBy);
        Assert.NotEqual(default, organization.CreatedAtUtc);
    }

    [Fact]
    public async Task ConcurrencyTokenRejectsStaleUpdate()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgA = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        var connectionString = _container!.GetConnectionString();
        await using (var setup = CreateDb(connectionString, orgA))
        {
            await setup.Database.MigrateAsync();
            setup.FeatureFlags.Add(new FeatureFlag { OrganizationId = orgA, Key = "beta", Enabled = false });
            await setup.SaveChangesAsync();
        }

        await using var first = CreateDb(connectionString, orgA);
        await using var second = CreateDb(connectionString, orgA);
        var left = await first.FeatureFlags.SingleAsync(x => x.Key == "beta");
        var right = await second.FeatureFlags.SingleAsync(x => x.Key == "beta");
        left.Enabled = true;
        await first.SaveChangesAsync();
        right.Enabled = true;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task DomainEventWritesVersionedOutboxEnvelope()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgA = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        await using var db = CreateDb(_container!.GetConnectionString(), orgA);
        await db.Database.MigrateAsync();
        db.OrganizationsSet.Add(new Organization("Org A", "org-a", DateTimeOffset.UtcNow, orgA));
        await db.SaveChangesAsync();

        var message = await db.OutboxMessagesSet.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("jerseyos.organization.created", message.Type);
        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(message.PayloadJson);
        Assert.NotNull(envelope);
        Assert.Equal(1, envelope.SchemaVersion);
        Assert.Equal(orgA, envelope.OrganizationId);
        Assert.NotEqual(Guid.Empty, envelope.EventId);
    }

    [Fact]
    public async Task OutboxDispatchIsIdempotent()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgA = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        await using var db = CreateDb(_container!.GetConnectionString(), orgA);
        await db.Database.MigrateAsync();
        var message = new OutboxMessage
        {
            OrganizationId = orgA,
            Type = "test.event",
            PayloadJson = "{}",
            CorrelationId = "corr-1"
        };
        db.OutboxMessagesSet.Add(message);
        await db.SaveChangesAsync();

        var dispatcher = new OutboxDispatcher(db, TimeProvider.System, new NoOpPublishingJobScheduler());
        await dispatcher.DispatchAsync(message.Id, CancellationToken.None);
        await dispatcher.DispatchAsync(message.Id, CancellationToken.None);

        var stored = await db.OutboxMessagesSet.IgnoreQueryFilters().SingleAsync(x => x.Id == message.Id);
        Assert.NotNull(stored.ProcessedAtUtc);
        Assert.Equal(1, stored.Attempts);
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

        Assert.Fail("Docker is required for infrastructure integration tests. Install Docker or set JERSEYOS_ALLOW_SKIP_DOCKER=true for local machines without Docker.");
        return false;
    }

    private static JerseyOsDbContext CreateDb(string connectionString, Guid organizationId, string actor = "tests")
    {
        var outbox = new OutboxIntegrationEventPublisher();
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IIntegrationEventPublisher>(outbox);
        services.AddSingleton(outbox);
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<OrganizationCreatedHandler>());
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

    private sealed class NoOpPublishingJobScheduler : IPublishingJobScheduler
    {
        public void EnqueuePublishProduct(Guid organizationId, Guid productId, bool unpublish)
        {
        }

        public void EnqueueSyncInventory(Guid organizationId, Guid variantId)
        {
        }

        public void EnqueueProcessWebhook(Guid deliveryId)
        {
        }
    }
}
