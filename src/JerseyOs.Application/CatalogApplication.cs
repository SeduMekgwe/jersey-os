using FluentValidation;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JerseyOs.Application;

public interface IApplicationDbContext
{
    IQueryable<Organization> Organizations { get; }
    IQueryable<RefreshTokenSession> RefreshTokenSessions { get; }
    IQueryable<OutboxMessage> OutboxMessages { get; }
    IQueryable<Product> Products { get; }
    IQueryable<ProductVariant> ProductVariants { get; }
    IQueryable<InventoryLevel> InventoryLevels { get; }
    IQueryable<Team> Teams { get; }
    IQueryable<Season> Seasons { get; }
    IQueryable<Category> Categories { get; }
    IQueryable<Tag> Tags { get; }
    IQueryable<AuditLog> AuditLogs { get; }
    IQueryable<Supplier> Suppliers { get; }
    IQueryable<ImportBatch> ImportBatches { get; }
    IQueryable<ImportItem> ImportItems { get; }
    IQueryable<SupplierScrapeRun> SupplierScrapeRuns { get; }
    IQueryable<SalesChannel> SalesChannels { get; }
    IQueryable<ExternalIdMap> ExternalIdMaps { get; }
    IQueryable<PublishRun> PublishRuns { get; }
    IQueryable<WebhookDelivery> WebhookDeliveries { get; }

    void Add<TEntity>(TEntity entity) where TEntity : class;
    void Remove<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public static class CatalogMapping
{
    public static ProductResponse ToResponse(Product product, IObjectStorage storage, string currency) =>
        new(
            product.Id,
            product.Name,
            product.Slug,
            product.StyleCode,
            product.Status.ToString(),
            currency,
            product.TeamId,
            product.SeasonId,
            product.Categories.Select(x => x.CategoryId).ToArray(),
            product.Tags.Select(x => x.TagId).ToArray(),
            product.Variants
                .OrderBy(x => x.SortOrder)
                .Select(v => new ProductVariantResponse(
                    v.Id,
                    v.Sku,
                    v.Size,
                    v.SortOrder,
                    v.PriceAmount,
                    new InventoryResponse(
                        v.Inventory.VariantId,
                        v.Inventory.OnHand,
                        v.Inventory.Reserved,
                        Convert.ToBase64String(v.Inventory.RowVersion))))
                .ToArray(),
            product.Images
                .OrderBy(x => x.SortOrder)
                .Select(i => new ProductImageResponse(
                    i.Id,
                    i.ObjectKey,
                    storage.GetUrl(i.ObjectKey),
                    i.ContentType,
                    i.AltText,
                    i.SortOrder))
                .ToArray());

    public static ProductSummaryResponse ToSummary(Product product) =>
        new(
            product.Id,
            product.Name,
            product.Slug,
            product.StyleCode,
            product.Status.ToString(),
            product.TeamId,
            product.SeasonId,
            product.Variants.Count);

    public static async Task<ProductResponse> ToResponseAsync(
        Product product, IObjectStorage storage, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var currency = await CatalogHandlerSupport.GetCurrencyAsync(db, cancellationToken).ConfigureAwait(false);
        return ToResponse(product, storage, currency);
    }
}

public sealed record CreateProductCommand(
    string Name,
    string Slug,
    string? StyleCode,
    Guid? TeamId,
    Guid? SeasonId,
    IReadOnlyCollection<Guid>? CategoryIds,
    IReadOnlyCollection<Guid>? TagIds) : IRequest<ProductResponse>;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string Slug,
    string? StyleCode,
    Guid? TeamId,
    Guid? SeasonId,
    IReadOnlyCollection<Guid>? CategoryIds,
    IReadOnlyCollection<Guid>? TagIds) : IRequest<ProductResponse?>;

public sealed record ActivateProductCommand(Guid ProductId) : IRequest<ProductResponse?>;
public sealed record ArchiveProductCommand(Guid ProductId) : IRequest<ProductResponse?>;
public sealed record GetProductQuery(Guid ProductId) : IRequest<ProductResponse?>;
public sealed record SearchProductsQuery(
    string? Search,
    string? Status,
    Guid? TeamId,
    Guid? SeasonId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedProductsResponse>;

public sealed record UpsertVariantCommand(
    Guid ProductId,
    Guid? VariantId,
    string Sku,
    string Size,
    int SortOrder,
    decimal? PriceAmount) : IRequest<ProductResponse?>;

public sealed record RemoveVariantCommand(Guid ProductId, Guid VariantId) : IRequest<ProductResponse?>;
public sealed record AdjustInventoryCommand(
    Guid VariantId,
    int DeltaOnHand,
    string Reason,
    string? ExpectedRowVersion) : IRequest<InventoryResponse?>;

public sealed record AttachProductImageCommand(
    Guid ProductId,
    Stream Content,
    string FileName,
    string ContentType,
    string? AltText,
    int SortOrder) : IRequest<ProductResponse?>;

public sealed record ReorderProductImagesCommand(
    Guid ProductId,
    IReadOnlyCollection<ImageSortOrder> Items) : IRequest<ProductResponse?>;

public sealed record RemoveProductImageCommand(Guid ProductId, Guid ImageId) : IRequest<ProductResponse?>;

public sealed record CreateTeamCommand(string Name, string Slug) : IRequest<TaxonomyItemResponse>;
public sealed record UpdateTeamCommand(Guid Id, string Name, string Slug) : IRequest<TaxonomyItemResponse?>;
public sealed record ListTeamsQuery : IRequest<IReadOnlyCollection<TaxonomyItemResponse>>;
public sealed record CreateSeasonCommand(string Name, string Slug) : IRequest<TaxonomyItemResponse>;
public sealed record UpdateSeasonCommand(Guid Id, string Name, string Slug) : IRequest<TaxonomyItemResponse?>;
public sealed record ListSeasonsQuery : IRequest<IReadOnlyCollection<TaxonomyItemResponse>>;
public sealed record CreateCategoryCommand(string Name, string Slug, Guid? ParentId) : IRequest<TaxonomyItemResponse>;
public sealed record UpdateCategoryCommand(Guid Id, string Name, string Slug, Guid? ParentId) : IRequest<TaxonomyItemResponse?>;
public sealed record ListCategoriesQuery : IRequest<IReadOnlyCollection<TaxonomyItemResponse>>;
public sealed record CreateTagCommand(string Name, string Slug) : IRequest<TaxonomyItemResponse>;
public sealed record UpdateTagCommand(Guid Id, string Name, string Slug) : IRequest<TaxonomyItemResponse?>;
public sealed record ListTagsQuery : IRequest<IReadOnlyCollection<TaxonomyItemResponse>>;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.StyleCode).MaximumLength(64);
    }
}

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.StyleCode).MaximumLength(64);
    }
}

public sealed class UpsertVariantValidator : AbstractValidator<UpsertVariantCommand>
{
    public UpsertVariantValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Size).NotEmpty().MaximumLength(32);
        RuleFor(x => x.PriceAmount).GreaterThanOrEqualTo(0).When(x => x.PriceAmount is not null);
    }
}

public sealed class AdjustInventoryValidator : AbstractValidator<AdjustInventoryCommand>
{
    public AdjustInventoryValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.DeltaOnHand).NotEqual(0);
    }
}

public sealed class CreateProductHandler(
    IApplicationDbContext db,
    ICurrentRequest current,
    IObjectStorage storage,
    TimeProvider time) : IRequestHandler<CreateProductCommand, ProductResponse>
{
    public async Task<ProductResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var orgId = CatalogHandlerSupport.RequireOrganization(current);
        await CatalogHandlerSupport.EnsureSlugAvailable(db, request.Slug, null, cancellationToken).ConfigureAwait(false);
        await CatalogHandlerSupport.EnsureTaxonomyExists(db, request.TeamId, request.SeasonId, request.CategoryIds, request.TagIds, cancellationToken)
            .ConfigureAwait(false);

        var product = new Product(
            orgId,
            request.Name,
            request.Slug,
            request.StyleCode,
            request.TeamId,
            request.SeasonId,
            time.GetUtcNow());
        if (request.CategoryIds is { Count: > 0 })
        {
            product.SetCategories(request.CategoryIds, time.GetUtcNow());
        }

        if (request.TagIds is { Count: > 0 })
        {
            product.SetTags(request.TagIds, time.GetUtcNow());
        }

        db.Add(product);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await CatalogMapping.ToResponseAsync(product, storage, db, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UpdateProductHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    TimeProvider time) : IRequestHandler<UpdateProductCommand, ProductResponse?>
{
    public async Task<ProductResponse?> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await CatalogHandlerSupport.LoadProduct(db, request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        await CatalogHandlerSupport.EnsureSlugAvailable(db, request.Slug, product.Id, cancellationToken).ConfigureAwait(false);
        await CatalogHandlerSupport.EnsureTaxonomyExists(db, request.TeamId, request.SeasonId, request.CategoryIds, request.TagIds, cancellationToken)
            .ConfigureAwait(false);
        product.UpdateDetails(
            request.Name,
            request.Slug,
            request.StyleCode,
            request.TeamId,
            request.SeasonId,
            time.GetUtcNow());
        product.SetCategories(request.CategoryIds ?? [], time.GetUtcNow());
        product.SetTags(request.TagIds ?? [], time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await CatalogMapping.ToResponseAsync(product, storage, db, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ActivateProductHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    TimeProvider time) : IRequestHandler<ActivateProductCommand, ProductResponse?>
{
    public async Task<ProductResponse?> Handle(ActivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await CatalogHandlerSupport.LoadProduct(db, request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        product.Activate(time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await CatalogMapping.ToResponseAsync(product, storage, db, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ArchiveProductHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    TimeProvider time) : IRequestHandler<ArchiveProductCommand, ProductResponse?>
{
    public async Task<ProductResponse?> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await CatalogHandlerSupport.LoadProduct(db, request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        product.Archive(time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await CatalogMapping.ToResponseAsync(product, storage, db, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class GetProductHandler(IApplicationDbContext db, IObjectStorage storage)
    : IRequestHandler<GetProductQuery, ProductResponse?>
{
    public async Task<ProductResponse?> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await CatalogHandlerSupport.LoadProduct(db, request.ProductId, cancellationToken).ConfigureAwait(false);
        return product is null
            ? null
            : await CatalogMapping.ToResponseAsync(product, storage, db, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class SearchProductsHandler(IApplicationDbContext db)
    : IRequestHandler<SearchProductsQuery, PagedProductsResponse>
{
    public async Task<PagedProductsResponse> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x =>
                x.Name.Contains(term) ||
                x.Slug.Contains(term) ||
                (x.StyleCode != null && x.StyleCode.Contains(term)) ||
                x.Variants.Any(v => v.Sku.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<ProductStatus>(request.Status, true, out var status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (request.TeamId is { } teamId)
        {
            query = query.Where(x => x.TeamId == teamId);
        }

        if (request.SeasonId is { } seasonId)
        {
            query = query.Where(x => x.SeasonId == seasonId);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductSummaryResponse(
                x.Id,
                x.Name,
                x.Slug,
                x.StyleCode,
                x.Status.ToString(),
                x.TeamId,
                x.SeasonId,
                x.Variants.Count))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedProductsResponse(items, page, pageSize, total);
    }
}

public sealed class UpsertVariantHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    TimeProvider time) : IRequestHandler<UpsertVariantCommand, ProductResponse?>
{
    public async Task<ProductResponse?> Handle(UpsertVariantCommand request, CancellationToken cancellationToken)
    {
        var product = await CatalogHandlerSupport.LoadProduct(db, request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        var sku = request.Sku.Trim().ToUpperInvariant();
        var conflict = await db.ProductVariants
            .AnyAsync(
                x => x.Sku == sku && (request.VariantId == null || x.Id != request.VariantId),
                cancellationToken)
            .ConfigureAwait(false);
        if (conflict)
        {
            throw new InvalidOperationException($"SKU '{sku}' is already in use.");
        }

        product.UpsertVariant(
            request.VariantId,
            request.Sku,
            request.Size,
            request.SortOrder,
            time.GetUtcNow(),
            request.PriceAmount);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await CatalogMapping.ToResponseAsync(product, storage, db, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class RemoveVariantHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    TimeProvider time) : IRequestHandler<RemoveVariantCommand, ProductResponse?>
{
    public async Task<ProductResponse?> Handle(RemoveVariantCommand request, CancellationToken cancellationToken)
    {
        var product = await CatalogHandlerSupport.LoadProduct(db, request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        var variant = product.Variants.SingleOrDefault(x => x.Id == request.VariantId);
        if (variant is null)
        {
            return null;
        }

        db.Remove(variant.Inventory);
        product.RemoveVariant(request.VariantId, time.GetUtcNow());
        db.Remove(variant);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await CatalogMapping.ToResponseAsync(product, storage, db, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class AdjustInventoryHandler(
    IApplicationDbContext db,
    ICurrentRequest current,
    TimeProvider time) : IRequestHandler<AdjustInventoryCommand, InventoryResponse?>
{
    public async Task<InventoryResponse?> Handle(AdjustInventoryCommand request, CancellationToken cancellationToken)
    {
        var inventory = await db.InventoryLevels
            .SingleOrDefaultAsync(x => x.VariantId == request.VariantId, cancellationToken)
            .ConfigureAwait(false);
        if (inventory is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedRowVersion))
        {
            var expected = Convert.FromBase64String(request.ExpectedRowVersion);
            if (!inventory.RowVersion.SequenceEqual(expected))
            {
                throw new DbUpdateConcurrencyException("Inventory was modified by another request.");
            }
        }

        inventory.Adjust(request.DeltaOnHand, request.Reason, time.GetUtcNow());
        db.Add(new AuditLog
        {
            OrganizationId = inventory.OrganizationId,
            Action = "inventory.adjust",
            EntityType = nameof(InventoryLevel),
            EntityId = inventory.VariantId.ToString("N"),
            DataJson = $"{{\"delta\":{request.DeltaOnHand},\"onHand\":{inventory.OnHand},\"reason\":{System.Text.Json.JsonSerializer.Serialize(request.Reason)}}}",
            CorrelationId = current.CorrelationId
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new InventoryResponse(
            inventory.VariantId,
            inventory.OnHand,
            inventory.Reserved,
            Convert.ToBase64String(inventory.RowVersion));
    }
}

public sealed class AttachProductImageHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    TimeProvider time) : IRequestHandler<AttachProductImageCommand, ProductResponse?>
{
    public async Task<ProductResponse?> Handle(AttachProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await CatalogHandlerSupport.LoadProduct(db, request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        var extension = Path.GetExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var key = $"catalog/{product.OrganizationId:N}/{product.Id:N}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await storage.PutAsync(key, request.Content, request.ContentType, cancellationToken).ConfigureAwait(false);
        product.AttachImage(key, request.ContentType, request.AltText, request.SortOrder, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await CatalogMapping.ToResponseAsync(product, storage, db, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ReorderProductImagesHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    TimeProvider time) : IRequestHandler<ReorderProductImagesCommand, ProductResponse?>
{
    public async Task<ProductResponse?> Handle(ReorderProductImagesCommand request, CancellationToken cancellationToken)
    {
        var product = await CatalogHandlerSupport.LoadProduct(db, request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        product.ReorderImages(request.Items.ToDictionary(x => x.ImageId, x => x.SortOrder), time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await CatalogMapping.ToResponseAsync(product, storage, db, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class RemoveProductImageHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    TimeProvider time) : IRequestHandler<RemoveProductImageCommand, ProductResponse?>
{
    public async Task<ProductResponse?> Handle(RemoveProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await CatalogHandlerSupport.LoadProduct(db, request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        var image = product.RemoveImage(request.ImageId, time.GetUtcNow());
        db.Remove(image);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await storage.DeleteAsync(image.ObjectKey, cancellationToken).ConfigureAwait(false);
        return await CatalogMapping.ToResponseAsync(product, storage, db, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class CreateTeamHandler(IApplicationDbContext db, ICurrentRequest current)
    : IRequestHandler<CreateTeamCommand, TaxonomyItemResponse>
{
    public async Task<TaxonomyItemResponse> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = new Team(CatalogHandlerSupport.RequireOrganization(current), request.Name, request.Slug);
        db.Add(team);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TaxonomyItemResponse(team.Id, team.Name, team.Slug);
    }
}

public sealed class UpdateTeamHandler(IApplicationDbContext db) : IRequestHandler<UpdateTeamCommand, TaxonomyItemResponse?>
{
    public async Task<TaxonomyItemResponse?> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken).ConfigureAwait(false);
        if (team is null)
        {
            return null;
        }

        team.Rename(request.Name, request.Slug);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TaxonomyItemResponse(team.Id, team.Name, team.Slug);
    }
}

public sealed class ListTeamsHandler(IApplicationDbContext db)
    : IRequestHandler<ListTeamsQuery, IReadOnlyCollection<TaxonomyItemResponse>>
{
    public async Task<IReadOnlyCollection<TaxonomyItemResponse>> Handle(
        ListTeamsQuery request, CancellationToken cancellationToken) =>
        await db.Teams.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new TaxonomyItemResponse(x.Id, x.Name, x.Slug))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
}

public sealed class CreateSeasonHandler(IApplicationDbContext db, ICurrentRequest current)
    : IRequestHandler<CreateSeasonCommand, TaxonomyItemResponse>
{
    public async Task<TaxonomyItemResponse> Handle(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        var season = new Season(CatalogHandlerSupport.RequireOrganization(current), request.Name, request.Slug);
        db.Add(season);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TaxonomyItemResponse(season.Id, season.Name, season.Slug);
    }
}

public sealed class UpdateSeasonHandler(IApplicationDbContext db) : IRequestHandler<UpdateSeasonCommand, TaxonomyItemResponse?>
{
    public async Task<TaxonomyItemResponse?> Handle(UpdateSeasonCommand request, CancellationToken cancellationToken)
    {
        var season = await db.Seasons.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken).ConfigureAwait(false);
        if (season is null)
        {
            return null;
        }

        season.Rename(request.Name, request.Slug);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TaxonomyItemResponse(season.Id, season.Name, season.Slug);
    }
}

public sealed class ListSeasonsHandler(IApplicationDbContext db)
    : IRequestHandler<ListSeasonsQuery, IReadOnlyCollection<TaxonomyItemResponse>>
{
    public async Task<IReadOnlyCollection<TaxonomyItemResponse>> Handle(
        ListSeasonsQuery request, CancellationToken cancellationToken) =>
        await db.Seasons.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new TaxonomyItemResponse(x.Id, x.Name, x.Slug))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
}

public sealed class CreateCategoryHandler(IApplicationDbContext db, ICurrentRequest current)
    : IRequestHandler<CreateCategoryCommand, TaxonomyItemResponse>
{
    public async Task<TaxonomyItemResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = new Category(CatalogHandlerSupport.RequireOrganization(current), request.Name, request.Slug, request.ParentId);
        db.Add(category);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TaxonomyItemResponse(category.Id, category.Name, category.Slug, category.ParentId);
    }
}

public sealed class UpdateCategoryHandler(IApplicationDbContext db) : IRequestHandler<UpdateCategoryCommand, TaxonomyItemResponse?>
{
    public async Task<TaxonomyItemResponse?> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);
        if (category is null)
        {
            return null;
        }

        category.Rename(request.Name, request.Slug);
        category.SetParent(request.ParentId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TaxonomyItemResponse(category.Id, category.Name, category.Slug, category.ParentId);
    }
}

public sealed class ListCategoriesHandler(IApplicationDbContext db)
    : IRequestHandler<ListCategoriesQuery, IReadOnlyCollection<TaxonomyItemResponse>>
{
    public async Task<IReadOnlyCollection<TaxonomyItemResponse>> Handle(
        ListCategoriesQuery request, CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new TaxonomyItemResponse(x.Id, x.Name, x.Slug, x.ParentId))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
}

public sealed class CreateTagHandler(IApplicationDbContext db, ICurrentRequest current)
    : IRequestHandler<CreateTagCommand, TaxonomyItemResponse>
{
    public async Task<TaxonomyItemResponse> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        var tag = new Tag(CatalogHandlerSupport.RequireOrganization(current), request.Name, request.Slug);
        db.Add(tag);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TaxonomyItemResponse(tag.Id, tag.Name, tag.Slug);
    }
}

public sealed class UpdateTagHandler(IApplicationDbContext db) : IRequestHandler<UpdateTagCommand, TaxonomyItemResponse?>
{
    public async Task<TaxonomyItemResponse?> Handle(UpdateTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await db.Tags.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken).ConfigureAwait(false);
        if (tag is null)
        {
            return null;
        }

        tag.Rename(request.Name, request.Slug);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TaxonomyItemResponse(tag.Id, tag.Name, tag.Slug);
    }
}

public sealed class ListTagsHandler(IApplicationDbContext db)
    : IRequestHandler<ListTagsQuery, IReadOnlyCollection<TaxonomyItemResponse>>
{
    public async Task<IReadOnlyCollection<TaxonomyItemResponse>> Handle(
        ListTagsQuery request, CancellationToken cancellationToken) =>
        await db.Tags.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new TaxonomyItemResponse(x.Id, x.Name, x.Slug))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
}

internal static class CatalogHandlerSupport
{
    public static Guid RequireOrganization(ICurrentRequest current) =>
        current.OrganizationId ?? throw new InvalidOperationException("Organization context is required.");

    public static async Task<string> GetCurrencyAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var currency = await db.Organizations.AsNoTracking()
            .Select(x => x.DefaultCurrency)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(currency) ? "ZAR" : currency;
    }

    public static Task<Product?> LoadProduct(IApplicationDbContext db, Guid productId, CancellationToken cancellationToken) =>
        db.Products
            .Include(x => x.Variants).ThenInclude(x => x.Inventory)
            .Include(x => x.Images)
            .Include(x => x.Categories)
            .Include(x => x.Tags)
            .SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);

    public static async Task EnsureSlugAvailable(
        IApplicationDbContext db, string slug, Guid? excludingProductId, CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var exists = await db.Products
            .AnyAsync(x => x.Slug == normalized && (excludingProductId == null || x.Id != excludingProductId), cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"Product slug '{normalized}' is already in use.");
        }
    }

    public static async Task EnsureTaxonomyExists(
        IApplicationDbContext db,
        Guid? teamId,
        Guid? seasonId,
        IReadOnlyCollection<Guid>? categoryIds,
        IReadOnlyCollection<Guid>? tagIds,
        CancellationToken cancellationToken)
    {
        if (teamId is { } tid && !await db.Teams.AnyAsync(x => x.Id == tid, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Team was not found.");
        }

        if (seasonId is { } sid && !await db.Seasons.AnyAsync(x => x.Id == sid, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Season was not found.");
        }

        if (categoryIds is { Count: > 0 })
        {
            var count = await db.Categories.CountAsync(x => categoryIds.Contains(x.Id), cancellationToken).ConfigureAwait(false);
            if (count != categoryIds.Distinct().Count())
            {
                throw new InvalidOperationException("One or more categories were not found.");
            }
        }

        if (tagIds is { Count: > 0 })
        {
            var count = await db.Tags.CountAsync(x => tagIds.Contains(x.Id), cancellationToken).ConfigureAwait(false);
            if (count != tagIds.Distinct().Count())
            {
                throw new InvalidOperationException("One or more tags were not found.");
            }
        }
    }
}
