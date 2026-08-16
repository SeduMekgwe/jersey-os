using System.Net.Http.Headers;
using Hangfire;
using JerseyOs.Application;
using JerseyOs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure;

public static class ImportModelBuilder
{
    public static void ConfigureImport(this ModelBuilder builder, Guid effectiveOrganizationId)
    {
        builder.Entity<Supplier>(b =>
        {
            b.ToTable("import_suppliers");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Code).HasMaxLength(64).IsRequired();
            b.Property(x => x.FeedKind).HasMaxLength(32).IsRequired();
            b.Property(x => x.FeedFormat).HasMaxLength(32).IsRequired();
            b.Property(x => x.FeedUrl).HasMaxLength(1000);
            b.Property(x => x.FeedBearerToken).HasMaxLength(2000);
            b.Property(x => x.ScrapeProfileJson).HasColumnType("nvarchar(max)");
            b.Property(x => x.ScrapeUsername).HasMaxLength(256);
            b.Property(x => x.ScrapePassword).HasMaxLength(512);
            b.Property(x => x.SyncCron).HasMaxLength(64);
            b.Property(x => x.LastSyncStatus).HasMaxLength(32);
            b.Property(x => x.LastSyncError).HasMaxLength(2000);
            b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<SupplierScrapeRun>(b =>
        {
            b.ToTable("import_supplier_scrape_runs");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
            b.Property(x => x.Error).HasMaxLength(2000);
            b.HasIndex(x => new { x.OrganizationId, x.SupplierId, x.CreatedAtUtc });
            b.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<ImportBatch>(b =>
        {
            b.ToTable("import_batches");
            b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            b.Property(x => x.ObjectKey).HasMaxLength(500).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.ErrorSummary).HasMaxLength(2000);
            b.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
            b.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Items).WithOne(x => x.Batch).HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<ImportItem>(b =>
        {
            b.ToTable("import_items");
            b.Property(x => x.RawJson).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            b.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            b.Property(x => x.Size).HasMaxLength(32).IsRequired();
            b.Property(x => x.StyleCode).HasMaxLength(64);
            b.Property(x => x.TeamName).HasMaxLength(200);
            b.Property(x => x.SeasonName).HasMaxLength(200);
            b.Property(x => x.ImageUrl).HasMaxLength(1000);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.SeoTitle).HasMaxLength(200);
            b.Property(x => x.SeoDescription).HasMaxLength(320);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.MatchHint).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.ReviewNote).HasMaxLength(1000);
            b.HasIndex(x => new { x.BatchId, x.Sku });
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
    }
}

public sealed class SupplierCatalogFeedRegistry(IEnumerable<ISupplierCatalogFeed> feeds) : ISupplierCatalogFeedResolver
{
    private readonly Dictionary<string, ISupplierCatalogFeed> _feeds = feeds
        .ToDictionary(x => x.Format, StringComparer.OrdinalIgnoreCase);

    public ISupplierCatalogFeed Resolve(string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        if (_feeds.TryGetValue(format.Trim(), out var feed))
        {
            return feed;
        }

        throw new InvalidOperationException($"No supplier catalog feed is registered for format '{format}'.");
    }
}

public sealed class HangfireImportJobScheduler(IBackgroundJobClient jobs, IRecurringJobManager recurring)
    : IImportJobScheduler
{
    public void EnqueueParse(Guid batchId) =>
        jobs.Enqueue<ParseImportBatchJob>(job => job.ExecuteAsync(batchId, CancellationToken.None));

    public void EnqueueSyncSupplierFeed(Guid supplierId) =>
        jobs.Enqueue<DispatchSupplierFeedJob>(job => job.ExecuteAsync(supplierId, CancellationToken.None));

    public void UpsertSupplierFeedSchedule(Guid organizationId, Guid supplierId, string? cronExpression)
    {
        var jobId = SupplierFeedSchedule.JobId(organizationId, supplierId);
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            recurring.RemoveIfExists(jobId);
            return;
        }

        recurring.AddOrUpdate<DispatchSupplierFeedJob>(
            jobId,
            job => job.ExecuteAsync(supplierId, CancellationToken.None),
            cronExpression.Trim());
    }

    public void RemoveSupplierFeedSchedule(Guid organizationId, Guid supplierId) =>
        recurring.RemoveIfExists(SupplierFeedSchedule.JobId(organizationId, supplierId));
}

public static class SupplierFeedSchedule
{
    public static string JobId(Guid organizationId, Guid supplierId) =>
        $"supplier-feed:{organizationId:N}:{supplierId:N}";
}

public sealed class ParseImportBatchJob(
    JerseyOsDbContext db,
    IObjectStorage storage,
    ISupplierCatalogFeedResolver feedResolver,
    INotificationPublisher notifications)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await db.ImportBatchesSet.IgnoreQueryFilters()
            .Include(x => x.Items)
            .Include(x => x.Supplier)
            .SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            .ConfigureAwait(false);
        if (batch is null || batch.Status is ImportBatchStatus.ReadyForReview or ImportBatchStatus.Completed)
        {
            return;
        }

        try
        {
            batch.MarkParsing();
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await using var stream = await storage.OpenReadAsync(batch.ObjectKey, cancellationToken)
                .ConfigureAwait(false);
            var format = ResolveFormat(
                batch.ContentType,
                batch.FileName,
                batch.ObjectKey,
                batch.Supplier?.FeedFormat);
            var feed = feedResolver.Resolve(format);
            var rows = await feed.ParseAsync(stream, cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Name) || string.IsNullOrWhiteSpace(row.Sku) ||
                    string.IsNullOrWhiteSpace(row.Size))
                {
                    continue;
                }

                var slug = ImportSlug.From(string.IsNullOrWhiteSpace(row.StyleCode) ? row.Name : row.StyleCode);
                var sku = row.Sku.Trim().ToUpperInvariant();
                var matches = await db.ProductVariantsSet.IgnoreQueryFilters()
                    .Where(x => x.OrganizationId == batch.OrganizationId && x.Sku == sku)
                    .Select(x => new { x.Id, x.ProductId })
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
                var hint = matches.Length switch
                {
                    0 => ImportMatchHint.New,
                    1 => ImportMatchHint.UpdateVariant,
                    _ => ImportMatchHint.Ambiguous
                };
                batch.AddItem(
                    System.Text.Json.JsonSerializer.Serialize(row.Raw),
                    row.Name,
                    slug,
                    sku,
                    row.Size,
                    string.IsNullOrWhiteSpace(row.StyleCode) ? null : row.StyleCode,
                    row.Team,
                    row.Season,
                    row.Quantity,
                    row.ImageUrl,
                    hint,
                    matches.Length == 1 ? matches[0].ProductId : null,
                    matches.Length == 1 ? matches[0].Id : null);
            }

            batch.MarkReadyForReview();
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await notifications.PublishAsync(
                    batch.OrganizationId,
                    NotificationKinds.ImportReady,
                    new
                    {
                        batchId = batch.Id,
                        fileName = batch.FileName,
                        supplierName = batch.Supplier?.Name,
                        itemCount = batch.Items.Count
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            batch.MarkFailed(ex.Message);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public static string ResolveFormat(
        string contentType, string fileName, string objectKey, string? supplierFeedFormat)
    {
        if (!string.IsNullOrWhiteSpace(supplierFeedFormat))
        {
            return supplierFeedFormat.Trim().ToLowerInvariant();
        }

        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || objectKey.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return SupplierFeedFormats.Json;
        }

        return SupplierFeedFormats.Csv;
    }
}

public sealed class DispatchSupplierFeedJob(
    FetchSupplierFeedJob fetchJob,
    IBackgroundJobClient backgroundJobs,
    JerseyOsDbContext db)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var kind = await db.SuppliersSet.IgnoreQueryFilters()
            .Where(x => x.Id == supplierId)
            .Select(x => x.FeedKind)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (kind == SupplierFeedKinds.Http)
        {
            await fetchJob.ExecuteAsync(supplierId, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (kind == SupplierFeedKinds.Scrape)
        {
            // Isolate browser work on the scrape queue so it cannot starve Shopify/default jobs.
            backgroundJobs.Enqueue<ScrapeSupplierFeedJob>(job =>
                job.ExecuteAsync(supplierId, CancellationToken.None));
        }
    }
}

public sealed class FetchSupplierFeedJob(
    JerseyOsDbContext db,
    IObjectStorage storage,
    IHttpClientFactory httpClientFactory,
    IImportJobScheduler jobs,
    TimeProvider time)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var supplier = await db.SuppliersSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken)
            .ConfigureAwait(false);
        if (supplier is null || supplier.FeedKind != SupplierFeedKinds.Http)
        {
            return;
        }

        var now = time.GetUtcNow();
        try
        {
            if (string.IsNullOrWhiteSpace(supplier.FeedUrl))
            {
                throw new InvalidOperationException("Supplier feed URL is not configured.");
            }

            var client = httpClientFactory.CreateClient("import-feeds");
            using var request = new HttpRequestMessage(HttpMethod.Get, supplier.FeedUrl);
            if (!string.IsNullOrWhiteSpace(supplier.FeedBearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supplier.FeedBearerToken);
            }

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Feed HTTP {(int)response.StatusCode}: {(body.Length > 300 ? body[..300] : body)}");
            }

            await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var buffer = new MemoryStream();
            await remote.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;

            var extension = supplier.FeedFormat == SupplierFeedFormats.Json ? ".json" : ".csv";
            var contentType = supplier.FeedFormat == SupplierFeedFormats.Json
                ? "application/json"
                : "text/csv";
            var fileName = $"http-{supplier.Code}-{now:yyyyMMddHHmmss}{extension}";
            var key = $"imports/{supplier.OrganizationId:N}/{Guid.NewGuid():N}{extension}";
            await storage.PutAsync(key, buffer, contentType, cancellationToken).ConfigureAwait(false);

            var batch = new ImportBatch(
                supplier.OrganizationId,
                supplier.Id,
                fileName,
                key,
                contentType,
                Guid.NewGuid().ToString("N"));
            db.ImportBatchesSet.Add(batch);
            supplier.RecordSyncSucceeded(now);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            jobs.EnqueueParse(batch.Id);
        }
        catch (Exception exception)
        {
            supplier.RecordSyncFailed(exception.Message, now);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}

public sealed class ScrapeSupplierFeedJob(
    JerseyOsDbContext db,
    IObjectStorage storage,
    ISupplierSiteCrawler crawler,
    IImportJobScheduler jobs,
    INotificationPublisher notifications,
    IAuditRecorder audit,
    TimeProvider time)
{
    [Queue("scrape")]
    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var supplier = await db.SuppliersSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken)
            .ConfigureAwait(false);
        if (supplier is null || supplier.FeedKind != SupplierFeedKinds.Scrape)
        {
            return;
        }

        var now = time.GetUtcNow();
        var run = new SupplierScrapeRun(supplier.OrganizationId, supplier.Id, Guid.NewGuid().ToString("N"));
        run.MarkRunning(now);
        db.SupplierScrapeRunsSet.Add(run);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (string.IsNullOrWhiteSpace(supplier.FeedUrl) || string.IsNullOrWhiteSpace(supplier.ScrapeProfileJson))
            {
                throw new InvalidOperationException("Scrape supplier is missing start URL or profile JSON.");
            }

            var result = await crawler.CrawlAsync(
                    new SupplierScrapeRequest(
                        supplier.OrganizationId,
                        supplier.Id,
                        supplier.FeedUrl,
                        supplier.ScrapeProfileJson,
                        supplier.ScrapeUsername,
                        supplier.ScrapePassword),
                    cancellationToken)
                .ConfigureAwait(false);

            await using var buffer = new MemoryStream(result.JsonUtf8);
            var fileName = $"scrape-{supplier.Code}-{now:yyyyMMddHHmmss}.json";
            var key = $"imports/{supplier.OrganizationId:N}/{Guid.NewGuid():N}.json";
            await storage.PutAsync(key, buffer, "application/json", cancellationToken).ConfigureAwait(false);

            var batch = new ImportBatch(
                supplier.OrganizationId,
                supplier.Id,
                fileName,
                key,
                "application/json",
                run.CorrelationId);
            db.ImportBatchesSet.Add(batch);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            run.MarkSucceeded(result.ProductCount, batch.Id, time.GetUtcNow());
            supplier.RecordSyncSucceeded(time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            jobs.EnqueueParse(batch.Id);
        }
        catch (Exception exception)
        {
            run.MarkFailed(exception.Message, time.GetUtcNow());
            supplier.RecordSyncFailed(exception.Message, time.GetUtcNow());
            audit.Record(
                supplier.OrganizationId,
                AuditActions.ScrapeRunFailed,
                nameof(SupplierScrapeRun),
                run.Id.ToString("N"),
                new { supplier.Code, error = exception.Message });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await notifications.PublishAsync(
                    supplier.OrganizationId,
                    NotificationKinds.ScrapeFailed,
                    new { runId = run.Id, supplierCode = supplier.Code, error = exception.Message },
                    cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
    }
}

public sealed class ImportScrapeOptions
{
    public const string Section = "Import:Scrape";
    /// <summary>Playwright (real browser) or Fixture (deterministic local/CI without browsers).</summary>
    public string Engine { get; set; } = "Fixture";
}

public static class ImportInfrastructureExtensions
{
    public static IServiceCollection AddImportServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
        {
            services.Configure<ImportScrapeOptions>(configuration.GetSection(ImportScrapeOptions.Section));
        }
        else
        {
            services.Configure<ImportScrapeOptions>(_ => { });
        }

        services.AddSingleton<ISupplierCatalogFeed, CsvSupplierCatalogFeed>();
        services.AddSingleton<ISupplierCatalogFeed, JsonSupplierCatalogFeed>();
        services.AddSingleton<ISupplierCatalogFeedResolver, SupplierCatalogFeedRegistry>();
        services.AddSingleton<ISupplierSiteCrawler>(sp =>
        {
            var engine = sp.GetRequiredService<IOptions<ImportScrapeOptions>>().Value.Engine;
            return string.Equals(engine, "Playwright", StringComparison.OrdinalIgnoreCase)
                ? new PlaywrightSupplierSiteCrawler()
                : new FixtureSupplierSiteCrawler();
        });
        services.AddScoped<IImportJobScheduler, HangfireImportJobScheduler>();
        services.AddScoped<ParseImportBatchJob>();
        services.AddScoped<FetchSupplierFeedJob>();
        services.AddScoped<ScrapeSupplierFeedJob>();
        services.AddScoped<DispatchSupplierFeedJob>();
        services.AddHttpClient("import-images", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient("import-feeds", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        return services;
    }

    public static void RegisterSupplierFeedRecurringJobs(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JerseyOsDbContext>();
        var recurring = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        var suppliers = db.SuppliersSet.IgnoreQueryFilters()
            .Where(x => (x.FeedKind == SupplierFeedKinds.Http || x.FeedKind == SupplierFeedKinds.Scrape)
                        && x.SyncCron != null && x.SyncCron != "")
            .Select(x => new { x.OrganizationId, x.Id, x.SyncCron })
            .ToList();
        foreach (var supplier in suppliers)
        {
            recurring.AddOrUpdate<DispatchSupplierFeedJob>(
                SupplierFeedSchedule.JobId(supplier.OrganizationId, supplier.Id),
                job => job.ExecuteAsync(supplier.Id, CancellationToken.None),
                supplier.SyncCron!);
        }
    }
}
