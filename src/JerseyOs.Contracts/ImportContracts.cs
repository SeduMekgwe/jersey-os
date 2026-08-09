namespace JerseyOs.Contracts;

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string Code,
    string FeedKind,
    string FeedFormat,
    string? FeedUrl,
    bool HasFeedAuth,
    string? ScrapeProfileJson,
    bool HasScrapeCredentials,
    string? SyncCron,
    DateTimeOffset? LastSyncAtUtc,
    string? LastSyncStatus,
    string? LastSyncError);

public sealed record CreateSupplierRequest(
    string Name,
    string Code,
    string? FeedKind = null,
    string? FeedFormat = null,
    string? FeedUrl = null,
    string? FeedBearerToken = null,
    string? ScrapeProfileJson = null,
    string? ScrapeUsername = null,
    string? ScrapePassword = null,
    string? SyncCron = null);

public sealed record UpdateSupplierFeedRequest(
    string FeedKind,
    string FeedFormat,
    string? FeedUrl,
    string? FeedBearerToken,
    string? ScrapeProfileJson,
    string? ScrapeUsername,
    string? ScrapePassword,
    string? SyncCron);

public sealed record SupplierScrapeRunResponse(
    Guid Id,
    Guid SupplierId,
    string Status,
    string CorrelationId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int ProductsScraped,
    Guid? ImportBatchId,
    string? Error,
    DateTimeOffset CreatedAtUtc);

public sealed record ImportBatchSummaryResponse(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string FileName,
    string Status,
    int ItemCount,
    int PendingCount,
    string? ErrorSummary,
    DateTimeOffset CreatedAtUtc);

public sealed record ImportItemResponse(
    Guid Id,
    Guid BatchId,
    string Status,
    string MatchHint,
    string Name,
    string Slug,
    string Sku,
    string Size,
    string? StyleCode,
    string? TeamName,
    string? SeasonName,
    int? Quantity,
    string? ImageUrl,
    Guid? MatchedProductId,
    Guid? MatchedVariantId,
    Guid? AppliedProductId,
    Guid? AppliedVariantId,
    string? ReviewNote);

public sealed record ImportBatchDetailResponse(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string FileName,
    string Status,
    string? ErrorSummary,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyCollection<ImportItemResponse> Items);

public sealed record UpdateImportItemRequest(
    string Name,
    string Slug,
    string Sku,
    string Size,
    string? StyleCode,
    string? TeamName,
    string? SeasonName,
    int? Quantity,
    string? ImageUrl);

public sealed record ReviewImportItemRequest(string? Note);
public sealed record BulkApproveImportItemsRequest(IReadOnlyCollection<Guid> ItemIds, string? Note);
