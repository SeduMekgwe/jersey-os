namespace JerseyOs.Contracts;

public sealed record TaxonomyItemResponse(Guid Id, string Name, string Slug, Guid? ParentId = null);
public sealed record CreateTaxonomyItemRequest(string Name, string Slug, Guid? ParentId = null);
public sealed record UpdateTaxonomyItemRequest(string Name, string Slug, Guid? ParentId = null);

public sealed record InventoryResponse(Guid VariantId, int OnHand, int Reserved, string RowVersion);
public sealed record ProductVariantResponse(
    Guid Id,
    string Sku,
    string Size,
    int SortOrder,
    decimal? PriceAmount,
    decimal? CostAmount,
    decimal? CompareAtAmount,
    InventoryResponse Inventory);
public sealed record ProductImageResponse(
    Guid Id,
    string ObjectKey,
    string Url,
    string ContentType,
    string? AltText,
    int SortOrder);
public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Slug,
    string? StyleCode,
    string Status,
    string Currency,
    Guid? TeamId,
    Guid? SeasonId,
    string? SeoTitle,
    string? SeoDescription,
    string? SeoHandle,
    IReadOnlyCollection<Guid> CategoryIds,
    IReadOnlyCollection<Guid> TagIds,
    IReadOnlyCollection<Guid> CollectionIds,
    IReadOnlyCollection<ProductVariantResponse> Variants,
    IReadOnlyCollection<ProductImageResponse> Images);
public sealed record ProductSummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? StyleCode,
    string Status,
    Guid? TeamId,
    Guid? SeasonId,
    int VariantCount);
public sealed record PagedProductsResponse(
    IReadOnlyCollection<ProductSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CreateProductRequest(
    string Name,
    string Slug,
    string? StyleCode,
    Guid? TeamId,
    Guid? SeasonId,
    IReadOnlyCollection<Guid>? CategoryIds,
    IReadOnlyCollection<Guid>? TagIds,
    string? SeoTitle = null,
    string? SeoDescription = null,
    string? SeoHandle = null);
public sealed record UpdateProductRequest(
    string Name,
    string Slug,
    string? StyleCode,
    Guid? TeamId,
    Guid? SeasonId,
    IReadOnlyCollection<Guid>? CategoryIds,
    IReadOnlyCollection<Guid>? TagIds,
    string? SeoTitle = null,
    string? SeoDescription = null,
    string? SeoHandle = null);
public sealed record UpsertVariantRequest(
    Guid? Id,
    string Sku,
    string Size,
    int SortOrder,
    decimal? PriceAmount,
    decimal? CostAmount = null,
    decimal? CompareAtAmount = null);
public sealed record ReorderImagesRequest(IReadOnlyCollection<ImageSortOrder> Items);
public sealed record ImageSortOrder(Guid ImageId, int SortOrder);
public sealed record AdjustInventoryRequest(int DeltaOnHand, string Reason, string? ExpectedRowVersion);

public sealed record PricingRuleResponse(
    Guid Id,
    string Name,
    string Kind,
    decimal PercentRate,
    Guid? SalesChannelId,
    int Priority,
    bool IsEnabled);
public sealed record CreatePricingRuleRequest(
    string Name,
    string Kind,
    decimal PercentRate,
    int Priority,
    Guid? SalesChannelId,
    bool IsEnabled = true);
public sealed record UpdatePricingRuleRequest(
    string Name,
    string Kind,
    decimal PercentRate,
    int Priority,
    Guid? SalesChannelId,
    bool IsEnabled);
public sealed record PricePreviewRequest(
    decimal? CostAmount,
    decimal? ExplicitPriceAmount,
    decimal? ExplicitCompareAtAmount,
    Guid? SalesChannelId);
public sealed record PricePreviewResponse(
    decimal? PriceAmount,
    decimal? CompareAtAmount,
    Guid? SellRuleId,
    Guid? CompareAtRuleId);

public sealed record CollectionResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string MembershipKind,
    Guid? TeamId,
    Guid? SeasonId,
    Guid? CategoryId,
    Guid? TagId,
    IReadOnlyCollection<Guid> ProductIds,
    int MemberCount);
public sealed record CreateCollectionRequest(
    string Name,
    string Slug,
    string MembershipKind,
    string? Description = null,
    Guid? TeamId = null,
    Guid? SeasonId = null,
    Guid? CategoryId = null,
    Guid? TagId = null,
    IReadOnlyCollection<Guid>? ProductIds = null);
public sealed record UpdateCollectionRequest(
    string Name,
    string Slug,
    string? Description,
    Guid? TeamId,
    Guid? SeasonId,
    Guid? CategoryId,
    Guid? TagId,
    IReadOnlyCollection<Guid>? ProductIds);
