using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JerseyOs.Application;

public sealed record SupplierCatalogRow(
    string StyleCode,
    string Name,
    string Sku,
    string Size,
    string? Team,
    string? Season,
    int? Quantity,
    decimal? PriceAmount,
    string? ImageUrl,
    IReadOnlyDictionary<string, string> Raw);

public interface ISupplierCatalogFeed
{
    string Format { get; }
    Task<IReadOnlyList<SupplierCatalogRow>> ParseAsync(Stream content, CancellationToken cancellationToken);
}

public interface IImportJobScheduler
{
    void EnqueueParse(Guid batchId);
}

public static class ImportSlug
{
    public static string From(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9]+", "-");
        return normalized.Trim('-');
    }
}

public sealed record CreateSupplierCommand(string Name, string Code) : IRequest<SupplierResponse>;
public sealed record ListSuppliersQuery : IRequest<IReadOnlyCollection<SupplierResponse>>;
public sealed record UploadImportBatchCommand(
    Guid SupplierId,
    Stream Content,
    string FileName,
    string ContentType) : IRequest<ImportBatchSummaryResponse>;

public sealed record ListImportBatchesQuery : IRequest<IReadOnlyCollection<ImportBatchSummaryResponse>>;
public sealed record GetImportBatchQuery(Guid BatchId) : IRequest<ImportBatchDetailResponse?>;
public sealed record UpdateImportItemCommand(
    Guid ItemId,
    string Name,
    string Slug,
    string Sku,
    string Size,
    string? StyleCode,
    string? TeamName,
    string? SeasonName,
    int? Quantity,
    string? ImageUrl) : IRequest<ImportItemResponse?>;

public sealed record ApproveImportItemCommand(Guid ItemId, string? Note) : IRequest<ImportItemResponse?>;
public sealed record RejectImportItemCommand(Guid ItemId, string? Note) : IRequest<ImportItemResponse?>;
public sealed record BulkApproveImportItemsCommand(IReadOnlyCollection<Guid> ItemIds, string? Note)
    : IRequest<IReadOnlyCollection<ImportItemResponse>>;

public sealed class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
    }
}

public sealed class UpdateImportItemValidator : AbstractValidator<UpdateImportItemCommand>
{
    public UpdateImportItemValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Size).NotEmpty().MaximumLength(32);
    }
}

public sealed class CreateSupplierHandler(IApplicationDbContext db, ICurrentRequest current)
    : IRequestHandler<CreateSupplierCommand, SupplierResponse>
{
    public async Task<SupplierResponse> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var orgId = current.OrganizationId ?? throw new InvalidOperationException("Organization context is required.");
        var code = request.Code.Trim().ToLowerInvariant();
        if (await db.Suppliers.AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new InvalidOperationException($"Supplier code '{request.Code}' is already in use.");
        }

        var supplier = new Supplier(orgId, request.Name, code);
        db.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
        return new SupplierResponse(supplier.Id, supplier.Name, supplier.Code);
    }
}

public sealed class ListSuppliersHandler(IApplicationDbContext db)
    : IRequestHandler<ListSuppliersQuery, IReadOnlyCollection<SupplierResponse>>
{
    public async Task<IReadOnlyCollection<SupplierResponse>> Handle(
        ListSuppliersQuery request, CancellationToken cancellationToken) =>
        await db.Suppliers.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new SupplierResponse(x.Id, x.Name, x.Code))
            .ToArrayAsync(cancellationToken);
}

public sealed class UploadImportBatchHandler(
    IApplicationDbContext db,
    ICurrentRequest current,
    IObjectStorage storage,
    IImportJobScheduler jobs) : IRequestHandler<UploadImportBatchCommand, ImportBatchSummaryResponse>
{
    public async Task<ImportBatchSummaryResponse> Handle(
        UploadImportBatchCommand request, CancellationToken cancellationToken)
    {
        var orgId = current.OrganizationId ?? throw new InvalidOperationException("Organization context is required.");
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == request.SupplierId, cancellationToken)
            ?? throw new InvalidOperationException("Supplier was not found.");

        var extension = Path.GetExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".csv";
        }

        var key = $"imports/{orgId:N}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await storage.PutAsync(key, request.Content, request.ContentType, cancellationToken);
        var batch = new ImportBatch(orgId, supplier.Id, request.FileName, key, request.ContentType, current.CorrelationId);
        db.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        jobs.EnqueueParse(batch.Id);
        return new ImportBatchSummaryResponse(
            batch.Id,
            supplier.Id,
            supplier.Name,
            batch.FileName,
            batch.Status.ToString(),
            0,
            0,
            batch.ErrorSummary,
            batch.CreatedAtUtc);
    }
}

public sealed class ListImportBatchesHandler(IApplicationDbContext db)
    : IRequestHandler<ListImportBatchesQuery, IReadOnlyCollection<ImportBatchSummaryResponse>>
{
    public async Task<IReadOnlyCollection<ImportBatchSummaryResponse>> Handle(
        ListImportBatchesQuery request, CancellationToken cancellationToken)
    {
        var batches = await db.ImportBatches.AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
        return batches.Select(b => new ImportBatchSummaryResponse(
            b.Id,
            b.SupplierId,
            b.Supplier.Name,
            b.FileName,
            b.Status.ToString(),
            b.Items.Count,
            b.Items.Count(i => i.Status == ImportItemStatus.Pending),
            b.ErrorSummary,
            b.CreatedAtUtc)).ToArray();
    }
}

public sealed class GetImportBatchHandler(IApplicationDbContext db)
    : IRequestHandler<GetImportBatchQuery, ImportBatchDetailResponse?>
{
    public async Task<ImportBatchDetailResponse?> Handle(GetImportBatchQuery request, CancellationToken cancellationToken)
    {
        var batch = await db.ImportBatches.AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == request.BatchId, cancellationToken);
        return batch is null ? null : ImportMapping.ToDetail(batch);
    }
}

public sealed class UpdateImportItemHandler(IApplicationDbContext db)
    : IRequestHandler<UpdateImportItemCommand, ImportItemResponse?>
{
    public async Task<ImportItemResponse?> Handle(UpdateImportItemCommand request, CancellationToken cancellationToken)
    {
        var item = await db.ImportItems.Include(x => x.Batch)
            .SingleOrDefaultAsync(x => x.Id == request.ItemId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        if (item.Batch.Status != ImportBatchStatus.ReadyForReview)
        {
            throw new InvalidOperationException("Batch is not ready for review.");
        }

        item.UpdateProposed(
            request.Name,
            request.Slug,
            request.Sku,
            request.Size,
            request.StyleCode,
            request.TeamName,
            request.SeasonName,
            request.Quantity,
            request.ImageUrl);
        await ImportMatching.RefreshMatchAsync(db, item, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ImportMapping.ToItem(item);
    }
}

public sealed class ApproveImportItemHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    IHttpClientFactory httpClientFactory,
    TimeProvider time) : IRequestHandler<ApproveImportItemCommand, ImportItemResponse?>
{
    public async Task<ImportItemResponse?> Handle(ApproveImportItemCommand request, CancellationToken cancellationToken)
    {
        var item = await db.ImportItems.Include(x => x.Batch)
            .SingleOrDefaultAsync(x => x.Id == request.ItemId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        await ImportApply.ApproveAndApplyAsync(db, storage, httpClientFactory, time, item, request.Note, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ImportMapping.ToItem(item);
    }
}

public sealed class RejectImportItemHandler(IApplicationDbContext db)
    : IRequestHandler<RejectImportItemCommand, ImportItemResponse?>
{
    public async Task<ImportItemResponse?> Handle(RejectImportItemCommand request, CancellationToken cancellationToken)
    {
        var item = await db.ImportItems.Include(x => x.Batch)
            .SingleOrDefaultAsync(x => x.Id == request.ItemId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        if (item.Batch.Status != ImportBatchStatus.ReadyForReview)
        {
            throw new InvalidOperationException("Batch is not ready for review.");
        }

        item.Reject(request.Note);
        item.Batch.RefreshCompletion();
        await db.SaveChangesAsync(cancellationToken);
        return ImportMapping.ToItem(item);
    }
}

public sealed class BulkApproveImportItemsHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    IHttpClientFactory httpClientFactory,
    TimeProvider time) : IRequestHandler<BulkApproveImportItemsCommand, IReadOnlyCollection<ImportItemResponse>>
{
    public async Task<IReadOnlyCollection<ImportItemResponse>> Handle(
        BulkApproveImportItemsCommand request, CancellationToken cancellationToken)
    {
        var items = await db.ImportItems.Include(x => x.Batch)
            .Where(x => request.ItemIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            await ImportApply.ApproveAndApplyAsync(db, storage, httpClientFactory, time, item, request.Note, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return items.Select(ImportMapping.ToItem).ToArray();
    }
}

public static class ImportMapping
{
    public static ImportItemResponse ToItem(ImportItem item) =>
        new(
            item.Id,
            item.BatchId,
            item.Status.ToString(),
            item.MatchHint.ToString(),
            item.Name,
            item.Slug,
            item.Sku,
            item.Size,
            item.StyleCode,
            item.TeamName,
            item.SeasonName,
            item.Quantity,
            item.ImageUrl,
            item.MatchedProductId,
            item.MatchedVariantId,
            item.AppliedProductId,
            item.AppliedVariantId,
            item.ReviewNote);

    public static ImportBatchDetailResponse ToDetail(ImportBatch batch) =>
        new(
            batch.Id,
            batch.SupplierId,
            batch.Supplier.Name,
            batch.FileName,
            batch.Status.ToString(),
            batch.ErrorSummary,
            batch.CorrelationId,
            batch.CreatedAtUtc,
            batch.Items.OrderBy(x => x.Sku).Select(ToItem).ToArray());
}

public static class ImportMatching
{
    public static async Task RefreshMatchAsync(
        IApplicationDbContext db, ImportItem item, CancellationToken cancellationToken)
    {
        var matches = await db.ProductVariants.AsNoTracking()
            .Where(x => x.Sku == item.Sku)
            .Select(x => new { x.Id, x.ProductId })
            .ToArrayAsync(cancellationToken);
        if (matches.Length == 0)
        {
            item.SetMatch(ImportMatchHint.New, null, null);
        }
        else if (matches.Length == 1)
        {
            item.SetMatch(ImportMatchHint.UpdateVariant, matches[0].ProductId, matches[0].Id);
        }
        else
        {
            item.SetMatch(ImportMatchHint.Ambiguous, null, null);
        }
    }
}

public static class ImportApply
{
    public static async Task ApproveAndApplyAsync(
        IApplicationDbContext db,
        IObjectStorage storage,
        IHttpClientFactory httpClientFactory,
        TimeProvider time,
        ImportItem item,
        string? note,
        CancellationToken cancellationToken)
    {
        if (item.Status == ImportItemStatus.Applied)
        {
            return;
        }

        if (item.Batch.Status != ImportBatchStatus.ReadyForReview && item.Batch.Status != ImportBatchStatus.Completed)
        {
            throw new InvalidOperationException("Batch is not ready for review.");
        }

        await ImportMatching.RefreshMatchAsync(db, item, cancellationToken);
        item.Approve(note);

        var now = time.GetUtcNow();
        var teamId = await EnsureTeamAsync(db, item.OrganizationId, item.TeamName, cancellationToken);
        var seasonId = await EnsureSeasonAsync(db, item.OrganizationId, item.SeasonName, cancellationToken);

        Product product;
        ProductVariant variant;
        if (item.MatchHint == ImportMatchHint.UpdateVariant && item.MatchedProductId is { } productId)
        {
            product = await db.Products
                .Include(x => x.Variants).ThenInclude(x => x.Inventory)
                .Include(x => x.Images)
                .Include(x => x.Categories)
                .Include(x => x.Tags)
                .SingleAsync(x => x.Id == productId, cancellationToken);
            product.UpdateDetails(item.Name, item.Slug, item.StyleCode, teamId, seasonId, now);
            variant = product.UpsertVariant(
                item.MatchedVariantId, item.Sku, item.Size, 0, now, ReadPriceAmount(item.RawJson));
        }
        else
        {
            Product? existing = null;
            if (!string.IsNullOrWhiteSpace(item.StyleCode))
            {
                existing = await db.Products
                    .Include(x => x.Variants).ThenInclude(x => x.Inventory)
                    .Include(x => x.Images)
                    .Include(x => x.Categories)
                    .Include(x => x.Tags)
                    .SingleOrDefaultAsync(x => x.StyleCode == item.StyleCode, cancellationToken);
            }

            existing ??= await db.Products
                .Include(x => x.Variants).ThenInclude(x => x.Inventory)
                .Include(x => x.Images)
                .Include(x => x.Categories)
                .Include(x => x.Tags)
                .SingleOrDefaultAsync(x => x.Slug == item.Slug, cancellationToken);

            if (existing is null)
            {
                product = new Product(item.OrganizationId, item.Name, item.Slug, item.StyleCode, teamId, seasonId, now);
                db.Add(product);
            }
            else
            {
                product = existing;
                product.UpdateDetails(item.Name, item.Slug, item.StyleCode, teamId, seasonId, now);
            }

            variant = product.UpsertVariant(
                null, item.Sku, item.Size, product.Variants.Count, now, ReadPriceAmount(item.RawJson));
        }

        if (item.Quantity is > 0)
        {
            var delta = item.Quantity.Value - variant.Inventory.OnHand;
            if (delta != 0)
            {
                variant.Inventory.Adjust(delta, "import approve", now);
            }
        }

        if (!string.IsNullOrWhiteSpace(item.ImageUrl) && product.Images.Count == 0)
        {
            await TryAttachImageAsync(storage, httpClientFactory, product, item.ImageUrl, now, cancellationToken);
        }

        item.MarkApplied(product.Id, variant.Id);
        item.Batch.RefreshCompletion();
    }

    private static decimal? ReadPriceAmount(string rawJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var name in new[] { "price", "price_amount", "Price", "PriceAmount" })
            {
                if (!document.RootElement.TryGetProperty(name, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) && number >= 0)
                {
                    return number;
                }

                if (value.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) &&
                    parsed >= 0)
                {
                    return parsed;
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed import payload; price stays unset.
        }

        return null;
    }

    private static async Task<Guid?> EnsureTeamAsync(
        IApplicationDbContext db, Guid orgId, string? teamName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return null;
        }

        var slug = ImportSlug.From(teamName);
        var existing = await db.Teams.SingleOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var team = new Team(orgId, teamName, slug);
        db.Add(team);
        return team.Id;
    }

    private static async Task<Guid?> EnsureSeasonAsync(
        IApplicationDbContext db, Guid orgId, string? seasonName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(seasonName))
        {
            return null;
        }

        var slug = ImportSlug.From(seasonName);
        var existing = await db.Seasons.SingleOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var season = new Season(orgId, seasonName, slug);
        db.Add(season);
        return season.Id;
    }

    private static async Task TryAttachImageAsync(
        IObjectStorage storage,
        IHttpClientFactory httpClientFactory,
        Product product,
        string imageUrl,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("import-images");
            using var response = await client.GetAsync(imageUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            var extension = contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
            var key = $"catalog/{product.OrganizationId:N}/{product.Id:N}/{Guid.NewGuid():N}{extension}";
            await storage.PutAsync(key, buffer, contentType, cancellationToken);
            product.AttachImage(key, contentType, product.Name, 0, now);
        }
        catch (Exception)
        {
            // Image attach is best-effort during import approve.
        }
    }
}

public sealed class CsvSupplierCatalogFeed : ISupplierCatalogFeed
{
    public string Format => "csv";

    public async Task<IReadOnlyList<SupplierCatalogRow>> ParseAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidOperationException("CSV is empty.");
        }

        var headers = SplitCsv(headerLine).Select(x => x.Trim().ToLowerInvariant()).ToArray();
        Require(headers, "style_code", "name", "sku", "size");
        var rows = new List<SupplierCatalogRow>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = SplitCsv(line);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length && i < cells.Count; i++)
            {
                map[headers[i]] = cells[i].Trim();
            }

            var qtyRaw = Get(map, "qty") ?? Get(map, "quantity");
            int? qty = null;
            if (!string.IsNullOrWhiteSpace(qtyRaw) &&
                int.TryParse(qtyRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                qty = parsed;
            }

            var priceRaw = Get(map, "price") ?? Get(map, "price_amount");
            decimal? price = null;
            if (!string.IsNullOrWhiteSpace(priceRaw) &&
                decimal.TryParse(priceRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedPrice) &&
                parsedPrice >= 0)
            {
                price = parsedPrice;
            }

            rows.Add(new SupplierCatalogRow(
                Get(map, "style_code") ?? string.Empty,
                Get(map, "name") ?? string.Empty,
                Get(map, "sku") ?? string.Empty,
                Get(map, "size") ?? string.Empty,
                Get(map, "team"),
                Get(map, "season"),
                qty,
                price,
                Get(map, "image_url") ?? Get(map, "imageurl"),
                map));
        }

        return rows;
    }

    private static void Require(IReadOnlyCollection<string> headers, params string[] required)
    {
        foreach (var column in required)
        {
            if (!headers.Contains(column))
            {
                throw new InvalidOperationException($"CSV is missing required column '{column}'.");
            }
        }
    }

    private static string? Get(Dictionary<string, string> map, string key) =>
        map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static List<string> SplitCsv(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values;
    }
}
