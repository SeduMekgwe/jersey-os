using JerseyOs.SharedKernel;

namespace JerseyOs.Domain;

public enum ImportBatchStatus
{
    Uploaded = 0,
    Parsing = 1,
    ReadyForReview = 2,
    Failed = 3,
    Completed = 4
}

public enum ImportItemStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Applied = 3
}

public enum ImportMatchHint
{
    New = 0,
    UpdateVariant = 1,
    Ambiguous = 2
}

public static class SupplierFeedKinds
{
    public const string Upload = "upload";
    public const string Http = "http";
}

public static class SupplierFeedFormats
{
    public const string Csv = "csv";
    public const string Json = "json";
}

public sealed class Supplier : AuditableEntity, IOrganizationScoped
{
    private Supplier() { }

    public Supplier(Guid organizationId, string name, string code)
    {
        OrganizationId = organizationId;
        Rename(name, code);
        FeedKind = SupplierFeedKinds.Upload;
        FeedFormat = SupplierFeedFormats.Csv;
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string FeedKind { get; private set; } = SupplierFeedKinds.Upload;
    public string FeedFormat { get; private set; } = SupplierFeedFormats.Csv;
    public string? FeedUrl { get; private set; }
    public string? FeedBearerToken { get; private set; }
    public string? SyncCron { get; private set; }
    public DateTimeOffset? LastSyncAtUtc { get; private set; }
    public string? LastSyncStatus { get; private set; }
    public string? LastSyncError { get; private set; }

    public void Rename(string name, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Name = name.Trim();
        Code = code.Trim().ToLowerInvariant();
    }

    public void ConfigureUploadFeed(string contentFormat = SupplierFeedFormats.Csv)
    {
        FeedKind = SupplierFeedKinds.Upload;
        FeedFormat = NormalizeFormat(contentFormat);
        FeedUrl = null;
        FeedBearerToken = null;
        SyncCron = null;
    }

    public void ConfigureHttpFeed(
        string feedUrl,
        string contentFormat,
        string? bearerToken,
        string? syncCron)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedUrl);
        if (!Uri.TryCreate(feedUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Feed URL must be an absolute http(s) URL.");
        }

        FeedKind = SupplierFeedKinds.Http;
        FeedUrl = uri.ToString();
        FeedFormat = NormalizeFormat(contentFormat);
        FeedBearerToken = string.IsNullOrWhiteSpace(bearerToken) ? null : bearerToken.Trim();
        SyncCron = string.IsNullOrWhiteSpace(syncCron) ? null : syncCron.Trim();
    }

    public void RecordSyncSucceeded(DateTimeOffset now)
    {
        LastSyncAtUtc = now;
        LastSyncStatus = "Succeeded";
        LastSyncError = null;
    }

    public void RecordSyncFailed(string error, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        LastSyncAtUtc = now;
        LastSyncStatus = "Failed";
        LastSyncError = error.Trim();
    }

    private static string NormalizeFormat(string contentFormat)
    {
        var normalized = string.IsNullOrWhiteSpace(contentFormat)
            ? SupplierFeedFormats.Csv
            : contentFormat.Trim().ToLowerInvariant();
        if (normalized is not (SupplierFeedFormats.Csv or SupplierFeedFormats.Json))
        {
            throw new InvalidOperationException("Feed format must be 'csv' or 'json'.");
        }

        return normalized;
    }
}

public sealed class ImportBatch : AuditableEntity, IOrganizationScoped
{
    private readonly List<ImportItem> _items = [];

    private ImportBatch() { }

    public ImportBatch(
        Guid organizationId,
        Guid supplierId,
        string fileName,
        string objectKey,
        string contentType,
        string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        OrganizationId = organizationId;
        SupplierId = supplierId;
        FileName = fileName.Trim();
        ObjectKey = objectKey.Trim();
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "text/csv" : contentType.Trim();
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId;
        Status = ImportBatchStatus.Uploaded;
    }

    public Guid OrganizationId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ObjectKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public ImportBatchStatus Status { get; private set; }
    public string? ErrorSummary { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public Supplier Supplier { get; private set; } = null!;
    public IReadOnlyCollection<ImportItem> Items => _items.AsReadOnly();

    public void MarkParsing()
    {
        EnsureMutable();
        Status = ImportBatchStatus.Parsing;
        ErrorSummary = null;
    }

    public void MarkReadyForReview()
    {
        if (Status is not (ImportBatchStatus.Parsing or ImportBatchStatus.Uploaded))
        {
            throw new InvalidOperationException("Only parsing batches can become ready for review.");
        }

        Status = ImportBatchStatus.ReadyForReview;
        ErrorSummary = null;
    }

    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Status = ImportBatchStatus.Failed;
        ErrorSummary = error.Trim();
    }

    public ImportItem AddItem(
        string rawJson,
        string name,
        string slug,
        string sku,
        string size,
        string? styleCode,
        string? teamName,
        string? seasonName,
        int? quantity,
        string? imageUrl,
        ImportMatchHint matchHint,
        Guid? matchedProductId,
        Guid? matchedVariantId)
    {
        if (Status is not (ImportBatchStatus.Parsing or ImportBatchStatus.ReadyForReview))
        {
            throw new InvalidOperationException("Items can only be added while parsing.");
        }

        var item = new ImportItem(
            OrganizationId,
            Id,
            rawJson,
            name,
            slug,
            sku,
            size,
            styleCode,
            teamName,
            seasonName,
            quantity,
            imageUrl,
            matchHint,
            matchedProductId,
            matchedVariantId);
        _items.Add(item);
        return item;
    }

    public void RefreshCompletion()
    {
        if (Status is ImportBatchStatus.Failed or ImportBatchStatus.Uploaded or ImportBatchStatus.Parsing)
        {
            return;
        }

        if (_items.Count == 0)
        {
            return;
        }

        if (_items.All(x => x.Status is ImportItemStatus.Applied or ImportItemStatus.Rejected))
        {
            Status = ImportBatchStatus.Completed;
        }
        else
        {
            Status = ImportBatchStatus.ReadyForReview;
        }
    }

    private void EnsureMutable()
    {
        if (Status is ImportBatchStatus.Completed or ImportBatchStatus.Failed)
        {
            throw new InvalidOperationException("Completed or failed batches cannot be modified.");
        }
    }
}

public sealed class ImportItem : AuditableEntity, IOrganizationScoped
{
    private ImportItem() { }

    public ImportItem(
        Guid organizationId,
        Guid batchId,
        string rawJson,
        string name,
        string slug,
        string sku,
        string size,
        string? styleCode,
        string? teamName,
        string? seasonName,
        int? quantity,
        string? imageUrl,
        ImportMatchHint matchHint,
        Guid? matchedProductId,
        Guid? matchedVariantId)
    {
        OrganizationId = organizationId;
        BatchId = batchId;
        RawJson = rawJson;
        ApplyProposed(name, slug, sku, size, styleCode, teamName, seasonName, quantity, imageUrl);
        MatchHint = matchHint;
        MatchedProductId = matchedProductId;
        MatchedVariantId = matchedVariantId;
        Status = ImportItemStatus.Pending;
    }

    public Guid OrganizationId { get; private set; }
    public Guid BatchId { get; private set; }
    public string RawJson { get; private set; } = "{}";
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public string Size { get; private set; } = string.Empty;
    public string? StyleCode { get; private set; }
    public string? TeamName { get; private set; }
    public string? SeasonName { get; private set; }
    public int? Quantity { get; private set; }
    public string? ImageUrl { get; private set; }
    public ImportItemStatus Status { get; private set; }
    public ImportMatchHint MatchHint { get; private set; }
    public Guid? MatchedProductId { get; private set; }
    public Guid? MatchedVariantId { get; private set; }
    public Guid? AppliedProductId { get; private set; }
    public Guid? AppliedVariantId { get; private set; }
    public string? ReviewNote { get; private set; }
    public ImportBatch Batch { get; private set; } = null!;

    public void UpdateProposed(
        string name,
        string slug,
        string sku,
        string size,
        string? styleCode,
        string? teamName,
        string? seasonName,
        int? quantity,
        string? imageUrl)
    {
        EnsurePending();
        ApplyProposed(name, slug, sku, size, styleCode, teamName, seasonName, quantity, imageUrl);
    }

    public void SetMatch(ImportMatchHint hint, Guid? productId, Guid? variantId)
    {
        EnsurePending();
        MatchHint = hint;
        MatchedProductId = productId;
        MatchedVariantId = variantId;
    }

    public void Approve(string? note = null)
    {
        if (Status == ImportItemStatus.Applied)
        {
            return;
        }

        EnsurePendingOrApproved();
        if (MatchHint == ImportMatchHint.Ambiguous)
        {
            throw new InvalidOperationException("Ambiguous SKU matches must be resolved before approve.");
        }

        Status = ImportItemStatus.Approved;
        ReviewNote = note?.Trim();
    }

    public void Reject(string? note = null)
    {
        if (Status == ImportItemStatus.Applied)
        {
            throw new InvalidOperationException("Applied items cannot be rejected.");
        }

        Status = ImportItemStatus.Rejected;
        ReviewNote = note?.Trim();
    }

    public void MarkApplied(Guid productId, Guid variantId)
    {
        if (Status == ImportItemStatus.Applied)
        {
            return;
        }

        if (Status is not (ImportItemStatus.Approved or ImportItemStatus.Pending))
        {
            throw new InvalidOperationException("Only approved items can be applied.");
        }

        Status = ImportItemStatus.Applied;
        AppliedProductId = productId;
        AppliedVariantId = variantId;
    }

    private void EnsurePending()
    {
        if (Status != ImportItemStatus.Pending)
        {
            throw new InvalidOperationException("Only pending items can be edited.");
        }
    }

    private void EnsurePendingOrApproved()
    {
        if (Status is not (ImportItemStatus.Pending or ImportItemStatus.Approved))
        {
            throw new InvalidOperationException("Item is not reviewable.");
        }
    }

    private void ApplyProposed(
        string name,
        string slug,
        string sku,
        string size,
        string? styleCode,
        string? teamName,
        string? seasonName,
        int? quantity,
        string? imageUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(size);
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Sku = sku.Trim().ToUpperInvariant();
        Size = size.Trim();
        StyleCode = string.IsNullOrWhiteSpace(styleCode) ? null : styleCode.Trim();
        TeamName = string.IsNullOrWhiteSpace(teamName) ? null : teamName.Trim();
        SeasonName = string.IsNullOrWhiteSpace(seasonName) ? null : seasonName.Trim();
        Quantity = quantity;
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
    }
}
