using System.Text;
using JerseyOs.Application;
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

public sealed class ImportIntegrationTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private bool _ready;
    private string? _storageRoot;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder().Build();
            await _container.StartAsync();
            _storageRoot = Path.Combine(Path.GetTempPath(), "jerseyos-import-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_storageRoot);
            _ready = true;
        }
        catch
        {
            _ready = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_storageRoot is not null && Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task ParseAndApproveCreatesDraftProduct()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgId = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        await using var db = CreateDb(_container!.GetConnectionString(), orgId);
        await db.Database.MigrateAsync();

        var supplier = new Supplier(orgId, "Demo Supplier", "demo");
        db.SuppliersSet.Add(supplier);
        await db.SaveChangesAsync();

        var storage = CreateStorage();
        var csv = """
                  style_code,name,sku,size,team,season,qty
                  H25,Home Kit,HOME25-M,M,Arsenal,2025-26,4
                  """;
        await using (var upload = new MemoryStream(Encoding.UTF8.GetBytes(csv)))
        {
            await storage.PutAsync($"imports/{orgId:N}/sample.csv", upload, "text/csv", CancellationToken.None);
        }

        var batch = new ImportBatch(orgId, supplier.Id, "sample.csv", $"imports/{orgId:N}/sample.csv", "text/csv", "corr");
        db.ImportBatchesSet.Add(batch);
        await db.SaveChangesAsync();

        var job = new ParseImportBatchJob(
            db, storage, new SupplierCatalogFeedRegistry([new CsvSupplierCatalogFeed()]), new NoOpNotificationPublisher());
        await job.ExecuteAsync(batch.Id, CancellationToken.None);

        var reloaded = await db.ImportBatchesSet.Include(x => x.Items).SingleAsync(x => x.Id == batch.Id);
        Assert.Equal(ImportBatchStatus.ReadyForReview, reloaded.Status);
        var item = Assert.Single(reloaded.Items);

        var services = new ServiceCollection();
        services.AddHttpClient("import-images");
        await using var provider = services.BuildServiceProvider();
        await ImportApply.ApproveAndApplyAsync(
            db,
            storage,
            provider.GetRequiredService<IHttpClientFactory>(),
            TimeProvider.System,
            item,
            null,
            CancellationToken.None);
        await db.SaveChangesAsync();

        var product = await db.ProductsSet.Include(x => x.Variants).ThenInclude(x => x.Inventory).SingleAsync();
        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal("HOME25-M", Assert.Single(product.Variants).Sku);
        Assert.Equal(4, product.Variants.Single().Inventory.OnHand);
        Assert.Equal(ImportItemStatus.Applied, item.Status);
    }

    [Fact]
    public async Task BatchesAreOrganizationIsolated()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var orgA = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        var orgB = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f54");
        await using var db = CreateDb(_container!.GetConnectionString(), orgA);
        await db.Database.MigrateAsync();
        var supplierA = new Supplier(orgA, "A", "a");
        db.SuppliersSet.Add(supplierA);
        await db.SaveChangesAsync();
        db.ImportBatchesSet.Add(new ImportBatch(orgA, supplierA.Id, "a.csv", "imports/a.csv", "text/csv", "c1"));
        await db.SaveChangesAsync();

        await using (var other = CreateDb(_container.GetConnectionString(), orgB))
        {
            var supplierB = new Supplier(orgB, "B", "b");
            other.SuppliersSet.Add(supplierB);
            await other.SaveChangesAsync();
            other.ImportBatchesSet.Add(new ImportBatch(orgB, supplierB.Id, "b.csv", "imports/b.csv", "text/csv", "c2"));
            await other.SaveChangesAsync();
        }

        Assert.Equal(1, await db.ImportBatchesSet.CountAsync());
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

        Assert.Fail("Docker is required for import integration tests.");
        return false;
    }

    private LocalObjectStorage CreateStorage() =>
        new(
            Options.Create(new ObjectStorageOptions
            {
                LocalRootPath = _storageRoot!,
                PublicBasePath = "/media"
            }),
            new TestHostEnvironment(_storageRoot!));

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
}
