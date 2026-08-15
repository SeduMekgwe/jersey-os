using Asp.Versioning;
using JerseyOs.Application;
using JerseyOs.Contracts;
using JerseyOs.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JerseyOs.Api;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = Permissions.CatalogRead)]
[Route("api/v{version:apiVersion}/catalog")]
public sealed class CatalogController(ISender sender) : ControllerBase
{
    [HttpGet("products")]
    [ProducesResponseType<PagedProductsResponse>(StatusCodes.Status200OK)]
    public Task<PagedProductsResponse> Search(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] Guid? teamId,
        [FromQuery] Guid? seasonId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        sender.Send(new SearchProductsQuery(search, status, teamId, seasonId, page, pageSize), cancellationToken);

    [HttpGet("products/{productId:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Get(Guid productId, CancellationToken cancellationToken)
    {
        var product = await sender.Send(new GetProductQuery(productId), cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost("products")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await sender.Send(
            new CreateProductCommand(
                request.Name,
                request.Slug,
                request.StyleCode,
                request.TeamId,
                request.SeasonId,
                request.CategoryIds,
                request.TagIds,
                request.SeoTitle,
                request.SeoDescription,
                request.SeoHandle),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { productId = product.Id, version = "1.0" }, product);
    }

    [HttpPut("products/{productId:guid}")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid productId, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await sender.Send(
            new UpdateProductCommand(
                productId,
                request.Name,
                request.Slug,
                request.StyleCode,
                request.TeamId,
                request.SeasonId,
                request.CategoryIds,
                request.TagIds,
                request.SeoTitle,
                request.SeoDescription,
                request.SeoHandle,
                request.Description),
            cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost("products/{productId:guid}/activate")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Activate(Guid productId, CancellationToken cancellationToken)
    {
        var product = await sender.Send(new ActivateProductCommand(productId), cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost("products/{productId:guid}/archive")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Archive(Guid productId, CancellationToken cancellationToken)
    {
        var product = await sender.Send(new ArchiveProductCommand(productId), cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPut("products/{productId:guid}/variants")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> UpsertVariant(
        Guid productId, UpsertVariantRequest request, CancellationToken cancellationToken)
    {
        var product = await sender.Send(
            new UpsertVariantCommand(
                productId,
                request.Id,
                request.Sku,
                request.Size,
                request.SortOrder,
                request.PriceAmount,
                request.CostAmount,
                request.CompareAtAmount),
            cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("products/{productId:guid}/variants/{variantId:guid}")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> RemoveVariant(
        Guid productId, Guid variantId, CancellationToken cancellationToken)
    {
        var product = await sender.Send(new RemoveVariantCommand(productId, variantId), cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost("products/{productId:guid}/images")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> AttachImage(
        Guid productId,
        IFormFile file,
        [FromForm] string? altText,
        [FromForm] int sortOrder,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            return BadRequest();
        }

        await using var stream = file.OpenReadStream();
        var product = await sender.Send(
            new AttachProductImageCommand(
                productId,
                stream,
                file.FileName,
                file.ContentType,
                altText,
                sortOrder),
            cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPut("products/{productId:guid}/images/order")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> ReorderImages(
        Guid productId, ReorderImagesRequest request, CancellationToken cancellationToken)
    {
        var product = await sender.Send(new ReorderProductImagesCommand(productId, request.Items), cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("products/{productId:guid}/images/{imageId:guid}")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> RemoveImage(
        Guid productId, Guid imageId, CancellationToken cancellationToken)
    {
        var product = await sender.Send(new RemoveProductImageCommand(productId, imageId), cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("teams")]
    public Task<IReadOnlyCollection<TaxonomyItemResponse>> ListTeams(CancellationToken cancellationToken) =>
        sender.Send(new ListTeamsQuery(), cancellationToken);

    [HttpPost("teams")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    public Task<TaxonomyItemResponse> CreateTeam(CreateTaxonomyItemRequest request, CancellationToken cancellationToken) =>
        sender.Send(new CreateTeamCommand(request.Name, request.Slug), cancellationToken);

    [HttpPut("teams/{id:guid}")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    public async Task<ActionResult<TaxonomyItemResponse>> UpdateTeam(
        Guid id, UpdateTaxonomyItemRequest request, CancellationToken cancellationToken)
    {
        var item = await sender.Send(new UpdateTeamCommand(id, request.Name, request.Slug), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("seasons")]
    public Task<IReadOnlyCollection<TaxonomyItemResponse>> ListSeasons(CancellationToken cancellationToken) =>
        sender.Send(new ListSeasonsQuery(), cancellationToken);

    [HttpPost("seasons")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    public Task<TaxonomyItemResponse> CreateSeason(CreateTaxonomyItemRequest request, CancellationToken cancellationToken) =>
        sender.Send(new CreateSeasonCommand(request.Name, request.Slug), cancellationToken);

    [HttpPut("seasons/{id:guid}")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    public async Task<ActionResult<TaxonomyItemResponse>> UpdateSeason(
        Guid id, UpdateTaxonomyItemRequest request, CancellationToken cancellationToken)
    {
        var item = await sender.Send(new UpdateSeasonCommand(id, request.Name, request.Slug), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("categories")]
    public Task<IReadOnlyCollection<TaxonomyItemResponse>> ListCategories(CancellationToken cancellationToken) =>
        sender.Send(new ListCategoriesQuery(), cancellationToken);

    [HttpPost("categories")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    public Task<TaxonomyItemResponse> CreateCategory(
        CreateTaxonomyItemRequest request, CancellationToken cancellationToken) =>
        sender.Send(new CreateCategoryCommand(request.Name, request.Slug, request.ParentId), cancellationToken);

    [HttpPut("categories/{id:guid}")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    public async Task<ActionResult<TaxonomyItemResponse>> UpdateCategory(
        Guid id, UpdateTaxonomyItemRequest request, CancellationToken cancellationToken)
    {
        var item = await sender.Send(
            new UpdateCategoryCommand(id, request.Name, request.Slug, request.ParentId),
            cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("tags")]
    public Task<IReadOnlyCollection<TaxonomyItemResponse>> ListTags(CancellationToken cancellationToken) =>
        sender.Send(new ListTagsQuery(), cancellationToken);

    [HttpPost("tags")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    public Task<TaxonomyItemResponse> CreateTag(CreateTaxonomyItemRequest request, CancellationToken cancellationToken) =>
        sender.Send(new CreateTagCommand(request.Name, request.Slug), cancellationToken);

    [HttpPut("tags/{id:guid}")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    public async Task<ActionResult<TaxonomyItemResponse>> UpdateTag(
        Guid id, UpdateTaxonomyItemRequest request, CancellationToken cancellationToken)
    {
        var item = await sender.Send(new UpdateTagCommand(id, request.Name, request.Slug), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("collections")]
    [ProducesResponseType<IReadOnlyCollection<CollectionResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<CollectionResponse>> ListCollections(CancellationToken cancellationToken) =>
        sender.Send(new ListCollectionsQuery(), cancellationToken);

    [HttpGet("collections/{collectionId:guid}")]
    [ProducesResponseType<CollectionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CollectionResponse>> GetCollection(
        Guid collectionId, CancellationToken cancellationToken)
    {
        var collection = await sender.Send(new GetCollectionQuery(collectionId), cancellationToken);
        return collection is null ? NotFound() : Ok(collection);
    }

    [HttpPost("collections")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [ProducesResponseType<CollectionResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CollectionResponse>> CreateCollection(
        CreateCollectionRequest request, CancellationToken cancellationToken)
    {
        var collection = await sender.Send(
            new CreateCollectionCommand(
                request.Name,
                request.Slug,
                request.MembershipKind,
                request.Description,
                request.TeamId,
                request.SeasonId,
                request.CategoryId,
                request.TagId,
                request.ProductIds),
            cancellationToken);
        return CreatedAtAction(nameof(GetCollection), new { collectionId = collection.Id, version = "1.0" }, collection);
    }

    [HttpPut("collections/{collectionId:guid}")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [ProducesResponseType<CollectionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CollectionResponse>> UpdateCollection(
        Guid collectionId, UpdateCollectionRequest request, CancellationToken cancellationToken)
    {
        var collection = await sender.Send(
            new UpdateCollectionCommand(
                collectionId,
                request.Name,
                request.Slug,
                request.Description,
                request.TeamId,
                request.SeasonId,
                request.CategoryId,
                request.TagId,
                request.ProductIds),
            cancellationToken);
        return collection is null ? NotFound() : Ok(collection);
    }

    [HttpDelete("collections/{collectionId:guid}")]
    [Authorize(Policy = Permissions.CatalogWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCollection(Guid collectionId, CancellationToken cancellationToken)
    {
        var deleted = await sender.Send(new DeleteCollectionCommand(collectionId), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = Permissions.PricingRead)]
[Route("api/v{version:apiVersion}/catalog/pricing-rules")]
public sealed class PricingRulesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<PricingRuleResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<PricingRuleResponse>> List(CancellationToken cancellationToken) =>
        sender.Send(new ListPricingRulesQuery(), cancellationToken);

    [HttpPost]
    [Authorize(Policy = Permissions.PricingWrite)]
    [ProducesResponseType<PricingRuleResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PricingRuleResponse>> Create(
        CreatePricingRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await sender.Send(
            new CreatePricingRuleCommand(
                request.Name,
                request.Kind,
                request.PercentRate,
                request.Priority,
                request.SalesChannelId,
                request.IsEnabled),
            cancellationToken);
        return CreatedAtAction(nameof(List), new { version = "1.0" }, rule);
    }

    [HttpPut("{ruleId:guid}")]
    [Authorize(Policy = Permissions.PricingWrite)]
    [ProducesResponseType<PricingRuleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PricingRuleResponse>> Update(
        Guid ruleId, UpdatePricingRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await sender.Send(
            new UpdatePricingRuleCommand(
                ruleId,
                request.Name,
                request.Kind,
                request.PercentRate,
                request.Priority,
                request.SalesChannelId,
                request.IsEnabled),
            cancellationToken);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpDelete("{ruleId:guid}")]
    [Authorize(Policy = Permissions.PricingWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid ruleId, CancellationToken cancellationToken)
    {
        var deleted = await sender.Send(new DeletePricingRuleCommand(ruleId), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("preview")]
    [ProducesResponseType<PricePreviewResponse>(StatusCodes.Status200OK)]
    public Task<PricePreviewResponse> Preview(PricePreviewRequest request, CancellationToken cancellationToken) =>
        sender.Send(
            new PreviewPricingCommand(
                request.CostAmount,
                request.ExplicitPriceAmount,
                request.ExplicitCompareAtAmount,
                request.SalesChannelId),
            cancellationToken);
}

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = Permissions.InventoryAdjust)]
[Route("api/v{version:apiVersion}/inventory")]
public sealed class InventoryController(ISender sender) : ControllerBase
{
    [HttpPost("variants/{variantId:guid}/adjust")]
    [ProducesResponseType<InventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryResponse>> Adjust(
        Guid variantId, AdjustInventoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var inventory = await sender.Send(
                new AdjustInventoryCommand(variantId, request.DeltaOnHand, request.Reason, request.ExpectedRowVersion),
                cancellationToken);
            return inventory is null ? NotFound() : Ok(inventory);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
    }
}
