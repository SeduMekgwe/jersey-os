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
    Guid? TeamId,
    Guid? SeasonId,
    IReadOnlyCollection<Guid> CategoryIds,
    IReadOnlyCollection<Guid> TagIds,
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
    IReadOnlyCollection<Guid>? TagIds);
public sealed record UpdateProductRequest(
    string Name,
    string Slug,
    string? StyleCode,
    Guid? TeamId,
    Guid? SeasonId,
    IReadOnlyCollection<Guid>? CategoryIds,
    IReadOnlyCollection<Guid>? TagIds);
public sealed record UpsertVariantRequest(Guid? Id, string Sku, string Size, int SortOrder);
public sealed record ReorderImagesRequest(IReadOnlyCollection<ImageSortOrder> Items);
public sealed record ImageSortOrder(Guid ImageId, int SortOrder);
public sealed record AdjustInventoryRequest(int DeltaOnHand, string Reason, string? ExpectedRowVersion);
