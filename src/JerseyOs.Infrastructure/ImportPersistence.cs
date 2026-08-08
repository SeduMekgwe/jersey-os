using Hangfire;
using JerseyOs.Application;
using JerseyOs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
            b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
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
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.MatchHint).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.ReviewNote).HasMaxLength(1000);
            b.HasIndex(x => new { x.BatchId, x.Sku });
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
    }
}

public sealed class HangfireImportJobScheduler(IBackgroundJobClient jobs) : IImportJobScheduler
{
    public void EnqueueParse(Guid batchId) =>
        jobs.Enqueue<ParseImportBatchJob>(job => job.ExecuteAsync(batchId, CancellationToken.None));
}

public sealed class ParseImportBatchJob(
    JerseyOsDbContext db,
    IObjectStorage storage,
    ISupplierCatalogFeed feed)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await db.ImportBatchesSet.IgnoreQueryFilters()
            .Include(x => x.Items)
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

            await using var stream = await OpenStoredFileAsync(storage, batch.ObjectKey, cancellationToken)
                .ConfigureAwait(false);
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
        }
        catch (Exception ex)
        {
            batch.MarkFailed(ex.Message);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static Task<Stream> OpenStoredFileAsync(
        IObjectStorage storage, string key, CancellationToken cancellationToken) =>
        storage.OpenReadAsync(key, cancellationToken);
}

public static class ImportInfrastructureExtensions
{
    public static IServiceCollection AddImportServices(this IServiceCollection services)
    {
        services.AddSingleton<ISupplierCatalogFeed, CsvSupplierCatalogFeed>();
        services.AddScoped<IImportJobScheduler, HangfireImportJobScheduler>();
        services.AddScoped<ParseImportBatchJob>();
        services.AddHttpClient("import-images", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
